/* ============================================================
   RSMaps 2.0 - Paso 22B
   RESTAURAR ROL DESPUES DE PRUEBA UI ADMINISTRADOR

   IMPORTANTE:
   - Restaura SOLO al usuario configurado abajo.
   - Se niega a continuar si el rol actual no es ADMINISTRADOR.
   - No toca ningun otro usuario ni dato comercial.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52250, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @CorreoPrueba varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdAsesor int;
DECLARE @IdCuenta int;
DECLARE @RolActual varchar(30);
DECLARE @MembresiasActivas int;

SELECT @IdAsesor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoPrueba;

IF @IdAsesor IS NULL
    THROW 52251, 'No existe el usuario configurado para restaurar.', 1;

SELECT @MembresiasActivas = COUNT(*)
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1;

IF @MembresiasActivas <> 1
    THROW 52252, 'La restauracion requiere exactamente una membresia activa para este usuario.', 1;

SELECT TOP (1)
    @IdCuenta = cu.IdCuenta,
    @RolActual = cu.RolCodigo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @RolActual <> 'ADMINISTRADOR'
    THROW 52253, 'El usuario no esta en ADMINISTRADOR temporal. No se aplico ningun cambio.', 1;

BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_CuentaUsuario
SET RolCodigo = 'ASESOR'
WHERE IdCuenta = @IdCuenta
  AND IdAsesor = @IdAsesor
  AND RolCodigo = 'ADMINISTRADOR'
  AND Activo = 1;

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 52254, 'No se pudo restaurar el rol de forma segura.', 1;
END;

COMMIT TRANSACTION;

SELECT
    @IdCuenta AS IdCuenta,
    @IdAsesor AS IdAsesor,
    @CorreoPrueba AS Correo,
    'ADMINISTRADOR' AS RolTemporalAnterior,
    cu.RolCodigo AS RolRestaurado,
    CASE WHEN cu.RolCodigo = 'ASESOR'
         THEN 'OK - ROL ASESOR RESTAURADO'
         ELSE 'REVISAR'
    END AS EstadoPaso22B
FROM dbo.RSMAPS_CuentaUsuario cu
WHERE cu.IdCuenta = @IdCuenta
  AND cu.IdAsesor = @IdAsesor;

SELECT
    'CERRAR LA VENTANA DE INCOGNITO DE PRUEBA. LA SESION NORMAL CONSERVA SU COOKIE ASESOR.' AS SiguientePaso;
