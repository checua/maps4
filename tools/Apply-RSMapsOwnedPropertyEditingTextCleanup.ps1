$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$controllerPath = Join-Path $repoRoot 'Controllers\BorradorController.cs'
$viewPath = Join-Path $repoRoot 'Views\Borrador\Editar.cshtml'

foreach ($path in @($controllerPath, $viewPath)) {
    if (-not (Test-Path $path)) { throw "Required file not found: $path" }
}

$controller = [System.IO.File]::ReadAllText($controllerPath)
$view = [System.IO.File]::ReadAllText($viewPath)

# Keep this helper ASCII-only so Windows PowerShell 5.1 does not corrupt source literals.
# Match the C# switch arms by numeric error code instead of accented text.
# The same SQL error code can legitimately appear in more than one controller switch,
# so replace every matching arm instead of requiring exactly one occurrence.
$controllerPatterns = @(
    @('(?m)^\s*52927\s*=>.*$', '                52927 => "Esta propiedad ya no est\u00e1 en un estado editable.",'),
    @('(?m)^\s*53230\s*=>.*$', '                53230 => "La propiedad ya tiene el m\u00e1ximo de 20 fotos.",')
)
foreach ($pair in $controllerPatterns) {
    $matches = [regex]::Matches($controller, $pair[0])
    if ($matches.Count -lt 1) { throw "Expected at least one controller match for pattern $($pair[0]); found 0. No files were changed." }
    Write-Host "Controller matches for $($pair[0]): $($matches.Count)"
    $controller = [regex]::Replace($controller, $pair[0], $pair[1])
}

$replacementsView = @(
    @("setUploadState(true, 'Guardando cambios del borrador...');","setUploadState(true, 'Guardando cambios de la propiedad...');"),
    @("throw new Error('No fue posible identificar el formulario del borrador.');","throw new Error('No fue posible identificar el formulario de la propiedad.');"),
    @("throw new Error(data?.message || 'No fue posible guardar los cambios antes de subir las fotos.');","throw new Error(data?.message || 'No fue posible guardar los cambios de la propiedad antes de subir las fotos.');")
)
foreach ($pair in $replacementsView) {
    if (-not $view.Contains($pair[0])) { throw "View text not found: $($pair[0]). No files were changed." }
    $view = $view.Replace($pair[0], $pair[1])
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)
[System.IO.File]::WriteAllText($viewPath, $view, $utf8NoBom)

Write-Host 'RSMaps owned property editing text cleanup applied.'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Changed: Views/Borrador/Editar.cshtml'
