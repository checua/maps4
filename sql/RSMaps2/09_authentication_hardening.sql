/* ============================================================
   RSMaps 2.0 - Paso 09
   ENDURECER LOGIN PARA WEB Y FUTURA API/MOBILE

   Base esperada: mapsMarkers

   Objetivo:
   - Mantener autenticacion por correo + hash en la BD durante esta fase.
   - Dejar de devolver el hash de contraseña en el result set del login.
   - Conservar el contexto multi-tenant: IdCuenta, TipoCuenta y RolCodigo.
   - Mantener correo con longitud varchar(200).

   IMPORTANTE:
   - NO modifica contraseñas ni usuarios.
   - NO cambia la forma de almacenamiento del hash todavía.
   - El cambio HTTP GET -> POST se realiza en la capa .NET/JavaScript.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50900, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 50901, 'No existe dbo.RSMAPS_Usuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    THROW 50902, 'No existe dbo.RSMAPS_Cuenta.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 50903, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @sql nvarchar(max) = N'
ALTER PROCEDURE [dbo].[RSMAPS_sp_ListaUsuarioLogin]
(
    @correo varchar(200),
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
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Validacion de contrato del result set sin usar credenciales reales. */
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

IF EXISTS
(
    SELECT 1
    FROM sys.dm_exec_describe_first_result_set_for_object
    (
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaUsuarioLogin'),
        NULL
    )
    WHERE name = N'contra'
)
    THROW 50910, 'El login todavía expone la columna contra.', 1;

SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM sys.dm_exec_describe_first_result_set_for_object
            (
                OBJECT_ID(N'dbo.RSMAPS_sp_ListaUsuarioLogin'),
                NULL
            )
            WHERE name = N'IdCuenta'
        )
        AND EXISTS
        (
            SELECT 1
            FROM sys.dm_exec_describe_first_result_set_for_object
            (
                OBJECT_ID(N'dbo.RSMAPS_sp_ListaUsuarioLogin'),
                NULL
            )
            WHERE name = N'RolCodigo'
        )
        THEN 'OK - LOGIN SIN HASH Y CON CONTEXTO DE CUENTA'
        ELSE 'REVISAR'
    END AS EstadoPaso09;

PRINT 'Paso 09 RSMaps 2.0 terminado correctamente.';
