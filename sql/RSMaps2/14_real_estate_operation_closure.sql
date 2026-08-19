/* ============================================================
   RSMaps 2.0 - Paso 14
   CIERRE REAL DE OPERACIONES DE VENTA / RENTA

   Base esperada: mapsMarkers

   Objetivo:
   - Registrar una operacion real antes de marcar VENDIDO o RENTADO.
   - Conservar el inmueble; NO moverlo ni borrarlo fisicamente.
   - Guardar snapshot suficiente para analitica futura.
   - Registrar precio de cierre y fecha de cierre reales.
   - Mantener autorizacion conservadora: solo el asesor propietario.
   - No tocar RSMAPS_InmuebleVendido legacy.

   IMPORTANTE:
   - VENDIDO/RENTADO dejan de ser simples cambios de estado.
   - El historico legacy sigue considerandose NO clasificado.
   - Las pruebas usan ROLLBACK y no dejan operaciones persistentes.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 51400, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51401, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado', N'U') IS NULL
    THROW 51402, 'No existe dbo.RSMAPS_InmuebleCambioEstado. Ejecutar primero Paso 11.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'EstadoCodigo') IS NULL
    THROW 51403, 'Falta EstadoCodigo. Ejecutar primero Paso 11.', 1;

/* ------------------------------------------------------------
   1. Tabla de operaciones reales
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_OperacionInmueble', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_OperacionInmueble
    (
        IdOperacion bigint IDENTITY(1,1) NOT NULL,
        IdInmueble int NOT NULL,
        IdCuenta int NOT NULL,
        IdAsesor int NOT NULL,
        TipoOperacion varchar(10) NOT NULL,
        PrecioPublicado decimal(18,2) NULL,
        PrecioCierre decimal(18,2) NOT NULL,
        Moneda char(3) NOT NULL
            CONSTRAINT DF_RSMAPS_OperacionInmueble_Moneda DEFAULT ('MXN'),
        FechaPublicacionUtc datetime2(0) NULL,
        FechaCierreUtc datetime2(0) NOT NULL,
        DiasEnMercado int NULL,
        EstadoAnterior varchar(20) NOT NULL,
        VisibilidadAlCierre varchar(20) NOT NULL,
        IdTipo int NULL,
        Lat decimal(10,6) NULL,
        Lng decimal(10,6) NULL,
        Terreno float NULL,
        Construccion float NULL,
        Direccion nvarchar(500) NULL,
        NotasCierre nvarchar(1000) NULL,
        Origen varchar(30) NOT NULL
            CONSTRAINT DF_RSMAPS_OperacionInmueble_Origen DEFAULT ('APLICACION'),
        EsDatoConfiable bit NOT NULL
            CONSTRAINT DF_RSMAPS_OperacionInmueble_Confiable DEFAULT (1),
        FechaRegistroUtc datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_OperacionInmueble_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_RSMAPS_OperacionInmueble PRIMARY KEY (IdOperacion),
        CONSTRAINT CK_RSMAPS_OperacionInmueble_Tipo
            CHECK (TipoOperacion IN ('VENTA', 'RENTA')),
        CONSTRAINT CK_RSMAPS_OperacionInmueble_PrecioCierre
            CHECK (PrecioCierre > 0)
    );

    CREATE INDEX IX_RSMAPS_OperacionInmueble_Inmueble_Fecha
        ON dbo.RSMAPS_OperacionInmueble(IdInmueble, FechaCierreUtc DESC);

    CREATE INDEX IX_RSMAPS_OperacionInmueble_Cuenta_Tipo_Fecha
        ON dbo.RSMAPS_OperacionInmueble(IdCuenta, TipoOperacion, FechaCierreUtc DESC);

    CREATE INDEX IX_RSMAPS_OperacionInmueble_Ubicacion
        ON dbo.RSMAPS_OperacionInmueble(Lat, Lng);
END;

/* ------------------------------------------------------------
   2. Procedimiento de cierre autorizado
   ------------------------------------------------------------ */
DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_CerrarOperacionInmueble
    @idInmueble INT,
    @correo VARCHAR(200),
    @TipoOperacion VARCHAR(10),
    @PrecioCierre DECIMAL(18,2),
    @FechaCierreUtc DATETIME2(0) = NULL,
    @NotasCierre NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;
    DECLARE @EstadoAnterior VARCHAR(20);
    DECLARE @VisibilidadAnterior VARCHAR(20);
    DECLARE @EstadoCierre VARCHAR(20);
    DECLARE @FechaPublicacionUtc DATETIME2(0);
    DECLARE @FechaCierre DATETIME2(0);
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @PrecioPublicado DECIMAL(18,2);
    DECLARE @IdTipo INT;
    DECLARE @Lat DECIMAL(10,6);
    DECLARE @Lng DECIMAL(10,6);
    DECLARE @Terreno FLOAT;
    DECLARE @Construccion FLOAT;
    DECLARE @Direccion NVARCHAR(500);
    DECLARE @IdOperacion BIGINT;

    SET @TipoOperacion = UPPER(LTRIM(RTRIM(@TipoOperacion)));
    SET @FechaCierre = COALESCE(@FechaCierreUtc, @AhoraUtc);

    IF @TipoOperacion NOT IN (''VENTA'', ''RENTA'')
        THROW 51420, ''TipoOperacion debe ser VENTA o RENTA.'', 1;

    IF @PrecioCierre IS NULL OR @PrecioCierre <= 0
        THROW 51421, ''El precio de cierre debe ser mayor que cero.'', 1;

    IF @FechaCierre > DATEADD(MINUTE, 10, @AhoraUtc)
        THROW 51422, ''La fecha de cierre no puede estar en el futuro.'', 1;

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51423, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

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
            THROW 51424, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51425, ''El usuario pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @idAsesorInmueble = i.idAsesor,
        @idCuentaInmueble = i.IdCuenta,
        @EstadoAnterior = i.EstadoCodigo,
        @VisibilidadAnterior = i.VisibilidadCodigo,
        @FechaPublicacionUtc = i.FechaPublicacionUtc,
        @PrecioPublicado = TRY_CONVERT(DECIMAL(18,2), i.precio),
        @IdTipo = i.idTipo,
        @Lat = i.lat,
        @Lng = i.lng,
        @Terreno = i.terreno,
        @Construccion = i.construccion,
        @Direccion = LEFT(CONVERT(NVARCHAR(MAX), i.direccion), 500)
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51426, ''El inmueble no existe.'', 1;

    IF @idCuentaInmueble <> @idCuenta
        THROW 51427, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

    IF @idAsesorInmueble <> @idAsesor
        THROW 51428, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    IF @EstadoAnterior IN (''VENDIDO'', ''RENTADO'')
        THROW 51429, ''El inmueble ya tiene un estado de cierre.'', 1;

    IF @EstadoAnterior NOT IN (''PUBLICADO'', ''PAUSADO'', ''RETIRADO'')
        THROW 51430, ''El estado actual del inmueble no permite registrar un cierre.'', 1;

    IF @FechaPublicacionUtc IS NOT NULL
       AND @FechaCierre < @FechaPublicacionUtc
        THROW 51431, ''La fecha de cierre no puede ser anterior a la fecha de publicacion conocida.'', 1;

    SET @EstadoCierre = CASE @TipoOperacion
        WHEN ''VENTA'' THEN ''VENDIDO''
        WHEN ''RENTA'' THEN ''RENTADO''
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.RSMAPS_OperacionInmueble
        (
            IdInmueble,
            IdCuenta,
            IdAsesor,
            TipoOperacion,
            PrecioPublicado,
            PrecioCierre,
            Moneda,
            FechaPublicacionUtc,
            FechaCierreUtc,
            DiasEnMercado,
            EstadoAnterior,
            VisibilidadAlCierre,
            IdTipo,
            Lat,
            Lng,
            Terreno,
            Construccion,
            Direccion,
            NotasCierre,
            Origen,
            EsDatoConfiable
        )
        VALUES
        (
            @idInmueble,
            @idCuenta,
            @idAsesor,
            @TipoOperacion,
            @PrecioPublicado,
            @PrecioCierre,
            ''MXN'',
            @FechaPublicacionUtc,
            @FechaCierre,
            CASE
                WHEN @FechaPublicacionUtc IS NULL THEN NULL
                ELSE DATEDIFF(DAY, @FechaPublicacionUtc, @FechaCierre)
            END,
            @EstadoAnterior,
            @VisibilidadAnterior,
            @IdTipo,
            @Lat,
            @Lng,
            @Terreno,
            @Construccion,
            @Direccion,
            NULLIF(LTRIM(RTRIM(@NotasCierre)), N''''),
            ''APLICACION'',
            1
        );

        SET @IdOperacion = CONVERT(BIGINT, SCOPE_IDENTITY());

        UPDATE dbo.RSMAPS_Inmueble
        SET
            EstadoCodigo = @EstadoCierre,
            FechaUltimoCambioEstadoUtc = @AhoraUtc
        WHERE idInmueble = @idInmueble;

        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble,
            IdCuenta,
            EstadoAnterior,
            EstadoNuevo,
            VisibilidadAnterior,
            VisibilidadNueva,
            IdAsesorCambio,
            FechaCambioUtc,
            Motivo,
            Origen
        )
        VALUES
        (
            @idInmueble,
            @idCuenta,
            @EstadoAnterior,
            @EstadoCierre,
            @VisibilidadAnterior,
            @VisibilidadAnterior,
            @idAsesor,
            @AhoraUtc,
            CONCAT(N''Cierre '', @TipoOperacion, N''. Precio de cierre: '', CONVERT(NVARCHAR(50), @PrecioCierre),
                   CASE WHEN NULLIF(LTRIM(RTRIM(@NotasCierre)), N'''') IS NULL THEN N''''
                        ELSE CONCAT(N''. '', @NotasCierre) END),
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT
        o.IdOperacion,
        o.IdInmueble,
        o.IdCuenta,
        o.IdAsesor,
        o.TipoOperacion,
        o.PrecioPublicado,
        o.PrecioCierre,
        o.Moneda,
        o.FechaPublicacionUtc,
        o.FechaCierreUtc,
        o.DiasEnMercado,
        i.EstadoCodigo AS EstadoFinal
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = o.IdInmueble
    WHERE o.IdOperacion = @IdOperacion;
END;
';

EXEC sys.sp_executesql @sql;

/* ============================================================
   3. PRUEBAS CONTROLADAS CON ROLLBACK
   ============================================================ */
DECLARE @IdInmueblePrueba INT;
DECLARE @IdCuentaPrueba INT;
DECLARE @IdAsesorPropietario INT;
DECLARE @CorreoPropietario VARCHAR(200);
DECLARE @CorreoOtro VARCHAR(200);
DECLARE @PrecioPublicado DECIMAL(18,2);
DECLARE @PrecioCierre DECIMAL(18,2);
DECLARE @EstadoOriginal VARCHAR(20);
DECLARE @VisibilidadOriginal VARCHAR(20);
DECLARE @OperacionesAntes INT;
DECLARE @HistorialAntes INT;
DECLARE @MarketplaceAntes INT;
DECLARE @LegacyVendidosAntes INT = CASE
    WHEN OBJECT_ID(N'dbo.RSMAPS_InmuebleVendido', N'U') IS NULL THEN 0
    ELSE (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleVendido)
END;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdCuentaPrueba = i.IdCuenta,
    @IdAsesorPropietario = i.idAsesor,
    @PrecioPublicado = TRY_CONVERT(DECIMAL(18,2), i.precio),
    @EstadoOriginal = i.EstadoCodigo,
    @VisibilidadOriginal = i.VisibilidadCodigo
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

IF @IdInmueblePrueba IS NULL
    THROW 51440, 'No se encontro inmueble adecuado para la prueba del Paso 14.', 1;

SELECT @CorreoPropietario = correo
FROM dbo.RSMAPS_Usuario
WHERE idAsesor = @IdAsesorPropietario;

SELECT TOP (1) @CorreoOtro = u.correo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = cu.IdAsesor
WHERE cu.IdCuenta = @IdCuentaPrueba
  AND cu.IdAsesor <> @IdAsesorPropietario
  AND cu.Activo = 1
  AND u.correo IS NOT NULL
ORDER BY u.idAsesor;

IF @CorreoPropietario IS NULL OR @CorreoOtro IS NULL
    THROW 51441, 'No fue posible resolver usuarios para la prueba.', 1;

SET @PrecioCierre = CASE
    WHEN @PrecioPublicado IS NULL OR @PrecioPublicado <= 0 THEN 1000000.00
    ELSE ROUND(@PrecioPublicado * 0.95, 2)
END;

SELECT @OperacionesAntes = COUNT(*)
FROM dbo.RSMAPS_OperacionInmueble
WHERE IdInmueble = @IdInmueblePrueba;

SELECT @HistorialAntes = COUNT(*)
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE IdInmueble = @IdInmueblePrueba;

SELECT @MarketplaceAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CerrarOperacionInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPropietario,
    @TipoOperacion = 'VENTA',
    @PrecioCierre = @PrecioCierre,
    @FechaCierreUtc = NULL,
    @NotasCierre = N'PRUEBA PASO 14 - DEBE HACER ROLLBACK';

SELECT
    @IdInmueblePrueba AS idInmueble,
    (SELECT EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble = @IdInmueblePrueba) AS EstadoDurante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) AS OperacionesDurante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) AS HistorialDurante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo = 'PUBLICADO' AND VisibilidadCodigo = 'PUBLICO') AS MarketplaceDurante,
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_OperacionInmueble
            WHERE IdInmueble = @IdInmueblePrueba
              AND TipoOperacion = 'VENTA'
              AND PrecioCierre = @PrecioCierre
              AND EsDatoConfiable = 1
        )
         AND EXISTS
         (
             SELECT 1
             FROM dbo.RSMAPS_Inmueble
             WHERE idInmueble = @IdInmueblePrueba
               AND EstadoCodigo = 'VENDIDO'
         )
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo = 'PUBLICADO' AND VisibilidadCodigo = 'PUBLICO') = @MarketplaceAntes - 1
        THEN 'OK - VENTA REAL REGISTRADA SIN BORRAR INMUEBLE'
        ELSE 'REVISAR'
    END AS EstadoPruebaVenta;

SELECT
    CASE
        WHEN OBJECT_ID(N'dbo.RSMAPS_InmuebleVendido', N'U') IS NULL THEN 0
        ELSE (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleVendido)
    END AS LegacyVendidosDurante,
    @LegacyVendidosAntes AS LegacyVendidosAntes,
    CASE
        WHEN OBJECT_ID(N'dbo.RSMAPS_InmuebleVendido', N'U') IS NULL
          OR (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleVendido) = @LegacyVendidosAntes
        THEN 'OK - HISTORICO LEGACY NO TOCADO'
        ELSE 'REVISAR'
    END AS EstadoLegacy;

ROLLBACK TRANSACTION;

/* Otro asesor no puede cerrar la operacion. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_CerrarOperacionInmueble
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoOtro,
        @TipoOperacion = 'VENTA',
        @PrecioCierre = @PrecioCierre;

    SELECT 'ERROR - CIERRE AJENO NO FUE BLOQUEADO' AS EstadoAutorizacion;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51428
        SELECT
            'OK - CIERRE AJENO BLOQUEADO' AS EstadoAutorizacion,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

/* Precio invalido debe bloquearse. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_CerrarOperacionInmueble
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoPropietario,
        @TipoOperacion = 'VENTA',
        @PrecioCierre = 0;

    SELECT 'ERROR - PRECIO INVALIDO NO FUE BLOQUEADO' AS EstadoPrecio;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51421
        SELECT
            'OK - PRECIO INVALIDO BLOQUEADO' AS EstadoPrecio,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

SELECT
    @IdInmueblePrueba AS idInmueble,
    EstadoCodigo,
    VisibilidadCodigo,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) AS OperacionesActuales,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) AS HistorialActual,
    CASE
        WHEN EstadoCodigo = @EstadoOriginal
         AND VisibilidadCodigo = @VisibilidadOriginal
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) = @OperacionesAntes
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) = @HistorialAntes
        THEN 'OK - DATOS E HISTORIAL ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble
WHERE idInmueble = @IdInmueblePrueba;

SELECT
    p.name AS Procedimiento,
    prm.parameter_id,
    prm.name AS Parametro,
    TYPE_NAME(prm.user_type_id) AS TipoDato,
    prm.max_length AS LongitudBytes
FROM sys.procedures p
INNER JOIN sys.parameters prm
    ON prm.object_id = p.object_id
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_CerrarOperacionInmueble')
ORDER BY prm.parameter_id;

PRINT 'Paso 14 RSMaps 2.0 terminado correctamente.';
