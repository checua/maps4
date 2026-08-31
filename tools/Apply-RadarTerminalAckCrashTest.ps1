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
$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

# Anchor on the ordinary WhatsApp path's unique ACK candidate line.
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
# the same local block before inserting/repairing the crash hook.
$interpretMarker = 'interpreter.InterpretarAsync(radarMessage);'
$interpretIndex = $content.LastIndexOf($interpretMarker, $first, [System.StringComparison]::Ordinal)
if ($interpretIndex -lt 0 -or ($first - $interpretIndex) -gt 2000) {
    throw 'Could not confirm InterpretarAsync(radarMessage) before the ordinary message ACK candidate.'
}

$markerLineStart = $content.LastIndexOf("`n", $first)
if ($markerLineStart -lt 0) {
    $markerLineStart = 0
}
else {
    $markerLineStart++
}

$indentLength = 0
while (($markerLineStart + $indentLength) -lt $content.Length) {
    $ch = $content[$markerLineStart + $indentLength]
    if ($ch -ne ' ' -and $ch -ne "`t") {
        break
    }
    $indentLength++
}
$indent = $content.Substring($markerLineStart, $indentLength)

# Parenthesize every expression. In Windows PowerShell 5.1, leaving these
# expressions unparenthesized inside @() can collapse them into one line.
$hookLines = @(
    ($indent + 'string? terminalAckTest = Environment.GetEnvironmentVariable("RADAR_TERMINAL_ACK_TEST")?.Trim();')
    ($indent + 'if (string.Equals(terminalAckTest, "crash-after-central", StringComparison.OrdinalIgnoreCase) &&')
    ($indent + '    text.Contains("CENTRAL 012", StringComparison.OrdinalIgnoreCase))')
    ($indent + '{')
    ($indent + '    Console.WriteLine(')
    ($indent + '        "  [TERMINAL ACK TEST] Central Intelligence + matching persisted for CENTRAL 012; " +')
    ($indent + '        "terminating Agent before any downstream processing or Delivery preparation.");')
    ($indent + '    Environment.Exit(87);')
    ($indent + '}')
    ''
)
$hook = [string]::Join($newLine, $hookLines) + $newLine

$hookMarker = 'string? terminalAckTest = Environment.GetEnvironmentVariable("RADAR_TERMINAL_ACK_TEST")?.Trim();'
$hookIndex = $content.LastIndexOf($hookMarker, $first, [System.StringComparison]::Ordinal)

if ($hookIndex -ge 0) {
    # Repair an already-applied hook, including the malformed one-line version
    # produced by the previous helper. Replace everything from the hook's line
    # start up to demandasInterpretadas.Add(id).
    $hookLineStart = $content.LastIndexOf("`n", $hookIndex)
    if ($hookLineStart -lt 0) {
        $hookLineStart = 0
    }
    else {
        $hookLineStart++
    }

    if ($hookLineStart -gt $markerLineStart) {
        throw 'Existing RADAR terminal ACK test hook is not before the expected marker.'
    }

    $content = $content.Substring(0, $hookLineStart) + $hook + $content.Substring($markerLineStart)
    Write-Utf8NoBom -Path $programPath -Content $content
    Write-Host 'RADAR terminal ACK crash test hook repaired and normalized.'
}
else {
    $content = $content.Insert($markerLineStart, $hook)
    Write-Utf8NoBom -Path $programPath -Content $content
    Write-Host 'RADAR terminal ACK crash test hook applied.'
}

Write-Host 'Use only with RADAR_TERMINAL_ACK_TEST=crash-after-central and a message containing CENTRAL 012.'
