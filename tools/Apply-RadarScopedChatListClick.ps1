param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$old = @'
static async Task<bool> ClickPorTituloVisible(IPage page, string nombreChat)
{
    foreach (var candidato in CandidatosTitulo(nombreChat))
    {
        var exacto = page.GetByText(candidato, new PageGetByTextOptions { Exact = true });
        var count = await exacto.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var item = exacto.Nth(i);
            try
            {
                if (!await item.IsVisibleAsync())
                    continue;

                await item.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                return true;
            }
            catch
            {
                try
                {
                    var contenedor = item.Locator(
                        "xpath=ancestor::*[@tabindex='0' or @tabindex='-1' or @role='button' or @role='listitem' or @role='row'][1]");

                    if (await contenedor.CountAsync() > 0)
                    {
                        await contenedor.First.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                        return true;
                    }
                }
                catch { }
            }
        }
    }

    var titulos = page.Locator("[data-testid='cell-frame-title']");
    var total = await titulos.CountAsync();

    for (var i = 0; i < total; i++)
    {
        var titulo = titulos.Nth(i);
        try
        {
            if (!await titulo.IsVisibleAsync())
                continue;

            var texto = (await titulo.InnerTextAsync()).Trim();
            if (!EsMismoChat(texto, nombreChat))
                continue;

            await titulo.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            return true;
        }
        catch { }
    }

    return false;
}
'@

$new = @'
static async Task<bool> ClickPorTituloVisible(IPage page, string nombreChat)
{
    var listaChats = page.Locator("#pane-side");
    if (await listaChats.CountAsync() == 0)
        return false;

    foreach (var candidato in CandidatosTitulo(nombreChat))
    {
        var exacto = listaChats.GetByText(candidato, new LocatorGetByTextOptions { Exact = true });
        var count = await exacto.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var item = exacto.Nth(i);
            try
            {
                if (!await item.IsVisibleAsync())
                    continue;

                await item.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                return true;
            }
            catch
            {
                try
                {
                    var contenedor = item.Locator(
                        "xpath=ancestor::*[@tabindex='0' or @tabindex='-1' or @role='button' or @role='listitem' or @role='row'][1]");

                    if (await contenedor.CountAsync() > 0)
                    {
                        await contenedor.First.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                        return true;
                    }
                }
                catch { }
            }
        }
    }

    var titulos = listaChats.Locator("[data-testid='cell-frame-title']");
    var total = await titulos.CountAsync();

    for (var i = 0; i < total; i++)
    {
        var titulo = titulos.Nth(i);
        try
        {
            if (!await titulo.IsVisibleAsync())
                continue;

            var texto = (await titulo.InnerTextAsync()).Trim();
            if (!EsMismoChat(texto, nombreChat))
                continue;

            await titulo.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            return true;
        }
        catch { }
    }

    return false;
}
'@

$resolvedPath = (Resolve-Path -LiteralPath $ProgramPath).Path
$content = [System.IO.File]::ReadAllText($resolvedPath)
$normalized = $content.Replace("`r`n", "`n")

if ($normalized.Contains($new)) {
    Write-Host "Scoped chat-list click patch is already applied."
    exit 0
}

if (-not $normalized.Contains($old)) {
    throw "Expected ClickPorTituloVisible block was not found. Program.cs was not modified."
}

$updated = $normalized.Replace($old, $new)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedPath, $updated, $utf8NoBom)

Write-Host "Applied Radar scoped chat-list click experiment."
Write-Host ""
& git diff --check -- $ProgramPath
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check reported a problem."
}

& git diff -- $ProgramPath
