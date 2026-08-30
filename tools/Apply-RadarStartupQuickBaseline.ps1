param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$old = @'
    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await EstabilizarMensajesChat(page, ids);
    idsConocidosPorChat[chat] = ids;
'@

$new = @'
    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await AbsorberMensajesActuales(page, ids);
    idsConocidosPorChat[chat] = ids;
'@

$resolvedPath = (Resolve-Path -LiteralPath $ProgramPath).Path
$content = [System.IO.File]::ReadAllText($resolvedPath)

if ($content.Contains("const int minimoIntentos = 6;")) {
    throw "The failed per-chat settling-window experiment is still applied. Run: git restore RSMaps.Radar.Listener/Program.cs"
}

# PowerShell 5.1 and Git may expose different line endings. Normalize only for matching,
# then write the resulting source back as UTF-8 without BOM.
$contentNormalized = $content.Replace("`r`n", "`n")
$oldNormalized = $old.Replace("`r`n", "`n")
$newNormalized = $new.Replace("`r`n", "`n")

if ($contentNormalized.Contains($newNormalized)) {
    Write-Host "Quick startup baseline patch is already applied."
    exit 0
}

if (-not $contentNormalized.Contains($oldNormalized)) {
    throw "Expected startup initialization block was not found. Program.cs was not modified."
}

$updated = $contentNormalized.Replace($oldNormalized, $newNormalized)

if ($updated -eq $contentNormalized) {
    throw "Replacement produced no change. Program.cs was not modified."
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedPath, $updated, $utf8NoBom)

Write-Host "Applied Radar quick-baseline startup experiment."
Write-Host ""
& git diff --check -- $ProgramPath
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check reported a problem."
}

& git diff -- $ProgramPath
