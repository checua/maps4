param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$old = @'
for (var ronda = 1; ronda <= 3; ronda++)
{
    foreach (var chat in idsConocidosPorChat.Keys.ToList())
    {
        if (!await AbrirChat(page, chat))
            continue;

        await EstabilizarMensajesChat(page, idsConocidosPorChat[chat]);
    }
}
'@

$new = @'
for (var ronda = 1; ronda <= 3; ronda++)
{
    var mensajesAntes = idsConocidosPorChat.Values.Sum(ids => ids.Count);

    foreach (var chat in idsConocidosPorChat.Keys.ToList())
    {
        if (!await AbrirChat(page, chat))
            continue;

        await EstabilizarMensajesChat(page, idsConocidosPorChat[chat]);
    }

    var mensajesDespues = idsConocidosPorChat.Values.Sum(ids => ids.Count);
    var mensajesAdicionales = mensajesDespues - mensajesAntes;

    Console.WriteLine(
        $"  Ronda {ronda}: {mensajesAdicionales} mensaje(s) adicional(es) absorbido(s).");

    if (mensajesAdicionales == 0)
    {
        Console.WriteLine("  Historial estable; se omiten rondas adicionales.");
        break;
    }
}
'@

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if ($content.Contains($new)) {
    # PowerShell 5.1's Set-Content -Encoding UTF8 would add a BOM.
    # Normalize the already-patched file back to UTF-8 without BOM.
    [System.IO.File]::WriteAllText($resolvedProgramPath, $content, $utf8NoBom)
    Write-Host "Early-exit startup patch is already applied; encoding normalized to UTF-8 without BOM."
    Write-Host ""
    & git diff --check -- $ProgramPath
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check reported a problem."
    }
    & git diff -- $ProgramPath
    exit 0
}

if (-not $content.Contains($old)) {
    throw "Expected stabilization block was not found. Program.cs was not modified."
}

$updated = $content.Replace($old, $new)

if ($updated -eq $content) {
    throw "Replacement produced no change. Program.cs was not modified."
}

[System.IO.File]::WriteAllText($resolvedProgramPath, $updated, $utf8NoBom)

Write-Host "Applied Radar startup early-exit patch."
Write-Host ""
& git diff --check -- $ProgramPath
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check reported a problem."
}

& git diff -- $ProgramPath
