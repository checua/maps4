/* ============================================================
   RSMaps 2.0 - Paso 22A
   HABILITAR TEMPORALMENTE VISTA ADMINISTRADOR PARA PRUEBA UI

   IMPORTANTE:
   - Este script CAMBIA temporalmente una membresia real de ASESOR a
     ADMINISTRADOR para permitir una prueba end-to-end desde otra sesion.
   - Solo actua sobre el correo configurado abajo.
   - Se niega a continuar si el rol original no es ASESOR.
   - Al terminar la prueba debe ejecutarse Paso 22B.
   - No modifica inmuebles, operaciones, usuarios ni contrasenas.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @CorreoPrueba varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdAsesor int;
DECLARE @IdCuenta int;
DECLARE @RolActual varchar(30);
DECLARE @MembresiasActivas int;
DECLARE @Propios int;
DECLARE @CuentaTotal int;

SELECT @IdAsesor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoPrueba;

IF @IdAsesor IS NULL
    THROW 52201, 'No existe el usuario configurado para la prueba.', 1;

SELECT @MembresiasActivas = COUNT(*)
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1;

IF @MembresiasActivas <> 1
    THROW 52202, 'La prueba requiere exactamente una membresia activa para este usuario.', 1;

SELECT TOP (1)
    @IdCuenta = cu.IdCuenta,
    @RolActual = cu.RolCodigo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @RolActual <> 'ASESOR'
    THROW 52203, 'El usuario no esta actualmente como ASESOR. No se aplico ningun cambio.', 1;

SELECT @Propios = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE IdCuenta = @IdCuenta
  AND idAsesor = @IdAsesor;

SELECT @CuentaTotal = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE IdCuenta = @IdCuenta;

IF @CuentaTotal <= @Propios
    THROW 52204, 'La cuenta no tiene inventario de otros asesores para demostrar la vista de equipo.', 1;

BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_CuentaUsuario
SET RolCodigo = 'ADMINISTRADOR'
WHERE IdCuenta = @IdCuenta
  AND IdAsesor = @IdAsesor
  AND RolCodigo = 'ASESOR'
  AND Activo = 1;

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 52205, 'No se pudo aplicar de forma segura el rol temporal.', 1;
END;

COMMIT TRANSACTION;

SELECT
    @IdCuenta AS IdCuenta,
    @IdAsesor AS IdAsesor,
    @CorreoPrueba AS Correo,
    'ASESOR' AS RolAnterior,
    cu.RolCodigo AS RolTemporal,
    @Propios AS InmueblesPropios,
    @CuentaTotal AS InmueblesCuenta,
    CASE WHEN cu.RolCodigo = 'ADMINISTRADOR'
         THEN 'OK - ADMINISTRADOR TEMPORAL HABILITADO PARA PRUEBA UI'
         ELSE 'REVISAR'
    END AS EstadoPaso22A
FROM dbo.RSMAPS_CuentaUsuario cu
WHERE cu.IdCuenta = @IdCuenta
  AND cu.IdAsesor = @IdAsesor;

SELECT
    'ABRIR UNA VENTANA DE INCOGNITO, INICIAR SESION Y PROBAR /Inventario. NO REFRESCAR LA SESION NORMAL. AL TERMINAR EJECUTAR 22B.' AS SiguientePaso;
