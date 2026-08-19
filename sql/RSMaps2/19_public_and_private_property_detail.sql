/* ============================================================
   RSMaps 2.0 - Paso 19
   DETALLE PUBLICO VS DETALLE PRIVADO DE INMUEBLE

   Base esperada: mapsMarkers

   Objetivo:
   - Evitar que una propiedad PAUSADA, RETIRADA, VENDIDA, RENTADA o
     no PUBLICA siga siendo accesible por un ID numerico publico.
   - Crear una lectura publica que exija PUBLICADO + PUBLICO.
   - Crear una lectura privada autenticada que valide Cuenta + asesor.
   - Preparar la futura visibilidad ENLACE para usar token no adivinable,
     no /Share/{id}.

   IMPORTANTE:
   - No cambia datos persistentes de inmuebles.
   - Las pruebas usan ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 51900, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51901, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 51902, 'No existe dbo.RSMAPS_Usuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 51903, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'EstadoCodigo') IS NULL
    THROW 51904, 'Falta EstadoCodigo. Ejecutar primero Paso 11.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'VisibilidadCodigo') IS NULL
    THROW 51905, 'Falta VisibilidadCodigo. Ejecutar primero Paso 11.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GetInmueblePublicoById
    @idInmueble INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.idInmueble,
        i.idInmobiliaria,
        inm.nombre,
        i.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        i.telefono,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.exclusiva,
        i.link,
        i.contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
        ON inm.idInmobiliaria = i.idInmobiliaria
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    WHERE i.idInmueble = @idInmueble
      AND i.EstadoCodigo = ''PUBLICADO''
      AND i.VisibilidadCodigo = ''PUBLICO'';
END;
');

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GetInmueblePrivadoById
    @idInmueble INT,
    @correo VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51920, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

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
            THROW 51921, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51922, ''El usuario autenticado pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @idAsesorInmueble = i.idAsesor,
        @idCuentaInmueble = i.IdCuenta
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51923, ''El inmueble no existe.'', 1;

    IF @idCuentaInmueble <> @idCuenta
        THROW 51924, ''El inmueble pertenece a una Cuenta diferente.'', 1;

    IF @idAsesorInmueble <> @idAsesor
        THROW 51925, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    SELECT
        i.idInmueble,
        i.idInmobiliaria,
        inm.nombre,
        i.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        i.telefono,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.exclusiva,
        i.link,
        i.contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaPublicacionUtc,
        i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
        ON inm.idInmobiliaria = i.idInmobiliaria
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    WHERE i.idInmueble = @idInmueble;
END;
');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   PRUEBAS CONTROLADAS
   ============================================================ */
DECLARE @IdInmueble INT;
DECLARE @IdCuenta INT;
DECLARE @IdAsesor INT;
DECLARE @CorreoPropietario VARCHAR(200);
DECLARE @CorreoOtro VARCHAR(200);
DECLARE @PublicoAntes INT;
DECLARE @PublicoPausado INT;
DECLARE @PrivadoPausado INT;
DECLARE @PublicoCuenta INT;
DECLARE @PrivadoCuenta INT;

SELECT TOP (1)
    @IdInmueble = i.idInmueble,
    @IdCuenta = i.IdCuenta,
    @IdAsesor = i.idAsesor
FROM dbo.RSMAPS_Inmueble i
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO'
  AND EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_CuentaUsuario cu
      WHERE cu.IdCuenta = i.IdCuenta
        AND cu.IdAsesor <> i.idAsesor
        AND cu.Activo = 1
  )
ORDER BY i.idInmueble;

IF @IdInmueble IS NULL
    THROW 51940, 'No se encontró inmueble adecuado para la prueba.', 1;

SELECT @CorreoPropietario = correo
FROM dbo.RSMAPS_Usuario
WHERE idAsesor = @IdAsesor;

SELECT TOP (1) @CorreoOtro = u.correo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = cu.IdAsesor
WHERE cu.IdCuenta = @IdCuenta
  AND cu.IdAsesor <> @IdAsesor
  AND cu.Activo = 1
  AND u.correo IS NOT NULL
ORDER BY u.idAsesor;

CREATE TABLE #Publico
(
    idInmueble INT,
    idInmobiliaria INT NULL,
    nombre VARCHAR(MAX) NULL,
    idAsesor INT,
    nombres VARCHAR(MAX),
    aPaterno VARCHAR(MAX),
    correo VARCHAR(200) NULL,
    direccion VARCHAR(MAX) NULL,
    lat DECIMAL(10,6) NULL,
    lng DECIMAL(10,6) NULL,
    idTipo INT NULL,
    telefono VARCHAR(MAX) NULL,
    terreno FLOAT NULL,
    construccion FLOAT NULL,
    precio FLOAT NULL,
    observaciones VARCHAR(MAX) NULL,
    exclusiva INT NULL,
    link VARCHAR(MAX) NULL,
    contacto_a VARCHAR(MAX) NULL,
    imagenes INT
);

INSERT #Publico
EXEC dbo.RSMAPS_sp_GetInmueblePublicoById @idInmueble = @IdInmueble;
SELECT @PublicoAntes = COUNT(*) FROM #Publico;
TRUNCATE TABLE #Publico;

BEGIN TRANSACTION;
UPDATE dbo.RSMAPS_Inmueble
SET EstadoCodigo = 'PAUSADO'
WHERE idInmueble = @IdInmueble;

INSERT #Publico
EXEC dbo.RSMAPS_sp_GetInmueblePublicoById @idInmueble = @IdInmueble;
SELECT @PublicoPausado = COUNT(*) FROM #Publico;

DECLARE @PrivadoPausadoTable TABLE
(
    idInmueble INT,
    idInmobiliaria INT NULL,
    nombre VARCHAR(MAX) NULL,
    idAsesor INT,
    nombres VARCHAR(MAX),
    aPaterno VARCHAR(MAX),
    correo VARCHAR(200) NULL,
    direccion VARCHAR(MAX) NULL,
    lat DECIMAL(10,6) NULL,
    lng DECIMAL(10,6) NULL,
    idTipo INT NULL,
    telefono VARCHAR(MAX) NULL,
    terreno FLOAT NULL,
    construccion FLOAT NULL,
    precio FLOAT NULL,
    observaciones VARCHAR(MAX) NULL,
    exclusiva INT NULL,
    link VARCHAR(MAX) NULL,
    contacto_a VARCHAR(MAX) NULL,
    imagenes INT,
    EstadoCodigo VARCHAR(20),
    VisibilidadCodigo VARCHAR(20),
    FechaPublicacionUtc DATETIME2(0) NULL,
    FechaUltimoCambioEstadoUtc DATETIME2(0) NULL
);

INSERT @PrivadoPausadoTable
EXEC dbo.RSMAPS_sp_GetInmueblePrivadoById
    @idInmueble = @IdInmueble,
    @correo = @CorreoPropietario;
SELECT @PrivadoPausado = COUNT(*) FROM @PrivadoPausadoTable;

SELECT
    @IdInmueble AS idInmueble,
    @PublicoAntes AS PublicoAntes,
    @PublicoPausado AS PublicoDurantePausa,
    @PrivadoPausado AS PrivadoDurantePausa,
    CASE WHEN @PublicoAntes = 1 AND @PublicoPausado = 0 AND @PrivadoPausado = 1
         THEN 'OK - PAUSADO OCULTO PUBLICAMENTE Y VISIBLE AL PROPIETARIO'
         ELSE 'REVISAR' END AS EstadoPruebaPausa;

ROLLBACK TRANSACTION;

TRUNCATE TABLE #Publico;
BEGIN TRANSACTION;
UPDATE dbo.RSMAPS_Inmueble
SET VisibilidadCodigo = 'CUENTA'
WHERE idInmueble = @IdInmueble;

INSERT #Publico
EXEC dbo.RSMAPS_sp_GetInmueblePublicoById @idInmueble = @IdInmueble;
SELECT @PublicoCuenta = COUNT(*) FROM #Publico;

DELETE FROM @PrivadoPausadoTable;
INSERT @PrivadoPausadoTable
EXEC dbo.RSMAPS_sp_GetInmueblePrivadoById
    @idInmueble = @IdInmueble,
    @correo = @CorreoPropietario;
SELECT @PrivadoCuenta = COUNT(*) FROM @PrivadoPausadoTable;

SELECT
    @IdInmueble AS idInmueble,
    @PublicoCuenta AS PublicoConVisibilidadCuenta,
    @PrivadoCuenta AS PrivadoConVisibilidadCuenta,
    CASE WHEN @PublicoCuenta = 0 AND @PrivadoCuenta = 1
         THEN 'OK - VISIBILIDAD CUENTA NO SE EXPONE PUBLICAMENTE'
         ELSE 'REVISAR' END AS EstadoPruebaVisibilidad;

ROLLBACK TRANSACTION;

BEGIN TRY
    EXEC dbo.RSMAPS_sp_GetInmueblePrivadoById
        @idInmueble = @IdInmueble,
        @correo = @CorreoOtro;
    SELECT 'ERROR - LECTURA PRIVADA AJENA NO BLOQUEADA' AS EstadoAutorizacion;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51925
        SELECT 'OK - LECTURA PRIVADA AJENA BLOQUEADA' AS EstadoAutorizacion,
               ERROR_NUMBER() AS NumeroError,
               ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

SELECT
    i.idInmueble,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    CASE WHEN i.EstadoCodigo = 'PUBLICADO' AND i.VisibilidadCodigo = 'PUBLICO'
         THEN 'OK - DATOS ORIGINALES INTACTOS'
         ELSE 'REVISAR' END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueble;

DROP TABLE #Publico;

PRINT 'Paso 19 RSMaps 2.0 terminado correctamente.';
