/* ============================================================
   RSMaps 2.0 - Paso 15
   HISTORIAL CONFIABLE DE PRECIOS

   Base esperada: mapsMarkers

   Objetivo:
   - Conservar una linea base del precio actual de cada inmueble sin
     inventar una fecha historica de publicacion.
   - Registrar automaticamente cambios reales de precio en futuras
     ediciones de inmuebles.
   - No generar historial si una edicion conserva el mismo precio.
   - Mantener autorizacion Cuenta + asesor del flujo actual.
   - Preparar analitica de reducciones, sobreprecio, negociacion y
     comportamiento temporal para Web / Android / iOS.

   IMPORTANTE:
   - Los precios legacy se registran como MIGRACION y EsDatoConfiable = 0
     respecto a su fecha historica. El valor observado si es el actual.
   - Los cambios posteriores quedan como APLICACION y EsDatoConfiable = 1.
   - RSMAPS_Inmueble.precio sigue siendo FLOAT por compatibilidad. La nueva
     historia usa DECIMAL(18,2); una futura migracion debera normalizar
     campos monetarios a DECIMAL.
   - Las pruebas usan ROLLBACK y no dejan cambios persistentes.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 51500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51501, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 51502, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;

/* ------------------------------------------------------------
   1. Tabla de historial de precios
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioHistorial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_InmueblePrecioHistorial
    (
        IdPrecioHistorial bigint IDENTITY(1,1) NOT NULL,
        IdInmueble        int NOT NULL,
        IdCuenta          int NOT NULL,
        IdAsesor          int NOT NULL,
        PrecioAnterior    decimal(18,2) NULL,
        PrecioNuevo       decimal(18,2) NOT NULL,
        Moneda            char(3) NOT NULL
            CONSTRAINT DF_RSMAPS_InmueblePrecioHistorial_Moneda DEFAULT ('MXN'),
        FechaCambioUtc    datetime2(0) NULL,
        Motivo            nvarchar(500) NULL,
        Origen            varchar(30) NOT NULL,
        EsDatoConfiable   bit NOT NULL,
        FechaRegistroUtc  datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_InmueblePrecioHistorial_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_RSMAPS_InmueblePrecioHistorial
            PRIMARY KEY (IdPrecioHistorial),
        CONSTRAINT CK_RSMAPS_InmueblePrecioHistorial_PrecioNuevo
            CHECK (PrecioNuevo >= 0)
    );

    CREATE INDEX IX_RSMAPS_InmueblePrecioHistorial_Inmueble_Fecha
        ON dbo.RSMAPS_InmueblePrecioHistorial(IdInmueble, FechaRegistroUtc DESC);

    CREATE INDEX IX_RSMAPS_InmueblePrecioHistorial_Cuenta_Fecha
        ON dbo.RSMAPS_InmueblePrecioHistorial(IdCuenta, FechaRegistroUtc DESC);
END;

/* ------------------------------------------------------------
   2. Linea base de migracion para inmuebles actuales

   FechaCambioUtc queda NULL deliberadamente: conocemos el precio observado
   al migrar, no la fecha real en la que comenzo a ofertarse a ese precio.
   ------------------------------------------------------------ */
INSERT dbo.RSMAPS_InmueblePrecioHistorial
(
    IdInmueble,
    IdCuenta,
    IdAsesor,
    PrecioAnterior,
    PrecioNuevo,
    Moneda,
    FechaCambioUtc,
    Motivo,
    Origen,
    EsDatoConfiable
)
SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    NULL,
    TRY_CONVERT(decimal(18,2), i.precio),
    'MXN',
    NULL,
    N'Precio observado al establecer la linea base de RSMaps 2.0; fecha historica real desconocida.',
    'MIGRACION',
    0
FROM dbo.RSMAPS_Inmueble i
WHERE TRY_CONVERT(decimal(18,2), i.precio) IS NOT NULL
  AND TRY_CONVERT(decimal(18,2), i.precio) >= 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_InmueblePrecioHistorial h
      WHERE h.IdInmueble = i.idInmueble
        AND h.Origen = 'MIGRACION'
        AND h.PrecioAnterior IS NULL
  );

/* ------------------------------------------------------------
   3. Extender procedimiento actual de alta / edicion
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
    DECLARE @PrecioAnterior DECIMAL(18,2);
    DECLARE @PrecioNuevo DECIMAL(18,2) = TRY_CONVERT(DECIMAL(18,2), @precio);
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();

    IF @PrecioNuevo IS NULL OR @PrecioNuevo < 0
        THROW 51520, ''El precio del inmueble debe ser un valor numerico mayor o igual a cero.'', 1;

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

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @idInmueble IS NULL
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

            INSERT dbo.RSMAPS_InmueblePrecioHistorial
            (
                IdInmueble,
                IdCuenta,
                IdAsesor,
                PrecioAnterior,
                PrecioNuevo,
                Moneda,
                FechaCambioUtc,
                Motivo,
                Origen,
                EsDatoConfiable
            )
            VALUES
            (
                @idInmueble,
                @idCuenta,
                @idAsesor,
                NULL,
                @PrecioNuevo,
                ''MXN'',
                @AhoraUtc,
                N''Precio inicial registrado al crear el inmueble.'',
                ''ALTA'',
                1
            );
        END
        ELSE
        BEGIN
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.RSMAPS_Inmueble
                WHERE idInmueble = @idInmueble
            )
                THROW 51025, ''El inmueble solicitado para actualizar no existe.'', 1;

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

            SELECT @PrecioAnterior = TRY_CONVERT(DECIMAL(18,2), precio)
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @idInmueble;

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

            IF @PrecioAnterior IS NULL OR @PrecioAnterior <> @PrecioNuevo
            BEGIN
                INSERT dbo.RSMAPS_InmueblePrecioHistorial
                (
                    IdInmueble,
                    IdCuenta,
                    IdAsesor,
                    PrecioAnterior,
                    PrecioNuevo,
                    Moneda,
                    FechaCambioUtc,
                    Motivo,
                    Origen,
                    EsDatoConfiable
                )
                VALUES
                (
                    @idInmueble,
                    @idCuenta,
                    @idAsesor,
                    @PrecioAnterior,
                    @PrecioNuevo,
                    ''MXN'',
                    @AhoraUtc,
                    N''Cambio de precio registrado durante edicion del inmueble.'',
                    ''APLICACION'',
                    1
                );
            END;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idInmueble AS idInmueble;
END;
';

EXEC sys.sp_executesql @sqlWrite;

/* ============================================================
   4. VALIDACION DE LINEA BASE
   ============================================================ */
SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble) AS TotalInmuebles,
    COUNT(*) AS LineasBasePrecio,
    COUNT(DISTINCT IdInmueble) AS InmueblesConLineaBase,
    SUM(CASE WHEN FechaCambioUtc IS NULL AND EsDatoConfiable = 0 THEN 1 ELSE 0 END) AS LineasBaseSinFechaInventada
FROM dbo.RSMAPS_InmueblePrecioHistorial
WHERE Origen = 'MIGRACION';

/* ============================================================
   5. PRUEBAS CONTROLADAS
   ============================================================ */
DECLARE @IdInmueblePrueba INT;
DECLARE @IdCuentaPrueba INT;
DECLARE @IdAsesorPrueba INT;
DECLARE @CorreoPrueba VARCHAR(200);
DECLARE @IdTipo INT;
DECLARE @Terreno FLOAT;
DECLARE @Construccion FLOAT;
DECLARE @PrecioOriginal DECIMAL(18,2);
DECLARE @PrecioPrueba DECIMAL(18,2);
DECLARE @Observaciones VARCHAR(MAX);
DECLARE @Contacto VARCHAR(MAX);
DECLARE @HistorialAntes INT;
DECLARE @HistorialDurante INT;
DECLARE @HistorialMismoPrecio INT;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdCuentaPrueba = i.IdCuenta,
    @IdAsesorPrueba = i.idAsesor,
    @IdTipo = i.idTipo,
    @Terreno = i.terreno,
    @Construccion = i.construccion,
    @PrecioOriginal = TRY_CONVERT(DECIMAL(18,2), i.precio),
    @Observaciones = i.observaciones,
    @Contacto = i.contacto_a
FROM dbo.RSMAPS_Inmueble i
WHERE TRY_CONVERT(DECIMAL(18,2), i.precio) IS NOT NULL
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 51540, 'No se encontro inmueble adecuado para probar historial de precios.', 1;

SELECT @CorreoPrueba = correo
FROM dbo.RSMAPS_Usuario
WHERE idAsesor = @IdAsesorPrueba;

IF @CorreoPrueba IS NULL
    THROW 51541, 'No fue posible resolver el correo del propietario para la prueba.', 1;

SET @PrecioPrueba = CASE
    WHEN @PrecioOriginal IS NULL THEN 1000
    ELSE @PrecioOriginal + 10000
END;

SELECT @HistorialAntes = COUNT(*)
FROM dbo.RSMAPS_InmueblePrecioHistorial
WHERE IdInmueble = @IdInmueblePrueba;

/* A. Cambiar precio crea exactamente una entrada nueva. */
BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_insertar_coordenadas
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPrueba,
    @idTipo = @IdTipo,
    @terreno = @Terreno,
    @construccion = @Construccion,
    @precio = @PrecioPrueba,
    @observaciones = @Observaciones,
    @contacto = @Contacto;

SELECT @HistorialDurante = COUNT(*)
FROM dbo.RSMAPS_InmueblePrecioHistorial
WHERE IdInmueble = @IdInmueblePrueba;

SELECT
    @IdInmueblePrueba AS idInmueble,
    @PrecioOriginal AS PrecioAnterior,
    @PrecioPrueba AS PrecioNuevo,
    @HistorialAntes AS HistorialAntes,
    @HistorialDurante AS HistorialDurante,
    CASE
        WHEN @HistorialDurante = @HistorialAntes + 1
         AND EXISTS
         (
             SELECT 1
             FROM dbo.RSMAPS_InmueblePrecioHistorial
             WHERE IdInmueble = @IdInmueblePrueba
               AND PrecioAnterior = @PrecioOriginal
               AND PrecioNuevo = @PrecioPrueba
               AND Origen = 'APLICACION'
               AND EsDatoConfiable = 1
               AND FechaCambioUtc IS NOT NULL
         )
        THEN 'OK - CAMBIO DE PRECIO CON HISTORIAL'
        ELSE 'REVISAR'
    END AS EstadoCambioPrecio;

ROLLBACK TRANSACTION;

/* B. Guardar exactamente el mismo precio NO crea entrada. */
BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_insertar_coordenadas
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPrueba,
    @idTipo = @IdTipo,
    @terreno = @Terreno,
    @construccion = @Construccion,
    @precio = @PrecioOriginal,
    @observaciones = @Observaciones,
    @contacto = @Contacto;

SELECT @HistorialMismoPrecio = COUNT(*)
FROM dbo.RSMAPS_InmueblePrecioHistorial
WHERE IdInmueble = @IdInmueblePrueba;

SELECT
    @IdInmueblePrueba AS idInmueble,
    @HistorialAntes AS HistorialAntes,
    @HistorialMismoPrecio AS HistorialDespuesMismoPrecio,
    CASE
        WHEN @HistorialMismoPrecio = @HistorialAntes
        THEN 'OK - MISMO PRECIO NO DUPLICA HISTORIAL'
        ELSE 'REVISAR'
    END AS EstadoMismoPrecio;

ROLLBACK TRANSACTION;

/* C. Estado final original. */
SELECT
    i.idInmueble,
    TRY_CONVERT(DECIMAL(18,2), i.precio) AS PrecioActual,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmueblePrecioHistorial h
     WHERE h.IdInmueble = i.idInmueble) AS HistorialActual,
    CASE
        WHEN TRY_CONVERT(DECIMAL(18,2), i.precio) = @PrecioOriginal
         AND (SELECT COUNT(*)
              FROM dbo.RSMAPS_InmueblePrecioHistorial h
              WHERE h.IdInmueble = i.idInmueble) = @HistorialAntes
        THEN 'OK - PRECIO E HISTORIAL ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueblePrueba;

SELECT
    p.parameter_id,
    p.name AS Parametro,
    TYPE_NAME(p.user_type_id) AS TipoDato,
    p.max_length AS LongitudBytes
FROM sys.parameters p
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_insertar_coordenadas')
ORDER BY p.parameter_id;

PRINT 'Paso 15 RSMaps 2.0 terminado correctamente.';
