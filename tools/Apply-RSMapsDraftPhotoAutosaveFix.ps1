$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$controllerPath = Join-Path $repoRoot 'Controllers\BorradorController.cs'
$viewPath = Join-Path $repoRoot 'Views\Borrador\Editar.cshtml'

foreach ($path in @($controllerPath, $viewPath)) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }
}

$controller = [System.IO.File]::ReadAllText($controllerPath)
$view = [System.IO.File]::ReadAllText($viewPath)

$controllerAnchor = @'
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Descartar(int idInmueble, CancellationToken cancellationToken)
'@

$controllerInsert = @'
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarAntesDeFotos(BorradorEdicionViewModel modelo)
        {
            string? correo = User.Identity?.Name;
            if (modelo.IdInmueble <= 0 || string.IsNullOrWhiteSpace(correo))
                return Unauthorized(new { success = false, message = "Tu sesion termino o el borrador no es valido." });

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Revisa los datos del borrador antes de subir las fotos." });

            try
            {
                await _borradorRepository.GuardarAsync(correo, modelo);
                return Ok(new { success = true });
            }
            catch (SqlException ex) when (ex.Number == 52924)
            {
                return NotFound(new { success = false, message = "El borrador ya no existe." });
            }
            catch (SqlException ex)
            {
                return BadRequest(new { success = false, message = MensajeEdicionSeguro(ex.Number) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Descartar(int idInmueble, CancellationToken cancellationToken)
'@

if (-not $controller.Contains($controllerAnchor)) {
    throw 'Controller anchor was not found. No files were changed.'
}

$controller = $controller.Replace($controllerAnchor, $controllerInsert)

$viewOldToken = @'
    const progress = document.getElementById('photoUploadProgress');
    const token = document.querySelector('#draftEditForm input[name="__RequestVerificationToken"]')?.value || '';
'@

$viewNewToken = @'
    const progress = document.getElementById('photoUploadProgress');
    const form = document.getElementById('draftEditForm');
    const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
'@

if (-not $view.Contains($viewOldToken)) {
    throw 'View token block was not found. No files were changed.'
}
$view = $view.Replace($viewOldToken, $viewNewToken)

$viewOldStart = @'
        setUploadState(true, `Preparando ${files.length} foto${files.length === 1 ? '' : 's'}…`);

        try {
            for (let i = 0; i < files.length; i++) {
'@

$viewNewStart = @'
        setUploadState(true, 'Guardando cambios del borrador...');

        try {
            await saveDraftBeforePhotoUpload();

            for (let i = 0; i < files.length; i++) {
'@

if (-not $view.Contains($viewOldStart)) {
    throw 'View upload-start block was not found. No files were changed.'
}
$view = $view.Replace($viewOldStart, $viewNewStart)

$viewUploadAnchor = @'
    async function uploadPhoto(file) {
'@

$viewUploadInsert = @'
    async function saveDraftBeforePhotoUpload() {
        if (!form)
            throw new Error('No fue posible identificar el formulario del borrador.');

        const formData = new FormData(form);
        const response = await fetch('@Url.Action("GuardarAntesDeFotos", "Borrador")', {
            method: 'POST',
            credentials: 'same-origin',
            body: formData
        });

        let data = null;
        try { data = await response.json(); } catch (_) { }

        if (!response.ok || !data?.success)
            throw new Error(data?.message || 'No fue posible guardar los cambios antes de subir las fotos.');
    }

    async function uploadPhoto(file) {
'@

if (-not $view.Contains($viewUploadAnchor)) {
    throw 'View upload function anchor was not found. No files were changed.'
}
$view = $view.Replace($viewUploadAnchor, $viewUploadInsert)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)
[System.IO.File]::WriteAllText($viewPath, $view, $utf8NoBom)

Write-Host 'RSMaps draft photo autosave patch applied.'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Changed: Views/Borrador/Editar.cshtml'
Write-Host 'Behavior: draft data is saved before photo uploads, then photos are uploaded and the page may reload safely.'
