param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$old = @'
static async Task EstabilizarMensajesChat(IPage page, HashSet<string> ids)
{
    var sinCambios = 0;
    var anterior = -1;

    for (var intento = 0; intento < 6 && sinCambios < 2; intento++)
    {
        await AbsorberMensajesActuales(page, ids);

        if (ids.Count == anterior)
            sinCambios++;
        else
            sinCambios = 0;

        anterior = ids.Count;
        await Task.Delay(400);
    }
}
'@

$new = @'
static async Task EstabilizarMensajesChat(IPage page, HashSet<string> ids)
{
    const int maxIntentos = 8;
    const int minimoIntentos = 6;
    const int lecturasEstablesNecesarias = 2;

    var sinCambios = 0;
    var anterior = -1;

    for (var intento = 0; intento < maxIntentos; intento++)
    {
        await AbsorberMensajesActuales(page, ids);

        if (ids.Count == anterior)
            sinCambios++;
        else
            sinCambios = 0;

        anterior = ids.Count;

        var cumplioVentanaMinima = intento + 1 >= minimoIntentos;
        if (cumplioVentanaMinima && sinCambios >= lecturasEstablesNecesarias)
            break;

        await Task.Delay(400);
    }
}
'@

$resolvedPath = (Resolve-Path -LiteralPath $ProgramPath).Path
$content = [System.IO.File]::ReadAllText($resolvedPath)

if ($content.Contains($new)) {
    Write-Host "Per-chat settling-window patch is already applied."
    exit 0
}

if (-not $content.Contains($old)) {
    throw "Expected EstabilizarMensajesChat block was not found. Program.cs was not modified."
}

$updated = $content.Replace($old, $new)

if ($updated -eq $content) {
    throw "Replacement produced no change. Program.cs was not modified."
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedPath, $updated, $utf8NoBom)

Write-Host "Applied Radar per-chat settling-window experiment."
Write-Host ""
& git diff --check -- $ProgramPath
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check reported a problem."
}

& git diff -- $ProgramPath
