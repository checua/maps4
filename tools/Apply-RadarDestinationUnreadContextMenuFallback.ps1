param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)

$pattern = '(?s)static async Task<bool> MarcarChatEnListaComoNoLeido\(IPage page, string nombreChat\)\s*\{.*?\n\}\s*\n\s*static string ConstruirAlerta'

$replacement = @'
static async Task<bool> MarcarChatEnListaComoNoLeido(IPage page, string nombreChat)
{
    try
    {
        await LimpiarBusqueda(page);

        var titulos = page.Locator("[data-testid='cell-frame-title']");
        ILocator? tituloObjetivo = null;

        async Task<bool> BuscarTituloVisibleAsync()
        {
            for (var i = 0; i < await titulos.CountAsync(); i++)
            {
                var titulo = titulos.Nth(i);
                if (!await titulo.IsVisibleAsync())
                    continue;

                var texto = (await titulo.InnerTextAsync()).Trim();
                if (EsMismoChat(texto, nombreChat))
                {
                    tituloObjetivo = titulo;
                    return true;
                }
            }

            return false;
        }

        if (!await BuscarTituloVisibleAsync())
        {
            var input = await ObtenerInputBusqueda(page);
            if (input is null)
                return false;

            await input.ClickAsync();
            await input.FillAsync(nombreChat);
            await Task.Delay(Math.Max(RadarSettings.EsperaBusquedaMs, 900));

            titulos = page.Locator("[data-testid='cell-frame-title']");
            if (!await BuscarTituloVisibleAsync())
            {
                await LimpiarBusqueda(page);
                return false;
            }
        }

        var fila = tituloObjetivo!.Locator(
            "xpath=ancestor::*[@role='row' or @role='listitem' or @tabindex='-1' or @tabindex='0'][1]");

        if (await fila.CountAsync() == 0)
            fila = tituloObjetivo.Locator("xpath=ancestor::div[6]");

        if (await fila.CountAsync() == 0)
        {
            await LimpiarBusqueda(page);
            return false;
        }

        var filaChat = fila.First;
        var opciones = new[]
        {
            "Marcar como no leído",
            "Marcar como no leido",
            "Mark as unread"
        };

        async Task<bool> ClickOpcionNoLeidoAsync()
        {
            foreach (var texto in opciones)
            {
                var opcion = page.GetByText(texto, new PageGetByTextOptions { Exact = true });
                var total = await opcion.CountAsync();

                for (var i = 0; i < total; i++)
                {
                    var item = opcion.Nth(i);
                    if (!await item.IsVisibleAsync())
                        continue;

                    await item.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                    await Task.Delay(350);
                    await LimpiarBusqueda(page);
                    return true;
                }
            }

            return false;
        }

        // Current WhatsApp Web versions reliably expose the chat actions with a
        // context click even when the hover-only dropdown icon changes its DOM.
        try
        {
            await filaChat.ClickAsync(new LocatorClickOptions
            {
                Button = MouseButton.Right,
                Timeout = 2_000
            });
            await Task.Delay(300);

            if (await ClickOpcionNoLeidoAsync())
            {
                Console.WriteLine($"  [UNREAD] {nombreChat}: marcado como no leído mediante menú contextual.");
                return true;
            }

            await page.Keyboard.PressAsync("Escape");
        }
        catch
        {
            try { await page.Keyboard.PressAsync("Escape"); } catch { }
        }

        // Fallback for older WhatsApp DOMs that still expose a hover dropdown.
        await filaChat.HoverAsync();
        await Task.Delay(250);

        ILocator? botonMenu = null;
        var selectoresMenu = new[]
        {
            "span[data-icon='down-context']",
            "[data-testid='down']",
            "button[aria-label*='menú' i]",
            "button[aria-label*='menu' i]"
        };

        foreach (var selector in selectoresMenu)
        {
            var candidatos = filaChat.Locator(selector);
            var total = await candidatos.CountAsync();

            for (var i = 0; i < total; i++)
            {
                var candidato = candidatos.Nth(i);
                if (!await candidato.IsVisibleAsync())
                    continue;

                if (selector.StartsWith("span", StringComparison.OrdinalIgnoreCase))
                {
                    var ancestro = candidato.Locator(
                        "xpath=ancestor::*[@role='button' or self::button or @tabindex='0'][1]");
                    botonMenu = await ancestro.CountAsync() > 0 ? ancestro.First : candidato;
                }
                else
                {
                    botonMenu = candidato;
                }
                break;
            }

            if (botonMenu is not null)
                break;
        }

        if (botonMenu is not null)
        {
            await botonMenu.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            await Task.Delay(250);

            if (await ClickOpcionNoLeidoAsync())
            {
                Console.WriteLine($"  [UNREAD] {nombreChat}: marcado como no leído mediante menú desplegable.");
                return true;
            }
        }

        await page.Keyboard.PressAsync("Escape");
        await LimpiarBusqueda(page);
        return false;
    }
    catch
    {
        try { await page.Keyboard.PressAsync("Escape"); } catch { }
        await LimpiarBusqueda(page);
        return false;
    }
}

static string ConstruirAlerta
'@

$regex = [regex]::new($pattern)
$matches = $regex.Matches($content)
if ($matches.Count -ne 1) {
    throw "Expected exactly one MarcarChatEnListaComoNoLeido block, found $($matches.Count)."
}

$updated = $regex.Replace($content, $replacement, 1)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $updated, $utf8NoBom)

Write-Host "Updated: $ProgramPath"
Write-Host "Destination unread marking now tries WhatsApp context-click first and keeps the previous hover-menu fallback."
Write-Host "Next: build, inspect git diff, and test one controlled real delivery."
