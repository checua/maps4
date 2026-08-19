/* ============================================================
   RSMaps 2.0 - Paso 25
   CONTEXTO DE AUTORIZACION ACTUAL PARA UI / API

   Objetivo:
   - Resolver desde SQL la Cuenta, Rol y permisos VIGENTES del usuario.
   - Evitar que la interfaz dependa de claims de rol potencialmente viejos.
   - Servir el mismo contexto a Web y, posteriormente, Android / iOS / API.
   - No cambiar permisos: solo exponer de forma segura los ya definidos.

   Requiere:
   - Paso 21: RSMAPS_Permiso / RSMAPS_RolPermiso
   - Paso 24: permisos comerciales de cuenta activados.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 52501, 'Falta RSMAPS_CuentaUsuario. Ejecutar primero Paso 01.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL
    THROW 52502, 'Falta RSMAPS_Permiso. Ejecutar primero Paso 21.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 52503, 'Falta RSMAPS_RolPermiso. Ejecutar primero Paso 21.', 1;

GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ContextoAutorizacionActual
    @correo VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @CuentaNombre NVARCHAR(200);
    DECLARE @TipoCuenta VARCHAR(20);
    DECLARE @MembresiasActivas INT;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 52520, 'No existe un usuario RSMaps con el correo autenticado.', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo,
        @CuentaNombre = c.Nombre,
        @TipoCuenta = c.TipoCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c
            ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @IdAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @IdCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo,
                @CuentaNombre = c.Nombre,
                @TipoCuenta = c.TipoCuenta
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c
                ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @IdAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 52521, 'El usuario autenticado no pertenece a ninguna cuenta activa.', 1;
        ELSE
            THROW 52522, 'El usuario pertenece a varias cuentas y no tiene una predeterminada.', 1;
    END;

    SELECT
        @IdCuenta AS IdCuenta,
        @CuentaNombre AS CuentaNombre,
        @TipoCuenta AS TipoCuenta,
        @IdAsesor AS IdAsesor,
        @RolCodigo AS RolCodigo,
        CONVERT(bit, CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_RolPermiso rp
            INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
            WHERE rp.RolCodigo = @RolCodigo
              AND rp.PermisoCodigo = 'INVENTARIO_VER_CUENTA'
              AND p.Activo = 1
        ) THEN 1 ELSE 0 END) AS PuedeVerCuenta,
        CONVERT(bit, CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_RolPermiso rp
            INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
            WHERE rp.RolCodigo = @RolCodigo
              AND rp.PermisoCodigo = 'INMUEBLE_CAMBIAR_ESTADO_CUENTA'
              AND p.Activo = 1
        ) THEN 1 ELSE 0 END) AS PuedeCambiarEstadoCuenta,
        CONVERT(bit, CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_RolPermiso rp
            INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
            WHERE rp.RolCodigo = @RolCodigo
              AND rp.PermisoCodigo = 'OPERACION_CERRAR_CUENTA'
              AND p.Activo = 1
        ) THEN 1 ELSE 0 END) AS PuedeCerrarOperacionCuenta,
        CONVERT(bit, CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_RolPermiso rp
            INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
            WHERE rp.RolCodigo = @RolCodigo
              AND rp.PermisoCodigo = 'INMUEBLE_CAPTURAR_PARA_OTRO'
              AND p.Activo = 1
        ) THEN 1 ELSE 0 END) AS PuedeCapturarParaOtro;
END;

GO

/* Validacion estructural y matriz vigente. */
SELECT
    rp.RolCodigo,
    rp.PermisoCodigo
FROM dbo.RSMAPS_RolPermiso rp
WHERE rp.PermisoCodigo IN
(
    'INVENTARIO_VER_CUENTA',
    'INVENTARIO_VER_PROPIO',
    'INMUEBLE_CAMBIAR_ESTADO_CUENTA',
    'OPERACION_CERRAR_CUENTA',
    'INMUEBLE_CAPTURAR_PARA_OTRO'
)
ORDER BY rp.RolCodigo, rp.PermisoCodigo;

SELECT
    OBJECT_ID(N'dbo.RSMAPS_sp_ContextoAutorizacionActual', N'P') AS ProcedimientoId,
    CASE WHEN OBJECT_ID(N'dbo.RSMAPS_sp_ContextoAutorizacionActual', N'P') IS NOT NULL
         THEN 'OK - CONTEXTO DE AUTORIZACION ACTUAL DISPONIBLE'
         ELSE 'REVISAR'
    END AS EstadoPaso25;
