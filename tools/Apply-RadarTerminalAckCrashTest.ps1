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

# Match the ordinary WhatsApp interpretation path by the unique
# demandasInterpretadas.Add(id) that follows InterpretarAsync. In a
# PowerShell single-quoted string, regex escapes use ONE backslash.
$pattern = '(?m)(?<indent>^[ \t]*)var interpretacion = await interpreter\.InterpretarAsync\(radarMessage\);\r?\n\k<indent>demandasInterpretadas\.Add\(id\);'
$matches = [regex]::Matches($content, $pattern)

if ($matches.Count -eq 0) {
    # Fallback tolerates an explicit RadarInterpretationResult type and
    # optional blank whitespace without risking a broad unrelated match.
    $pattern = '(?m)(?<indent>^[ \t]*)(?:var|RadarInterpretationResult) interpretacion = await interpreter\.InterpretarAsync\(radarMessage\);[ \t]*\r?\n(?:[ \t]*\r?\n)*\k<indent>demandasInterpretadas\.Add\(id\);'
    $matches = [regex]::Matches($content, $pattern)
}

if ($matches.Count -eq 0) {
    throw 'Could not locate ordinary message interpretation anchor in Listener Program.cs.'
}
if ($matches.Count -ne 1) {
    throw "Ordinary message interpretation anchor matched $($matches.Count) times; refusing to patch."
}

$match = $matches[0]
$indent = $match.Groups['indent'].Value
$newLine = if ($match.Value.Contains("`r`n")) { "`r`n" } else { "`n" }

$replacementLines = @(
    $indent + 'var interpretacion = await interpreter.InterpretarAsync(radarMessage);',
    '',
    $indent + 'string? terminalAckTest = Environment.GetEnvironmentVariable("RADAR_TERMINAL_ACK_TEST")?.Trim();',
    $indent + 'if (string.Equals(terminalAckTest, "crash-after-central", StringComparison.OrdinalIgnoreCase) &&',
    $indent + '    text.Contains("CENTRAL 012", StringComparison.OrdinalIgnoreCase))',
    $indent + '{',
    $indent + '    Console.WriteLine(',
    $indent + '        "  [TERMINAL ACK TEST] Central Intelligence + matching persisted for CENTRAL 012; " +',
    $indent + '        "terminating Agent before any downstream processing or Delivery preparation.");',
    $indent + '    Environment.Exit(87);',
    $indent + '}',
    '',
    $indent + 'demandasInterpretadas.Add(id);'
)
$replacement = [string]::Join($newLine, $replacementLines)

$content = $content.Substring(0, $match.Index) + $replacement + $content.Substring($match.Index + $match.Length)
Write-Utf8NoBom -Path $programPath -Content $content

Write-Host 'RADAR terminal ACK crash test hook applied.'
Write-Host 'Use only with RADAR_TERMINAL_ACK_TEST=crash-after-central and a message containing CENTRAL 012.'
