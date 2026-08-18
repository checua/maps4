/* ============================================================
   RSMaps 2.0 - Paso 08
   REGISTRO PUBLICO COMO CUENTA INDIVIDUAL

   Base esperada: mapsMarkers

   Objetivo:
   - El registro publico ya NO asigna automaticamente DNHoldings.
   - Cada usuario nuevo crea su propia Cuenta tipo INDIVIDUAL.
   - El usuario queda como PROPIETARIO y con esa Cuenta predeterminada.
   - Mantener compatibilidad temporal con el parametro legacy
     @idInmobiliaria, pero ignorarlo como fuente de afiliacion.
   - Alinear el login para aceptar correos de hasta 200 caracteres.

   IMPORTANTE:
   - NO modifica usuarios existentes.
   - NO mueve los 4 usuarios legacy que siguen sin Cuenta.
   - Incluye una prueba transaccional con ROLLBACK que no deja datos.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50800, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 50801, 'No existe dbo.RSMAPS_Usuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    THROW 50802, 'No existe dbo.RSMAPS_Cuenta.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 50803, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'PROPIETARIO' AND Activo = 1)
    THROW 50804, 'No existe el rol PROPIETARIO activo.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Registro publico: Usuario + Cuenta INDIVIDUAL + Membresia
       ------------------------------------------------------------ */
    DECLARE @sqlGuardar nvarchar(max) = N'
ALTER PROCEDURE [dbo].[RSMAPS_sp_GuardarUsuario]
(
    @nombres varchar(max),
    @aPaterno varchar(max),
    @aMaterno varchar(max),
    @idInmobiliaria int = NULL, -- LEGACY: se conserva por compatibilidad, no se usa para afiliacion publica
    @nick varchar(max),
    @contra varchar(max),
    @telefono varchar(max),
    @correo varchar(200),
    @foto varchar(max),
    @obs varchar(max),
    @dob varchar(max),
    @revisado int
)
AS
BEGIN
    SET XACT_ABORT ON;
    SET DATEFORMAT dmy;

    DECLARE @IdAsesorNuevo int;
    DECLARE @IdCuentaNueva int;
    DECLARE @NombreCuenta nvarchar(200);
    DECLARE @Slug varchar(200);

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NULLIF(LTRIM(RTRIM(@correo)), '''') IS NULL
            THROW 50810, ''El correo es obligatorio.'', 1;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Usuario
            WHERE correo = @correo
        )
            THROW 50811, ''Este correo ya ha sido registrado.'', 1;

        INSERT dbo.RSMAPS_Usuario
        (
            nombres,
            aPaterno,
            aMaterno,
            idInmobiliaria,
            nick,
            contra,
            telefono,
            correo,
            foto,
            obs,
            dob,
            revisado
        )
        VALUES
        (
            @nombres,
            @aPaterno,
            NULLIF(@aMaterno, ''''),
            NULL,
            @nick,
            @contra,
            @telefono,
            @correo,
            @foto,
            @obs,
            CONVERT(date, @dob),
            @revisado
        );

        SET @IdAsesorNuevo = CONVERT(int, SCOPE_IDENTITY());

        SET @NombreCuenta = LEFT(
            LTRIM(RTRIM(CONCAT(COALESCE(@nombres, ''''), '' '', COALESCE(@aPaterno, '''')))),
            200
        );

        IF NULLIF(@NombreCuenta, N'''') IS NULL
            SET @NombreCuenta = LEFT(CONVERT(nvarchar(200), @correo), 200);

        SET @Slug = CONCAT(''asesor-'', CONVERT(varchar(20), @IdAsesorNuevo));

        INSERT dbo.RSMAPS_Cuenta
        (
            Nombre,
            TipoCuenta,
            Slug,
            IdInmobiliariaLegacy,
            Activo
        )
        VALUES
        (
            @NombreCuenta,
            ''INDIVIDUAL'',
            @Slug,
            NULL,
            1
        );

        SET @IdCuentaNueva = CONVERT(int, SCOPE_IDENTITY());

        INSERT dbo.RSMAPS_CuentaUsuario
        (
            IdCuenta,
            IdAsesor,
            RolCodigo,
            Activo,
            EsPredeterminada
        )
        VALUES
        (
            @IdCuentaNueva,
            @IdAsesorNuevo,
            ''PROPIETARIO'',
            1,
            1
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
';

    EXEC sys.sp_executesql @sqlGuardar;

    /* ------------------------------------------------------------
       2. Login: alinear correo con RSMAPS_Usuario.correo varchar(200)
       ------------------------------------------------------------ */
    DECLARE @sqlLogin nvarchar(max) = N'
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

    EXEC sys.sp_executesql @sqlLogin;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   3. PRUEBA CONTROLADA CON ROLLBACK
   ============================================================ */
DECLARE @CorreoPrueba varchar(200) = 'rsmaps.paso08.rollback@test.local';
DECLARE @UsuariosAntes int = (SELECT COUNT(*) FROM dbo.RSMAPS_Usuario);
DECLARE @CuentasAntes int = (SELECT COUNT(*) FROM dbo.RSMAPS_Cuenta);
DECLARE @MembresiasAntes int = (SELECT COUNT(*) FROM dbo.RSMAPS_CuentaUsuario);

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Usuario WHERE correo = @CorreoPrueba)
    THROW 50820, 'Existe el correo reservado para la prueba del Paso 08.', 1;

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_GuardarUsuario
    @nombres = 'Prueba',
    @aPaterno = 'Paso08',
    @aMaterno = '',
    @idInmobiliaria = 999999,
    @nick = 'prueba',
    @contra = 'HASH_PRUEBA_NO_REAL',
    @telefono = '0000000000',
    @correo = @CorreoPrueba,
    @foto = '',
    @obs = 'PRUEBA ROLLBACK PASO 08',
    @dob = '18/08/2026',
    @revisado = 1;

SELECT
    u.idAsesor,
    u.correo,
    u.idInmobiliaria AS InmobiliariaLegacyUsuario,
    c.IdCuenta,
    c.Nombre AS Cuenta,
    c.TipoCuenta,
    c.IdInmobiliariaLegacy,
    cu.RolCodigo,
    cu.Activo,
    cu.EsPredeterminada,
    CASE
        WHEN u.idInmobiliaria IS NULL
         AND c.TipoCuenta = 'INDIVIDUAL'
         AND c.IdInmobiliariaLegacy IS NULL
         AND cu.RolCodigo = 'PROPIETARIO'
         AND cu.Activo = 1
         AND cu.EsPredeterminada = 1
        THEN 'OK - CUENTA INDIVIDUAL CREADA'
        ELSE 'REVISAR'
    END AS EstadoPrueba
FROM dbo.RSMAPS_Usuario u
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = u.idAsesor
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = cu.IdCuenta
WHERE u.correo = @CorreoPrueba;

ROLLBACK TRANSACTION;

SELECT
    @UsuariosAntes AS UsuariosAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Usuario) AS UsuariosDespues,
    @CuentasAntes AS CuentasAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Cuenta) AS CuentasDespues,
    @MembresiasAntes AS MembresiasAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_CuentaUsuario) AS MembresiasDespues,
    CASE
        WHEN @UsuariosAntes = (SELECT COUNT(*) FROM dbo.RSMAPS_Usuario)
         AND @CuentasAntes = (SELECT COUNT(*) FROM dbo.RSMAPS_Cuenta)
         AND @MembresiasAntes = (SELECT COUNT(*) FROM dbo.RSMAPS_CuentaUsuario)
         AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Usuario WHERE correo = @CorreoPrueba)
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

SELECT
    p.name AS Parametro,
    TYPE_NAME(p.user_type_id) AS TipoDato,
    p.max_length AS LongitudBytes,
    p.has_default_value AS TieneDefault
FROM sys.parameters p
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_GuardarUsuario')
ORDER BY p.parameter_id;

PRINT 'Paso 08 RSMaps 2.0 terminado correctamente.';
