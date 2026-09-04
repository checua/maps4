$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$programPath = Join-Path $repoRoot 'RSMaps.Radar.Listener\Program.cs'

if (-not (Test-Path $programPath)) {
    throw "Program.cs not found: $programPath"
}

$content = [System.IO.File]::ReadAllText($programPath)

$old = @'
            input = await ObtenerInputBusqueda(page) ?? input;
            await input.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            await input.FillAsync(string.Empty, new LocatorFillOptions { Timeout = 2_000 });
            await input.FillAsync(termino, new LocatorFillOptions { Timeout = 2_000 });
'@

$new = @'
            input = await ObtenerInputBusqueda(page) ?? input;
            // Fill already focuses and replaces the current value. Avoid an explicit ClickAsync:
            // WhatsApp occasionally leaves the search input visually ready while click dispatch stalls.
            await input.FillAsync(termino, new LocatorFillOptions { Timeout = 2_000 });
'@

if (-not $content.Contains($old)) {
    throw 'Expected AbrirChat search-input block was not found. No file was changed.'
}

$content = $content.Replace($old, $new)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($programPath, $content, $utf8NoBom)

Write-Host 'RADAR search fill patch applied.'
Write-Host 'Changed method: AbrirChat'
Write-Host 'Explicit search ClickAsync removed.'
Write-Host 'Search now uses one bounded FillAsync (2000 ms).'
