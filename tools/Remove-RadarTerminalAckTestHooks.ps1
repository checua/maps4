param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$programPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
if (-not (Test-Path -LiteralPath $programPath)) {
    throw "Listener Program.cs not found: $programPath"
}

$resolved = Resolve-Path -LiteralPath $programPath
$content = [System.IO.File]::ReadAllText($resolved)
$original = $content

$patterns = @(
    '(?ms)^[ \t]*string\? terminalAckTest = Environment\.GetEnvironmentVariable\("RADAR_TERMINAL_ACK_TEST"\)\?\.Trim\(\);[ \t]*\r?\n[ \t]*if \(string\.Equals\(terminalAckTest, "crash-after-central", StringComparison\.OrdinalIgnoreCase\) &&[ \t]*\r?\n[ \t]*text\.Contains\("CENTRAL 012", StringComparison\.OrdinalIgnoreCase\)\)[ \t]*\r?\n[ \t]*\{[ \t]*\r?\n[ \t]*Console\.WriteLine\([ \t]*\r?\n[ \t]*"  \[TERMINAL ACK TEST\] Central Intelligence \+ matching persisted for CENTRAL 012; " \+[ \t]*\r?\n[ \t]*"terminating Agent before any downstream processing or Delivery preparation\."\);[ \t]*\r?\n[ \t]*Environment\.Exit\(87\);[ \t]*\r?\n[ \t]*\}[ \t]*\r?\n(?:[ \t]*\r?\n)?',
    '(?ms)^[ \t]*string\? workflowTest = Environment\.GetEnvironmentVariable\("RADAR_WORKFLOW_TEST"\)\?\.Trim\(\);[ \t]*\r?\n[ \t]*if \(RadarCentralIntelligenceClient\.Habilitada &&[ \t]*\r?\n[ \t]*string\.Equals\(workflowTest, "crash-after-central", StringComparison\.OrdinalIgnoreCase\)\)[ \t]*\r?\n[ \t]*\{[ \t]*\r?\n[ \t]*Console\.WriteLine\([ \t]*\r?\n[ \t]*"  \[WORKFLOW TEST\] Central processing completed; terminating Agent before downstream work starts\."\);[ \t]*\r?\n[ \t]*Environment\.Exit\(87\);[ \t]*\r?\n[ \t]*\}[ \t]*\r?\n(?:[ \t]*\r?\n)?'
)

$removed = 0
foreach ($pattern in $patterns) {
    $matches = [regex]::Matches($content, $pattern)
    if ($matches.Count -gt 1) {
        throw "A RADAR test hook matched more than once; refusing to modify Program.cs."
    }
    if ($matches.Count -eq 1) {
        $content = [regex]::Replace($content, $pattern, '', 1)
        $removed++
    }
}

if ($removed -eq 0) {
    Write-Host 'No RADAR terminal ACK test hooks were present.'
    exit 0
}

if ($content.Contains('RADAR_TERMINAL_ACK_TEST') -or $content.Contains('RADAR_WORKFLOW_TEST')) {
    throw 'A RADAR terminal ACK test marker still remains after cleanup; Program.cs was not written.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)

Write-Host "Removed $removed RADAR terminal ACK test hook(s) from Listener Program.cs."
Write-Host 'Production workflow code was preserved.'
