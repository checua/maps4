$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$path = Join-Path $repoRoot 'RSMaps.Radar.Listener\Program.cs'

if (-not (Test-Path $path)) {
    throw "Program.cs not found: $path"
}

$content = [System.IO.File]::ReadAllText($path)
$startMarker = 'static async Task<bool> AbrirChat(IPage page, string nombreChat)'
$endMarker = 'static async Task<bool> ClickPorTituloVisible(IPage page, string nombreChat)'

$start = $content.IndexOf($startMarker, [System.StringComparison]::Ordinal)
if ($start -lt 0) {
    throw 'AbrirChat marker not found.'
}

$end = $content.IndexOf($endMarker, $start, [System.StringComparison]::Ordinal)
if ($end -lt 0) {
    throw 'ClickPorTituloVisible marker not found after AbrirChat.'
}

$newMethod = @'
static async Task<bool> AbrirChat(IPage page, string nombreChat)
{
    if (await ClickPorTituloVisible(page, nombreChat) && await EsperarChatAbierto(page, nombreChat))
    {
        Console.WriteLine($"  -> {nombreChat}: abierto desde lista visible.");
        return true;
    }

    await LimpiarBusqueda(page);
    var input = await ObtenerInputBusqueda(page);
    if (input is null)
        return false;

    var terminosBusqueda = RadarSettings.ObtenerTerminosBusqueda(nombreChat).ToList();
    if (terminosBusqueda.Count == 0 &&
        string.Equals(nombreChat, AlertSettings.ChatDestino, StringComparison.OrdinalIgnoreCase))
    {
        terminosBusqueda.Add(nombreChat);
    }

    foreach (var termino in terminosBusqueda)
    {
        try
        {
            Console.WriteLine($"  -> Buscando '{nombreChat}' con: {termino}");

            input = await ObtenerInputBusqueda(page) ?? input;
            await input.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            await input.FillAsync(string.Empty, new LocatorFillOptions { Timeout = 2_000 });
            await input.FillAsync(termino, new LocatorFillOptions { Timeout = 2_000 });

            var resultadoIdentificado = false;
            for (var intento = 0; intento < 6; intento++)
            {
                await Task.Delay(intento == 0
                    ? RadarSettings.EsperaBusquedaMs
                    : 250);

                if (!await ClickPorTituloVisible(page, nombreChat))
                    continue;

                resultadoIdentificado = true;
                break;
            }

            if (!resultadoIdentificado)
            {
                Console.WriteLine("     resultado visible no identificado tras reintentos.");
                await LimpiarBusqueda(page);
                continue;
            }

            if (await EsperarChatAbierto(page, nombreChat))
            {
                Console.WriteLine("     chat confirmado abierto.");
                await LimpiarBusqueda(page);
                return true;
            }

            Console.WriteLine("     resultado identificado, pero el chat no pudo confirmarse abierto.");
            await LimpiarBusqueda(page);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     fallo de navegacion acotado: {ex.Message}");
            await LimpiarBusqueda(page);
        }
    }

    await LimpiarBusqueda(page);
    return false;
}
'@

$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$newMethod = $newMethod -replace "`r?`n", $newLine
$newContent = $content.Substring(0, $start) + $newMethod + $newLine + $newLine + $content.Substring($end)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($path, $newContent, $utf8NoBom)

Write-Host 'RADAR navigation resilience patch applied.'
Write-Host 'Changed method: AbrirChat'
Write-Host 'Search input Click/Fill timeout: 2000 ms'
Write-Host 'Visible-result retry attempts: 6'
