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

# 1. Inventory: expose Edit for owned non-terminal properties.
$oldInventoryFooter = @'
                        <a href="@($"/?inmuebleId={item.IdInmueble}")">Ver en mapa →</a>

                        @if (terminal)
'@
$newInventoryFooter = @'
                        <a href="@($"/?inmuebleId={item.IdInmueble}")">Ver en mapa →</a>
                        @if (esPropio && !terminal)
                        {
                            <a class="edit-property-link" asp-controller="Borrador" asp-action="Editar" asp-route-id="@item.IdInmueble">Editar</a>
                        }

                        @if (terminal)
'@
$inventory = Replace-Required $inventory $oldInventoryFooter $newInventoryFooter 'Inventory footer anchor'

# 2. Edit view: switch wording according to current state and hide discard outside BORRADOR.
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

$oldTop = @'
            <h1>Completar propiedad</h1>
            <p>Borrador #@Model.IdInmueble · puedes guardar ahora y continuar después.</p>
'@
$newTop = @'
            <h1>@(esBorrador ? "Completar propiedad" : "Editar propiedad")</h1>
            <p>@(esBorrador ? $"Borrador #{Model.IdInmueble} · puedes guardar ahora y continuar después." : $"Propiedad #{Model.IdInmueble} · estado {Model.EstadoCodigo} · guardar no cambia su estado comercial.")</p>
'@
$edit = Replace-Required $edit $oldTop $newTop 'Edit view title block'

$oldDiscard = @'
            <form asp-controller="Borrador" asp-action="Descartar" method="post"
                  onsubmit="return confirm('¿Descartar definitivamente este borrador? Se eliminarán sus datos y fotos y no podrá recuperarse.');">
                @Html.AntiForgeryToken()
                <input type="hidden" name="idInmueble" value="@Model.IdInmueble" />
                <button type="submit" class="discard-draft">🗑 Descartar borrador</button>
            </form>
'@
$newDiscard = @'
            @if (esBorrador)
            {
                <form asp-controller="Borrador" asp-action="Descartar" method="post"
                      onsubmit="return confirm('¿Descartar definitivamente este borrador? Se eliminarán sus datos y fotos y no podrá recuperarse.');">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="idInmueble" value="@Model.IdInmueble" />
                    <button type="submit" class="discard-draft">🗑 Descartar borrador</button>
                </form>
            }
'@
$edit = Replace-Required $edit $oldDiscard $newDiscard 'Discard form block'

$oldSummary = @'
            <div class="muted">Aún no está publicada.</div>
'@
$newSummary = @'
            <div class="muted">@(esBorrador ? "Aún no está publicada." : esPublicado ? "Publicada: los cambios guardados se reflejan en la propiedad activa." : $"Estado actual: {Model.EstadoCodigo}.")</div>
'@
$edit = Replace-Required $edit $oldSummary $newSummary 'Summary state text'

$oldPublishNotice = @'
            <div class="publish-disabled">
                Publicar será una acción separada. Primero validaremos que la información y las fotos estén listas.
            </div>
'@
$newPublishNotice = @'
            <div class="publish-disabled">
                @if (esBorrador)
                {
                    <span>Publicar será una acción separada. Primero validaremos que la información y las fotos estén listas.</span>
                }
                else if (esPublicado)
                {
                    <span>Esta propiedad está publicada. Los cambios guardados se reflejarán en su información comercial sin despublicarla.</span>
                }
                else
                {
                    <span>Los cambios se guardarán sin modificar el estado comercial actual (@Model.EstadoCodigo).</span>
                }
            </div>
'@
$edit = Replace-Required $edit $oldPublishNotice $newPublishNotice 'Publication notice block'

$oldSaveNote = @'
                    <div class="publish-preview">Guardar no hace pública la propiedad. Puedes salir en cualquier momento y continuar desde Mi inventario.</div>
'@
$newSaveNote = @'
                    <div class="publish-preview">@(esBorrador ? "Guardar no hace pública la propiedad. Puedes salir en cualquier momento y continuar desde Mi inventario." : "Guardar actualiza la propiedad sin cambiar su estado comercial. Puedes volver a Mi inventario cuando quieras.")</div>
'@
$edit = Replace-Required $edit $oldSaveNote $newSaveNote 'Save note block'

# 3. Controller: make user-facing messages state-neutral.
$replacements = @(
    @('TempData["InventarioOk"] = $"Borrador #{modelo.IdInmueble} guardado. Puedes continuar cuando quieras.";','TempData["InventarioOk"] = $"Propiedad #{modelo.IdInmueble} guardada. Puedes continuar cuando quieras.";'),
    @('TempData["BorradorOk"] = "Cambios guardados. El inmueble sigue siendo un borrador privado.";','TempData["BorradorOk"] = "Cambios guardados correctamente.";'),
    @('TempData["BorradorOk"] = "Portada actualizada. El inmueble continúa privado.";','TempData["BorradorOk"] = "Portada actualizada.";'),
    @('TempData["BorradorOk"] = "Foto eliminada del borrador.";','TempData["BorradorOk"] = "Foto eliminada.";'),
    @('52923 => "Tu rol actual no tiene permiso para editar borradores.",','52923 => "Tu rol actual no tiene permiso para editar esta propiedad.",'),
    @('52924 => "El borrador ya no existe.",','52924 => "La propiedad ya no existe.",'),
    @('52925 => "El borrador pertenece a otra cuenta.",','52925 => "La propiedad pertenece a otra cuenta.",'),
    @('52926 => "Por ahora solo el asesor responsable puede completar este borrador.",','52926 => "Solo el asesor responsable puede editar esta propiedad.",'),
    @('52927 => "Este inmueble ya no está en estado Borrador.",','52927 => "Esta propiedad ya no está en un estado editable.",'),
    @('_ => "No fue posible guardar el borrador. Intenta nuevamente."','_ => "No fue posible guardar la propiedad. Intenta nuevamente."')
)
foreach ($pair in $replacements) {
    $controller = Replace-Required $controller $pair[0] $pair[1] "Controller text: $($pair[0])"
}

# Add errors introduced by migration 46.
$oldPriceError = @'
                52933 => "El precio no puede ser negativo.",
                _ => "No fue posible guardar la propiedad. Intenta nuevamente."
'@
$newPriceError = @'
                52933 => "El precio no puede ser negativo.",
                52934 => "Una propiedad comercializada debe conservar un precio mayor que cero.",
                52935 => "Una propiedad comercializada debe conservar al menos una superficie.",
                52936 => "Una propiedad comercializada debe conservar una descripción.",
                _ => "No fue posible guardar la propiedad. Intenta nuevamente."
'@
$controller = Replace-Required $controller $oldPriceError $newPriceError 'Controller published validation errors'

$oldPhotoError = @'
                53252 or 53262 => "La foto ya no existe o no pertenece al borrador.",
'@
$newPhotoError = @'
                53252 or 53262 => "La foto ya no existe o no pertenece a la propiedad.",
                53263 => "Una propiedad comercializada debe conservar al menos una foto.",
                53253 or 53264 or 53324 => "Tu rol actual no puede editar las fotos de esta propiedad.",
'@
$controller = Replace-Required $controller $oldPhotoError $newPhotoError 'Controller photo errors'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($inventoryPath, $inventory, $utf8NoBom)
[System.IO.File]::WriteAllText($editPath, $edit, $utf8NoBom)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)

Write-Host 'RSMaps owned property editing UI patch applied.'
Write-Host 'Changed: Views/Inventario/Index.cshtml'
Write-Host 'Changed: Views/Borrador/Editar.cshtml'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Next: run SQL step 46, build, and test an owned PUBLICADO property.'
