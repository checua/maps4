/* ============================================================
   RSMaps 2.0 - Paso 07
   LOGIN CON CONTEXTO DE CUENTA Y ROL

   Base esperada: mapsMarkers

   Objetivo:
   - Hacer que RSMAPS_sp_ListaUsuarioLogin resuelva la Cuenta activa
     y predeterminada del usuario desde RSMAPS_CuentaUsuario.
   - Mantener las columnas legacy actuales para no romper todavía
     UsuarioRepositoryLogin ni InicioController.
   - Agregar al resultado IdCuenta, CuentaNombre, TipoCuenta y RolCodigo.

   IMPORTANTE:
   - NO modifica usuarios ni contraseñas.
   - NO cambia los parámetros del procedimiento.
   - NO modifica el código .NET todavía.
   - Conserva u.contra temporalmente en el resultado por compatibilidad;
     se retirará cuando actualicemos el repositorio C#.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
        THROW 50701, 'No existe dbo.RSMAPS_Usuario.', 1;

    IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
        THROW 50702, 'No existe dbo.RSMAPS_Cuenta. Ejecutar primero Paso 01.', 1;

    IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
        THROW 50703, 'No existe dbo.RSMAPS_CuentaUsuario. Ejecutar primero Paso 01.', 1;

    IF EXISTS
    (
        SELECT IdAsesor
        FROM dbo.RSMAPS_CuentaUsuario
        WHERE Activo = 1
          AND EsPredeterminada = 1
        GROUP BY IdAsesor
        HAVING COUNT(*) > 1
    )
        THROW 50704, 'Hay usuarios con más de una Cuenta predeterminada activa.', 1;

    DECLARE @sql nvarchar(max) = N'
ALTER PROCEDURE [dbo].[RSMAPS_sp_ListaUsuarioLogin]
(
    @correo varchar(50),
    @contra varchar(max)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET DATEFORMAT dmy;

    SELECT
        u.idAsesor,
        u.nombres,
        u.aPaterno,
        u.aMaterno,
        i.idInmobiliaria,
        i.nombre,
        u.nick,
        u.contra,
        u.telefono,
        u.correo,
        u.foto,
        u.obs,
        CONVERT(char(10), u.dob, 103) AS fechaNacimiento,
        u.revisado,
        c.IdCuenta,
        c.Nombre AS CuentaNombre,
        c.TipoCuenta,
        cu.RolCodigo
    FROM dbo.RSMAPS_Usuario AS u
    INNER JOIN dbo.RSMAPS_CuentaUsuario AS cu
        ON cu.IdAsesor = u.idAsesor
       AND cu.Activo = 1
       AND cu.EsPredeterminada = 1
    INNER JOIN dbo.RSMAPS_Cuenta AS c
        ON c.IdCuenta = cu.IdCuenta
       AND c.Activo = 1
    LEFT JOIN dbo.RSMAPS_Inmobiliaria AS i
        ON i.idInmobiliaria = c.IdInmobiliariaLegacy
    WHERE u.correo = @correo
      AND u.contra = @contra;
END;
';

    EXEC sys.sp_executesql @sql;

    COMMIT TRANSACTION;

    /* Validación sin usar credenciales reales. */
    SELECT
        column_ordinal,
        name AS Columna,
        system_type_name AS TipoDato,
        is_nullable AS PermiteNull
    FROM sys.dm_exec_describe_first_result_set_for_object
    (
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaUsuarioLogin'),
        NULL
    )
    WHERE is_hidden = 0
    ORDER BY column_ordinal;

    SELECT
        COUNT(*) AS UsuariosConCuentaPredeterminadaActiva
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
       AND c.Activo = 1
    WHERE cu.Activo = 1
      AND cu.EsPredeterminada = 1;

    SELECT
        COUNT(*) AS UsuariosSinCuentaPredeterminada
    FROM dbo.RSMAPS_Usuario u
    LEFT JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdAsesor = u.idAsesor
       AND cu.Activo = 1
       AND cu.EsPredeterminada = 1
    WHERE cu.IdAsesor IS NULL;

    PRINT 'Paso 07 RSMaps 2.0 terminado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
