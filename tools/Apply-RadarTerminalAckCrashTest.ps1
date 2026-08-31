param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

$programPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
if (-not (Test-Path -LiteralPath $programPath)) {
    throw "Listener Program.cs not found: $programPath"
}

$content = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $programPath))

if ($content.Contains('RADAR_TERMINAL_ACK_TEST')) {
    Write-Host 'RADAR terminal ACK crash test hook is already present.'
    exit 0
}

$anchor = @'
        var interpretacion = await interpreter.InterpretarAsync(radarMessage);
        demandasInterpretadas.Add(id);
'@

$replacement = @'
        var interpretacion = await interpreter.InterpretarAsync(radarMessage);

        string? terminalAckTest = Environment.GetEnvironmentVariable("RADAR_TERMINAL_ACK_TEST")?.Trim();
        if (string.Equals(terminalAckTest, "crash-after-central", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("CENTRAL 012", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "  [TERMINAL ACK TEST] Central Intelligence + matching persisted for CENTRAL 012; " +
                "terminating Agent before any downstream processing or Delivery preparation.");
            Environment.Exit(87);
        }

        demandasInterpretadas.Add(id);
'@

$first = $content.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($first -lt 0) {
    throw 'Could not locate ordinary message interpretation anchor in Listener Program.cs.'
}

$second = $content.IndexOf($anchor, $first + $anchor.Length, [System.StringComparison]::Ordinal)
if ($second -ge 0) {
    throw 'Ordinary message interpretation anchor is not unique; refusing to patch.'
}

$content = $content.Substring(0, $first) + $replacement + $content.Substring($first + $anchor.Length)
Write-Utf8NoBom -Path $programPath -Content $content

Write-Host 'RADAR terminal ACK crash test hook applied.'
Write-Host 'Use only with RADAR_TERMINAL_ACK_TEST=crash-after-central and a message containing CENTRAL 012.'
