/* ============================================================
   RSMaps 2.0 - Paso 03
   RESOLVER CUENTA EN ALTAS Y EDICIONES DE INMUEBLES

   Base esperada: mapsMarkers

   Objetivo:
   - Hacer que RSMAPS_sp_insertar_coordenadas determine IdCuenta desde
     la membresia activa/predeterminada del usuario.
   - Dejar de confiar en el valor legacy @idInmobiliaria enviado por
     la aplicacion (actualmente el codigo .NET envia 1 de forma fija).
   - Mantener @idInmobiliaria temporalmente en la firma para no romper
     el codigo .NET actual durante la migracion.
   - Conservar idInmobiliaria legacy sincronizado cuando la Cuenta
     provenga de una inmobiliaria antigua.

   IMPORTANTE:
   - NO inserta ni elimina inmuebles durante la migracion.
   - NO cambia la firma que consume actualmente la aplicacion.
   - El parametro @idInmobiliaria queda LEGACY y se ignora para decidir
     la cuenta real del inmueble.
   - El siguiente paso retirara el hardcode idInmobiliaria = 1 del C#.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Validar prerrequisitos
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
        THROW 50301, 'No existe dbo.RSMAPS_Cuenta. Ejecutar primero Paso 01.', 1;

    IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
        THROW 50302, 'No existe dbo.RSMAPS_CuentaUsuario. Ejecutar primero Paso 01.', 1;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'IdCuenta') IS NULL
        THROW 50303, 'RSMAPS_Inmueble no tiene IdCuenta. Ejecutar primero Paso 02.', 1;

    /* Todas las propiedades activas deben tener su cuenta asignada. */
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE IdCuenta IS NULL)
        THROW 50304, 'Existen inmuebles activos sin IdCuenta. Revisar Paso 02.', 1;

    /* El asesor de cada inmueble debe pertenecer a la misma cuenta. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        LEFT JOIN dbo.RSMAPS_CuentaUsuario cu
            ON cu.IdCuenta = i.IdCuenta
           AND cu.IdAsesor = i.idAsesor
           AND cu.Activo = 1
        WHERE cu.IdAsesor IS NULL
    )
        THROW 50305, 'Hay inmuebles cuyo asesor no pertenece a la Cuenta del inmueble.', 1;

    /* ------------------------------------------------------------
       2. Garantizar una sola cuenta predeterminada activa por usuario
       ------------------------------------------------------------ */
    IF EXISTS
    (
        SELECT IdAsesor
        FROM dbo.RSMAPS_CuentaUsuario
        WHERE Activo = 1
          AND EsPredeterminada = 1
        GROUP BY IdAsesor
        HAVING COUNT(*) > 1
    )
        THROW 50306, 'Hay usuarios con mas de una Cuenta predeterminada activa.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario')
          AND name = N'UX_RSMAPS_CuentaUsuario_Predeterminada'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_CuentaUsuario_Predeterminada
            ON dbo.RSMAPS_CuentaUsuario(IdAsesor)
            WHERE Activo = 1 AND EsPredeterminada = 1;
    END;

    /* ------------------------------------------------------------
       3. Actualizar el procedimiento manteniendo compatibilidad

       Se usa SQL dinamico para que ALTER PROCEDURE pueda compilarse
       de forma independiente dentro de este script de migracion.
       ------------------------------------------------------------ */
    DECLARE @sql nvarchar(max) = N'
ALTER PROCEDURE [dbo].[RSMAPS_sp_insertar_coordenadas]
    @idInmueble INT = NULL OUTPUT,
    @correo VARCHAR(MAX),
    @idInmobiliaria INT = NULL, -- LEGACY: se conserva temporalmente por compatibilidad; no decide la cuenta
    @lat DECIMAL(10, 6) = NULL,
    @lng DECIMAL(10, 6) = NULL,
    @idTipo INT,
    @terreno FLOAT,
    @construccion FLOAT,
    @precio FLOAT,
    @observaciones VARCHAR(MAX) = NULL,
    @contacto VARCHAR(MAX) = NULL,
    @numImagenes INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @telefono VARCHAR(MAX);
    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @idInmobiliariaCuenta INT;
    DECLARE @MembresiasActivas INT;

    /* 1. Resolver usuario por correo. */
    SELECT
        @idAsesor = u.idAsesor,
        @telefono = u.telefono
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 50320, ''No existe un usuario RSMaps con el correo indicado.'', 1;

    /* 2. Preferir la cuenta activa marcada como predeterminada. */
    SELECT TOP (1)
        @idCuenta = cu.IdCuenta,
        @idInmobiliariaCuenta = c.IdInmobiliariaLegacy
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @idAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    /* 3. Compatibilidad futura: si no hay predeterminada, aceptar una
          unica membresia activa; con varias, exigir seleccion explicita
          antes de permitir escribir. */
    IF @idCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c
            ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @idAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @idCuenta = cu.IdCuenta,
                @idInmobiliariaCuenta = c.IdInmobiliariaLegacy
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c
                ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @idAsesor
              AND cu.Activo = 1
              AND c.Activo = 1;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 50321, ''El usuario no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 50322, ''El usuario pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    /* 4. Alta. */
    IF @idInmueble IS NULL
       OR NOT EXISTS
          (
              SELECT 1
              FROM dbo.RSMAPS_Inmueble
              WHERE idInmueble = @idInmueble
          )
    BEGIN
        INSERT INTO dbo.RSMAPS_Inmueble
        (
            idInmobiliaria,
            idAsesor,
            direccion,
            lat,
            lng,
            idTipo,
            telefono,
            terreno,
            construccion,
            precio,
            observaciones,
            exclusiva,
            link,
            contacto_a,
            IdCuenta
        )
        VALUES
        (
            @idInmobiliariaCuenta,
            @idAsesor,
            ''Dirección'',
            @lat,
            @lng,
            @idTipo,
            @telefono,
            @terreno,
            @construccion,
            @precio,
            @observaciones,
            1,
            ''link'',
            @contacto,
            @idCuenta
        );

        SET @idInmueble = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
        VALUES (@idInmueble, @numImagenes);
    END
    ELSE
    BEGIN
        /* No permitir que una edicion cambie silenciosamente un inmueble
           de una Cuenta a otra. */
        IF EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @idInmueble
              AND IdCuenta IS NOT NULL
              AND IdCuenta <> @idCuenta
        )
            THROW 50323, ''El inmueble pertenece a una Cuenta diferente a la del usuario.'', 1;

        UPDATE dbo.RSMAPS_Inmueble
        SET
            IdCuenta = @idCuenta,
            idInmobiliaria = @idInmobiliariaCuenta,
            idAsesor = @idAsesor,
            direccion = ''Dirección'',
            idTipo = @idTipo,
            telefono = @telefono,
            terreno = @terreno,
            construccion = @construccion,
            precio = @precio,
            observaciones = @observaciones,
            exclusiva = 1,
            link = ''link'',
            contacto_a = @contacto
        WHERE idInmueble = @idInmueble;

        /* El manejo moderno de imagenes se migrara en una fase posterior. */
    END;

    SELECT @idInmueble AS idInmueble;
END;
';

    EXEC sys.sp_executesql @sql;

    COMMIT TRANSACTION;

    /* ------------------------------------------------------------
       4. Validacion final - no modifica datos
       ------------------------------------------------------------ */
    SELECT
        COUNT(*) AS TotalInmuebles,
        SUM(CASE WHEN IdCuenta IS NOT NULL THEN 1 ELSE 0 END) AS ConCuenta,
        SUM(CASE WHEN IdCuenta IS NULL THEN 1 ELSE 0 END) AS SinCuenta
    FROM dbo.RSMAPS_Inmueble;

    SELECT
        COUNT(*) AS InmueblesConAsesorFueraDeCuenta
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdCuenta = i.IdCuenta
       AND cu.IdAsesor = i.idAsesor
       AND cu.Activo = 1
    WHERE cu.IdAsesor IS NULL;

    SELECT
        u.idAsesor,
        u.correo,
        cu.IdCuenta,
        c.Nombre AS Cuenta,
        c.TipoCuenta,
        c.IdInmobiliariaLegacy,
        cu.RolCodigo,
        cu.EsPredeterminada
    FROM dbo.RSMAPS_Usuario u
    INNER JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdAsesor = u.idAsesor
       AND cu.Activo = 1
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
       AND c.Activo = 1
    WHERE cu.EsPredeterminada = 1
    ORDER BY u.idAsesor;

    SELECT
        p.name AS Procedimiento,
        prm.parameter_id,
        prm.name AS Parametro,
        TYPE_NAME(prm.user_type_id) AS TipoDato,
        prm.is_output AS EsOutput
    FROM sys.procedures p
    INNER JOIN sys.parameters prm
        ON prm.object_id = p.object_id
    WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_insertar_coordenadas')
    ORDER BY prm.parameter_id;

    PRINT 'Paso 03 RSMaps 2.0 terminado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
