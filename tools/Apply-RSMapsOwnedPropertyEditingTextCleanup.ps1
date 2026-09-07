$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$controllerPath = Join-Path $repoRoot 'Controllers\BorradorController.cs'
$viewPath = Join-Path $repoRoot 'Views\Borrador\Editar.cshtml'

foreach ($path in @($controllerPath, $viewPath)) {
    if (-not (Test-Path $path)) { throw "Required file not found: $path" }
}

$controller = [System.IO.File]::ReadAllText($controllerPath)
$view = [System.IO.File]::ReadAllText($viewPath)

$replacementsController = @(
    @('52927 => "Este inmueble ya no está en estado Borrador.",','52927 => "Esta propiedad ya no está en un estado editable.",'),
    @('53230 => "El borrador ya tiene el máximo de 20 fotos.",','53230 => "La propiedad ya tiene el máximo de 20 fotos.",')
)
foreach ($pair in $replacementsController) {
    if (-not $controller.Contains($pair[0])) { throw "Controller text not found: $($pair[0])" }
    $controller = $controller.Replace($pair[0], $pair[1])
}

$replacementsView = @(
    @("setUploadState(true, 'Guardando cambios del borrador...');","setUploadState(true, 'Guardando cambios de la propiedad...');"),
    @("throw new Error('No fue posible identificar el formulario del borrador.');","throw new Error('No fue posible identificar el formulario de la propiedad.');"),
    @("throw new Error(data?.message || 'No fue posible guardar los cambios antes de subir las fotos.');","throw new Error(data?.message || 'No fue posible guardar los cambios de la propiedad antes de subir las fotos.');")
)
foreach ($pair in $replacementsView) {
    if (-not $view.Contains($pair[0])) { throw "View text not found: $($pair[0])" }
    $view = $view.Replace($pair[0], $pair[1])
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)
[System.IO.File]::WriteAllText($viewPath, $view, $utf8NoBom)

Write-Host 'RSMaps owned property editing text cleanup applied.'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Changed: Views/Borrador/Editar.cshtml'
