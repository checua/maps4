param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $RepoRoot 'tools\Apply-RadarDurableRestartRecovery.ps1'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Restart recovery helper not found: $sourcePath"
}

# Windows PowerShell 5.1 treats UTF-8 without BOM as ANSI. The original helper
# contains a non-ASCII separator in one source-rewrite line, so parse it through
# a temporary UTF-8-BOM copy and replace that rewrite with an ASCII-only regex.
$content = [System.IO.File]::ReadAllText(
    (Resolve-Path -LiteralPath $sourcePath),
    [System.Text.Encoding]::UTF8)

$lines = [System.Text.RegularExpressions.Regex]::Split($content, "\r?\n")
$out = New-Object System.Collections.Generic.List[string]
$inserted = $false

foreach ($line in $lines) {
    $isOldSeparatorRewrite =
        $line.Contains('$listenerProgram = $listenerProgram.Replace(') -and
        $line.Contains(' prepared ') -and
        $line.Contains(' attempt ')

    if ($isOldSeparatorRewrite) {
        continue
    }

    $out.Add($line)

    if ($line -eq '# Remove the mojibake separator observed in Windows console output.') {
        $out.Add('$listenerProgram = [System.Text.RegularExpressions.Regex]::Replace($listenerProgram, '' prepared [^a-zA-Z0-9\r\n]*attempt '', '' prepared - attempt '')')
        $inserted = $true
    }
}

if (-not $inserted) {
    throw 'Could not locate separator-rewrite section in restart recovery helper.'
}

$tempPath = Join-Path $env:TEMP ("Apply-RadarDurableRestartRecovery-{0}.ps1" -f [Guid]::NewGuid().ToString('N'))
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

try {
    [System.IO.File]::WriteAllText(
        $tempPath,
        ($out -join [Environment]::NewLine),
        $utf8Bom)

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $tempPath -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Restart recovery helper failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
}
