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

# Anchor on the ordinary WhatsApp path's unique ACK candidate line instead of
# matching a whole multiline block. This survives CRLF/LF and generated code
# formatting changes from the terminal-workflow patch.
$marker = 'demandasInterpretadas.Add(id);'
$first = $content.IndexOf($marker, [System.StringComparison]::Ordinal)
if ($first -lt 0) {
    throw 'Could not locate demandasInterpretadas.Add(id) in Listener Program.cs.'
}
$second = $content.IndexOf($marker, $first + $marker.Length, [System.StringComparison]::Ordinal)
if ($second -ge 0) {
    throw 'demandasInterpretadas.Add(id) is not unique; refusing to patch.'
}

# Confirm the expected central interpretation call is immediately upstream in
# the same local block before inserting the crash hook.
$interpretMarker = 'interpreter.InterpretarAsync(radarMessage);'
$interpretIndex = $content.LastIndexOf($interpretMarker, $first, [System.StringComparison]::Ordinal)
if ($interpretIndex -lt 0 -or ($first - $interpretIndex) -gt 1200) {
    throw 'Could not confirm InterpretarAsync(radarMessage) before the ordinary message ACK candidate.'
}

$lineStart = $content.LastIndexOf("`n", $first)
if ($lineStart -lt 0) {
    $lineStart = 0
}
else {
    $lineStart++
}

$indentLength = 0
while (($lineStart + $indentLength) -lt $content.Length) {
    $ch = $content[$lineStart + $indentLength]
    if ($ch -ne ' ' -and $ch -ne "`t") {
        break
    }
    $indentLength++
}
$indent = $content.Substring($lineStart, $indentLength)
$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

$hookLines = @(
    $indent + 'string? terminalAckTest = Environment.GetEnvironmentVariable("RADAR_TERMINAL_ACK_TEST")?.Trim();',
    $indent + 'if (string.Equals(terminalAckTest, "crash-after-central", StringComparison.OrdinalIgnoreCase) &&',
    $indent + '    text.Contains("CENTRAL 012", StringComparison.OrdinalIgnoreCase))',
    $indent + '{',
    $indent + '    Console.WriteLine(',
    $indent + '        "  [TERMINAL ACK TEST] Central Intelligence + matching persisted for CENTRAL 012; " +',
    $indent + '        "terminating Agent before any downstream processing or Delivery preparation.");',
    $indent + '    Environment.Exit(87);',
    $indent + '}',
    ''
)
$hook = [string]::Join($newLine, $hookLines) + $newLine

$content = $content.Insert($lineStart, $hook)
Write-Utf8NoBom -Path $programPath -Content $content

Write-Host 'RADAR terminal ACK crash test hook applied.'
Write-Host 'Use only with RADAR_TERMINAL_ACK_TEST=crash-after-central and a message containing CENTRAL 012.'
