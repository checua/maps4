$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$inventoryPath = Join-Path $repoRoot 'Views\Inventario\Index.cshtml'
$editPath = Join-Path $repoRoot 'Views\Borrador\Editar.cshtml'
$controllerPath = Join-Path $repoRoot 'Controllers\BorradorController.cs'

foreach ($path in @($inventoryPath, $editPath, $controllerPath)) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }
}

$inventory = [System.IO.File]::ReadAllText($inventoryPath)
$edit = [System.IO.File]::ReadAllText($editPath)
$controller = [System.IO.File]::ReadAllText($controllerPath)

function Replace-Required([string]$content, [string]$old, [string]$new, [string]$label) {
    if (-not $content.Contains($old)) {
        throw "$label was not found. No files were changed."
    }
    return $content.Replace($old, $new)
}

function Regex-Replace-One([string]$content, [string]$pattern, [string]$replacement, [string]$label) {
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $matches = $regex.Matches($content)
    if ($matches.Count -ne 1) {
        throw "$label expected exactly one match but found $($matches.Count). No files were changed."
    }
    return $regex.Replace($content, $replacement, 1)
}

# 1. Inventory: expose Edit for owned non-terminal properties.
$inventoryAnchor = '                        @if (terminal)'
$inventoryInsert = @'
                        @if (esPropio && !terminal)
                        {
                            <a class="edit-property-link" asp-controller="Borrador" asp-action="Editar" asp-route-id="@item.IdInmueble">Editar</a>
                        }

                        @if (terminal)
'@
$inventory = Replace-Required $inventory $inventoryAnchor $inventoryInsert 'Inventory terminal anchor'

# 2. Edit view: state-aware title and labels.
$oldHeaderModel = @'
@{
    ViewData["Title"] = $"Borrador #{Model.IdInmueble} - RSMaps";
    var cultura = CultureInfo.GetCultureInfo("es-MX");
    var precioTexto = Model.TienePrecio ? Model.Precio!.Value.ToString("C0", cultura) : "Pendiente";
}
'@
$newHeaderModel = @'
@{
    var esBorrador = string.Equals(Model.EstadoCodigo, "BORRADOR", StringComparison.OrdinalIgnoreCase);
    var esPublicado = string.Equals(Model.EstadoCodigo, "PUBLICADO", StringComparison.OrdinalIgnoreCase);
    ViewData["Title"] = esBorrador ? $"Borrador #{Model.IdInmueble} - RSMaps" : $"Editar propiedad #{Model.IdInmueble} - RSMaps";
    var cultura = CultureInfo.GetCultureInfo("es-MX");
    var precioTexto = Model.TienePrecio ? Model.Precio!.Value.ToString("C0", cultura) : "Pendiente";
}
'@
$edit = Replace-Required $edit $oldHeaderModel $newHeaderModel 'Edit view model header'

$edit = Replace-Required $edit '            <h1>Completar propiedad</h1>' '            <h1>@(esBorrador ? "Completar propiedad" : "Editar propiedad")</h1>' 'Edit view H1'

$edit = Regex-Replace-One $edit '^\s*<p>Borrador #@Model\.IdInmueble.*</p>\r?$' '            <p>@(esBorrador ? $"Borrador #{Model.IdInmueble} \u00b7 puedes guardar ahora y continuar despu\u00e9s." : $"Propiedad #{Model.IdInmueble} \u00b7 estado {Model.EstadoCodigo} \u00b7 guardar no cambia su estado comercial.")</p>' 'Edit view subtitle'

# Wrap the discard form so it only appears for BORRADOR.
$discardPattern = '(?ms)^            <form asp-controller="Borrador" asp-action="Descartar" method="post".*?^            </form>'
$discardRegex = New-Object System.Text.RegularExpressions.Regex($discardPattern)
$discardMatches = $discardRegex.Matches($edit)
if ($discardMatches.Count -ne 1) {
    throw "Discard form expected exactly one match but found $($discardMatches.Count). No files were changed."
}
$discardBlock = $discardMatches[0].Value
$wrappedDiscard = "            @if (esBorrador)`r`n            {`r`n" + $discardBlock + "`r`n            }"
$edit = $discardRegex.Replace($edit, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $wrappedDiscard }, 1)

$edit = Regex-Replace-One $edit '^\s*<div class="muted">.*publicada\.</div>\r?$' '            <div class="muted">@(esBorrador ? "A\u00fan no est\u00e1 publicada." : esPublicado ? "Publicada: los cambios guardados se reflejan en la propiedad activa." : $"Estado actual: {Model.EstadoCodigo}.")</div>' 'Edit summary state'

$publishPattern = '(?ms)^            <div class="publish-disabled">.*?^            </div>'
$publishReplacement = @'
            <div class="publish-disabled">
                @if (esBorrador)
                {
                    <span>Publicar sera una accion separada. Primero validaremos que la informacion y las fotos esten listas.</span>
                }
                else if (esPublicado)
                {
                    <span>Esta propiedad esta publicada. Los cambios guardados se reflejaran en su informacion comercial sin despublicarla.</span>
                }
                else
                {
                    <span>Los cambios se guardaran sin modificar el estado comercial actual (@Model.EstadoCodigo).</span>
                }
            </div>
'@
$publishRegex = New-Object System.Text.RegularExpressions.Regex($publishPattern)
if ($publishRegex.Matches($edit).Count -ne 1) {
    throw 'Publication notice expected exactly one match. No files were changed.'
}
$edit = $publishRegex.Replace($edit, $publishReplacement, 1)

$edit = Regex-Replace-One $edit '^\s*<div class="publish-preview">.*</div>\r?$' '                    <div class="publish-preview">@(esBorrador ? "Guardar no hace publica la propiedad. Puedes salir en cualquier momento y continuar desde Mi inventario." : "Guardar actualiza la propiedad sin cambiar su estado comercial. Puedes volver a Mi inventario cuando quieras.")</div>' 'Save note'

# 3. Controller: state-neutral success text and errors added by SQL step 46.
$controller = Replace-Required $controller 'TempData["InventarioOk"] = $"Borrador #{modelo.IdInmueble} guardado. Puedes continuar cuando quieras.";' 'TempData["InventarioOk"] = $"Propiedad #{modelo.IdInmueble} guardada. Puedes continuar cuando quieras.";' 'Inventory success text'
$controller = Replace-Required $controller 'TempData["BorradorOk"] = "Cambios guardados. El inmueble sigue siendo un borrador privado.";' 'TempData["BorradorOk"] = "Cambios guardados correctamente.";' 'Save success text'
$controller = Replace-Required $controller 'TempData["BorradorOk"] = "Foto eliminada del borrador.";' 'TempData["BorradorOk"] = "Foto eliminada.";' 'Photo delete success text'

$priceAnchor = '                52933 => "El precio no puede ser negativo.",'
$priceInsert = @'
                52933 => "El precio no puede ser negativo.",
                52934 => "Una propiedad comercializada debe conservar un precio mayor que cero.",
                52935 => "Una propiedad comercializada debe conservar al menos una superficie.",
                52936 => "Una propiedad comercializada debe conservar una descripci\u00f3n.",
'@
$controller = Replace-Required $controller $priceAnchor $priceInsert 'Published validation error anchor'

$photoAnchor = '                53252 or 53262 => "La foto ya no existe o no pertenece al borrador.",'
$photoInsert = @'
                53252 or 53262 => "La foto ya no existe o no pertenece a la propiedad.",
                53263 => "Una propiedad comercializada debe conservar al menos una foto.",
                53253 or 53264 or 53324 => "Tu rol actual no puede editar las fotos de esta propiedad.",
'@
$controller = Replace-Required $controller $photoAnchor $photoInsert 'Photo validation error anchor'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($inventoryPath, $inventory, $utf8NoBom)
[System.IO.File]::WriteAllText($editPath, $edit, $utf8NoBom)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)

Write-Host 'RSMaps owned property editing UI patch applied.'
Write-Host 'Changed: Views/Inventario/Index.cshtml'
Write-Host 'Changed: Views/Borrador/Editar.cshtml'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Next: review diff, run SQL step 46, build, and test an owned PUBLICADO property.'
