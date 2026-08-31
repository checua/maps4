param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $RepoRoot 'tools\Apply-RadarPendingProcessingRecovery.ps1'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Processing recovery helper not found: $sourcePath"
}

$content = [System.IO.File]::ReadAllText(
    (Resolve-Path -LiteralPath $sourcePath),
    [System.Text.Encoding]::UTF8)

$oldPrefixEnd = @'
{
'@ + $sharedBlock.Replace('resultado.DemandasInterpretadas', 'demandasInterpretadas').Replace('resultado.Solicitudes', 'solicitudes') + @'
}

static async Task RecuperarProcesamientosDurablesPendientesAsync(
'@

$newPrefixEnd = @'
{
'@
$sharedFunctionPrefix = $sharedFunction
$sharedFunctionSuffix = @'
}

static async Task RecuperarProcesamientosDurablesPendientesAsync(
'@
$sharedFunction = $sharedFunctionPrefix + $sharedBlock.Replace('resultado.DemandasInterpretadas', 'demandasInterpretadas').Replace('resultado.Solicitudes', 'solicitudes') + $sharedFunctionSuffix

$oldNeedle = [System.Environment]::NewLine + "'@ + `$sharedBlock.Replace('resultado.DemandasInterpretadas', 'demandasInterpretadas').Replace('resultado.Solicitudes', 'solicitudes') + @'" + [System.Environment]::NewLine + '}' + [System.Environment]::NewLine + [System.Environment]::NewLine + 'static async Task RecuperarProcesamientosDurablesPendientesAsync('

$newNeedle = [System.Environment]::NewLine + "'@" + [System.Environment]::NewLine + '`$sharedFunctionPrefix = `$sharedFunction' + [System.Environment]::NewLine + "`$sharedFunctionSuffix = @'" + [System.Environment]::NewLine + '}' + [System.Environment]::NewLine + [System.Environment]::NewLine + 'static async Task RecuperarProcesamientosDurablesPendientesAsync('

if (-not $content.Contains($oldNeedle)) {
    throw 'Could not locate PowerShell 5.1 here-string composition in processing recovery helper.'
}

$content = $content.Replace($oldNeedle, $newNeedle)

$closingNeedle = [System.Environment]::NewLine + "'@" + [System.Environment]::NewLine + [System.Environment]::NewLine + "`$deliveryFunctionMarker = 'static async Task RecuperarEntregasDurablesPendientesAsync('"
$closingReplacement = [System.Environment]::NewLine + "'@" + [System.Environment]::NewLine + "`$sharedFunction = `$sharedFunctionPrefix + `$sharedBlock.Replace('resultado.DemandasInterpretadas', 'demandasInterpretadas').Replace('resultado.Solicitudes', 'solicitudes') + `$sharedFunctionSuffix" + [System.Environment]::NewLine + [System.Environment]::NewLine + "`$deliveryFunctionMarker = 'static async Task RecuperarEntregasDurablesPendientesAsync('"

if (-not $content.Contains($closingNeedle)) {
    throw 'Could not locate processing recovery helper suffix.'
}

$content = $content.Replace($closingNeedle, $closingReplacement)

$tempPath = Join-Path $env:TEMP ("Apply-RadarPendingProcessingRecovery-{0}.ps1" -f [Guid]::NewGuid().ToString('N'))
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

try {
    [System.IO.File]::WriteAllText($tempPath, $content, $utf8Bom)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $tempPath -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Processing recovery helper failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
}
