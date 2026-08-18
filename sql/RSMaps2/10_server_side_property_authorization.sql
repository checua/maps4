/* ============================================================
   RSMaps 2.0 - Paso 10
   AUTORIZACION DE ESCRITURA DESDE IDENTIDAD AUTENTICADA

   Base esperada: mapsMarkers

   Objetivo:
   - Mantener el alta de inmuebles resolviendo Cuenta desde el usuario.
   - Impedir que un asesor edite inmuebles de otro asesor, aunque pertenezcan
     a la misma Cuenta, mientras no existan reglas explicitas para roles
     administrativos.
   - Crear un procedimiento seguro de eliminacion que valide Cuenta + asesor
     antes de invocar el procedimiento legacy de eliminacion.
   - Preparar una politica conservadora y portable para Web / Android / iOS.

   IMPORTANTE:
   - PROPIETARIO/ADMINISTRADOR todavia NO reciben excepciones especiales.
     Esa regla se agregara cuando definamos permisos finos por rol.
   - El procedimiento legacy RSMAPS_sp_delete_inmueble se conserva intacto.
   - La prueba final no deja cambios persistentes.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 51000, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51001, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 51002, 'No existe dbo.RSMAPS_Usuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    THROW 51003, 'No existe dbo.RSMAPS_Cuenta.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 51004, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_sp_delete_inmueble', N'P') IS NULL
    THROW 51005, 'No existe dbo.RSMAPS_sp_delete_inmueble legacy.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Alta / edicion: conservar propietario en actualizaciones
       ------------------------------------------------------------ */
    DECLARE @sqlWrite nvarchar(max) = N'
ALTER PROCEDURE [dbo].[RSMAPS_sp_insertar_coordenadas]
    @idInmueble INT = NULL OUTPUT,
    @correo VARCHAR(200),
    @idInmobiliaria INT = NULL,
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

    SELECT
        @idAsesor = u.idAsesor,
        @telefono = u.telefono
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51020, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

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
            THROW 51021, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51022, ''El usuario autenticado pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

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
        IF EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @idInmueble
              AND IdCuenta <> @idCuenta
        )
            THROW 51023, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @idInmueble
              AND idAsesor <> @idAsesor
        )
            THROW 51024, ''El asesor autenticado no es propietario de este inmueble.'', 1;

        UPDATE dbo.RSMAPS_Inmueble
        SET
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
    END;

    SELECT @idInmueble AS idInmueble;
END;
';

    EXEC sys.sp_executesql @sqlWrite;

    /* ------------------------------------------------------------
       2. Eliminacion segura como wrapper del procedimiento legacy
       ------------------------------------------------------------ */
    DECLARE @sqlDelete nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_delete_inmueble_seguro
    @idInmueble INT,
    @correo VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51030, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @idCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @idAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

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
            SELECT TOP (1) @idCuenta = cu.IdCuenta
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c
                ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @idAsesor
              AND cu.Activo = 1
              AND c.Activo = 1;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 51031, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51032, ''El usuario autenticado pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @idAsesorInmueble = i.idAsesor,
        @idCuentaInmueble = i.IdCuenta
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51033, ''El inmueble no existe o ya no esta activo.'', 1;

    IF @idCuentaInmueble <> @idCuenta
        THROW 51034, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

    IF @idAsesorInmueble <> @idAsesor
        THROW 51035, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    EXEC dbo.RSMAPS_sp_delete_inmueble @idInmueble = @idInmueble;
END;
';

    EXEC sys.sp_executesql @sqlDelete;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   3. PRUEBAS CONTROLADAS
   ============================================================ */
DECLARE @IdInmueblePrueba INT;
DECLARE @IdCuentaPrueba INT;
DECLARE @IdAsesorPropietario INT;
DECLARE @CorreoPropietario VARCHAR(200);
DECLARE @IdAsesorOtro INT;
DECLARE @CorreoOtro VARCHAR(200);
DECLARE @IdTipo INT;
DECLARE @Terreno FLOAT;
DECLARE @Construccion FLOAT;
DECLARE @Precio FLOAT;
DECLARE @PrecioPrueba FLOAT;
DECLARE @Observaciones VARCHAR(MAX);
DECLARE @Contacto VARCHAR(MAX);

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdCuentaPrueba = i.IdCuenta,
    @IdAsesorPropietario = i.idAsesor,
    @IdTipo = i.idTipo,
    @Terreno = i.terreno,
    @Construccion = i.construccion,
    @Precio = i.precio,
    @Observaciones = i.observaciones,
    @Contacto = i.contacto_a
FROM dbo.RSMAPS_Inmueble i
WHERE EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_CuentaUsuario cu
    WHERE cu.IdCuenta = i.IdCuenta
      AND cu.IdAsesor <> i.idAsesor
      AND cu.Activo = 1
)
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 51040, 'No se encontro un inmueble con otro asesor activo en la misma Cuenta para probar autorizacion.', 1;

SELECT @CorreoPropietario = correo
FROM dbo.RSMAPS_Usuario
WHERE idAsesor = @IdAsesorPropietario;

SELECT TOP (1)
    @IdAsesorOtro = u.idAsesor,
    @CorreoOtro = u.correo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = cu.IdAsesor
WHERE cu.IdCuenta = @IdCuentaPrueba
  AND cu.IdAsesor <> @IdAsesorPropietario
  AND cu.Activo = 1
  AND u.correo IS NOT NULL
ORDER BY u.idAsesor;

IF @CorreoPropietario IS NULL OR @CorreoOtro IS NULL
    THROW 51041, 'No fue posible resolver correos para la prueba de autorizacion.', 1;

SET @PrecioPrueba = CASE WHEN @Precio IS NULL THEN 1 ELSE @Precio + 1 END;

/* A. El propietario puede editar y el rollback restaura datos. */
BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_insertar_coordenadas
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPropietario,
    @idTipo = @IdTipo,
    @terreno = @Terreno,
    @construccion = @Construccion,
    @precio = @PrecioPrueba,
    @observaciones = 'PRUEBA PASO 10 - ROLLBACK',
    @contacto = @Contacto;

SELECT
    @IdInmueblePrueba AS idInmueble,
    @CorreoPropietario AS Propietario,
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @IdInmueblePrueba
              AND idAsesor = @IdAsesorPropietario
              AND IdCuenta = @IdCuentaPrueba
              AND precio = @PrecioPrueba
        )
        THEN 'OK - PROPIETARIO PUEDE EDITAR'
        ELSE 'REVISAR'
    END AS EstadoEdicionPropia;

ROLLBACK TRANSACTION;

/* B. Otro asesor de la misma cuenta NO puede editar. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_insertar_coordenadas
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoOtro,
        @idTipo = @IdTipo,
        @terreno = @Terreno,
        @construccion = @Construccion,
        @precio = @PrecioPrueba,
        @observaciones = 'NO DEBE GUARDARSE',
        @contacto = @Contacto;

    SELECT 'ERROR - EDICION AJENA NO FUE BLOQUEADA' AS EstadoEdicionAjena;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51024
        SELECT
            'OK - EDICION AJENA BLOQUEADA' AS EstadoEdicionAjena,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

/* C. Otro asesor de la misma cuenta NO puede eliminar. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_delete_inmueble_seguro
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoOtro;

    SELECT 'ERROR - ELIMINACION AJENA NO FUE BLOQUEADA' AS EstadoEliminacionAjena;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51035
        SELECT
            'OK - ELIMINACION AJENA BLOQUEADA' AS EstadoEliminacionAjena,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

/* D. Confirmar que el inmueble sigue intacto. */
SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.precio,
    i.observaciones,
    CASE
        WHEN i.IdCuenta = @IdCuentaPrueba
         AND i.idAsesor = @IdAsesorPropietario
         AND ((i.precio = @Precio) OR (i.precio IS NULL AND @Precio IS NULL))
        THEN 'OK - DATOS ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueblePrueba;

PRINT 'Paso 10 RSMaps 2.0 terminado correctamente.';
