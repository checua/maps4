$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $repoRoot 'sql\RSMaps2\46_edit_owned_active_properties.sql'
$controllerPath = Join-Path $repoRoot 'Controllers\BorradorController.cs'

foreach ($path in @($sqlPath, $controllerPath)) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }
}

$sql = [System.IO.File]::ReadAllText($sqlPath)
$controller = [System.IO.File]::ReadAllText($controllerPath)

# Fail closed when a user has multiple active account memberships and none is default.
# Step 46 must not silently choose the lowest account id.
$accountPattern = 'SELECT TOP\(1\)\s+@cuenta=cu\.IdCuenta,@rol=cu\.RolCodigo\s+FROM dbo\.RSMAPS_CuentaUsuario cu\s+JOIN dbo\.RSMAPS_Cuenta c ON c\.IdCuenta=cu\.IdCuenta\s+WHERE cu\.IdAsesor=@actor AND cu\.Activo=1 AND c\.Activo=1\s+ORDER BY cu\.EsPredeterminada DESC,cu\.IdCuenta;'

$accountReplacement = @'
SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 AND cu.EsPredeterminada=1
    ORDER BY cu.IdCuenta;

    IF @cuenta IS NULL
       AND (SELECT COUNT(*)
            FROM dbo.RSMAPS_CuentaUsuario cu
            JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
            WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1)=1
    BEGIN
        SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
        FROM dbo.RSMAPS_CuentaUsuario cu
        JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
        WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
        ORDER BY cu.IdCuenta;
    END;
'@

$matches = [regex]::Matches($sql, $accountPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
if ($matches.Count -lt 6) {
    throw "Expected at least 6 account-resolution blocks in step 46, found $($matches.Count). No files were changed."
}
$sql = [regex]::Replace($sql, $accountPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $accountReplacement }, [System.Text.RegularExpressions.RegexOptions]::Singleline)

# State-neutral user messages for the expanded editor.
$controllerReplacements = @(
    @('return Unauthorized(new { success = false, message = "Tu sesion termino o el borrador no es valido." });','return Unauthorized(new { success = false, message = "Tu sesion termino o la propiedad no es valida." });'),
    @('return BadRequest(new { success = false, message = "Revisa los datos del borrador antes de subir las fotos." });','return BadRequest(new { success = false, message = "Revisa los datos de la propiedad antes de subir las fotos." });'),
    @('return NotFound(new { success = false, message = "El borrador ya no existe." });','return NotFound(new { success = false, message = "La propiedad ya no existe." });'),
    @('52923 => "Tu rol actual no tiene permiso para editar borradores.",','52923 => "Tu rol actual no tiene permiso para editar esta propiedad.",'),
    @('52924 => "El borrador ya no existe.",','52924 => "La propiedad ya no existe.",'),
    @('52925 => "El borrador pertenece a otra cuenta.",','52925 => "La propiedad pertenece a otra cuenta.",'),
    @('52926 => "Por ahora solo el asesor responsable puede completar este borrador.",','52926 => "Solo el asesor responsable puede editar esta propiedad.",'),
    @('52927 => "Este inmueble ya no está en estado Borrador.",','52927 => "Esta propiedad ya no está en un estado editable.",'),
    @('_ => "No fue posible guardar el borrador. Intenta nuevamente."','_ => "No fue posible guardar la propiedad. Intenta nuevamente."'),
    @('53228 or 53251 or 53261 => "Estas fotos solo pueden modificarse mientras el inmueble sea borrador.",','53228 or 53251 or 53261 => "Las fotos no pueden modificarse en el estado actual de la propiedad.",'),
    @('53229 => "Tu rol actual no puede editar fotos del borrador.",','53229 => "Tu rol actual no puede editar fotos de esta propiedad.",'),
    @('53230 => "El borrador ya tiene el máximo de 20 fotos.",','53230 => "La propiedad ya tiene el máximo de 20 fotos.",'),
    @('53312 or 53323 => "La foto o el borrador ya no existen.",','53312 or 53323 => "La foto o la propiedad ya no existen.",'),
    @('53322 => "El orden solo puede cambiarse mientras el inmueble sea borrador.",','53322 => "El orden de las fotos no puede cambiarse en el estado actual de la propiedad.",')
)

foreach ($pair in $controllerReplacements) {
    if ($controller.Contains($pair[0])) {
        $controller = $controller.Replace($pair[0], $pair[1])
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($sqlPath, $sql, $utf8NoBom)
[System.IO.File]::WriteAllText($controllerPath, $controller, $utf8NoBom)

Write-Host 'RSMaps owned property editing safety patch applied.'
Write-Host "Account-resolution blocks hardened: $($matches.Count)"
Write-Host 'Changed: sql/RSMaps2/46_edit_owned_active_properties.sql'
Write-Host 'Changed: Controllers/BorradorController.cs'
Write-Host 'Behavior: multiple-account users without a default account now fail closed instead of silently selecting one.'