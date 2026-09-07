/* ============================================================
   RSMaps 2.0 - Paso 48
   NORMALIZACION CONTROLADA DE PRECIOS Y CONTEXTO CONFIRMADO

   Objetivo:
   - Corregir exclusivamente inmuebles legacy cuyo precio/moneda/
     periodicidad quedaron confirmados durante la auditoria manual.
   - Conservar intactos los registros historicos MIGRACION previos.
   - Registrar un nuevo evento NORMALIZACION solo cuando cambia el
     importe estructurado actual.
   - Crear/actualizar RSMAPS_InmueblePrecioContexto con datos confiables.
   - No tocar inmuebles ambiguos (por ejemplo IdInmueble 113).
   - Ser idempotente y fallar cerrado si algun precio actual ya no coincide
     con el valor auditado ni con el valor objetivo.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 54800, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 54801, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioHistorial', N'U') IS NULL
    THROW 54802, 'No existe dbo.RSMAPS_InmueblePrecioHistorial.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioContexto', N'U') IS NULL
    THROW 54803, 'No existe dbo.RSMAPS_InmueblePrecioContexto. Ejecutar primero Paso 47.', 1;

DECLARE @Normalizar TABLE
(
    IdInmueble               int NOT NULL PRIMARY KEY,
    PrecioAuditadoAnterior   decimal(18,2) NOT NULL,
    PrecioObjetivo           decimal(18,2) NOT NULL,
    MonedaCodigo             char(3) NOT NULL,
    FrecuenciaPrecioCodigo   varchar(20) NOT NULL,
    Motivo                   nvarchar(500) NOT NULL
);

INSERT @Normalizar
(
    IdInmueble,
    PrecioAuditadoAnterior,
    PrecioObjetivo,
    MonedaCodigo,
    FrecuenciaPrecioCodigo,
    Motivo
)
VALUES
    (79,  0.00,    4500.00,    'MXN', 'DIARIO',  N'Normalizacion confirmada: renta por noche de $4,500 MXN.'),
    (91,  0.00,    7812500.00, 'MXN', 'UNICO',   N'Normalizacion confirmada: precio de venta $7,812,500 MXN.'),
    (95,  1500.00, 1500000.00, 'MXN', 'UNICO',   N'Correccion de captura confirmada: $1,500 correspondia a $1,500,000 MXN.'),
    (110, 0.00,    5850000.00, 'MXN', 'UNICO',   N'Normalizacion confirmada: precio de venta $5,850,000 MXN.'),
    (111, 0.00,    4840000.00, 'MXN', 'UNICO',   N'Normalizacion confirmada: precio de venta $4,840,000 MXN.'),
    (112, 0.00,    340000.00,  'USD', 'UNICO',   N'Normalizacion confirmada: precio de venta USD 340,000.'),
    (115, 0.00,    6900000.00, 'MXN', 'UNICO',   N'Normalizacion confirmada: precio de venta $6,900,000 MXN.'),
    (117, 0.00,    9275000.00, 'MXN', 'UNICO',   N'Normalizacion confirmada: precio de venta $9,275,000 MXN.'),
    (150, 18000.00,18000.00,   'MXN', 'MENSUAL', N'Periodicidad confirmada: renta mensual $18,000 MXN.'),
    (151, 55000.00,55000.00,   'MXN', 'MENSUAL', N'Periodicidad confirmada: renta mensual $55,000 MXN.'),
    (154, 9500.00, 9500.00,    'MXN', 'MENSUAL', N'Periodicidad confirmada: renta mensual $9,500 MXN.');

/* ------------------------------------------------------------
   1. Validaciones de seguridad
   ------------------------------------------------------------ */
IF EXISTS
(
    SELECT 1
    FROM @Normalizar n
    LEFT JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = n.IdInmueble
    WHERE i.idInmueble IS NULL
)
    THROW 54810, 'Falta al menos uno de los inmuebles auditados. Normalizacion cancelada.', 1;

IF EXISTS
(
    SELECT 1
    FROM @Normalizar n
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = n.IdInmueble
    CROSS APPLY
    (
        SELECT TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual
    ) p
    WHERE p.PrecioActual IS NULL
       OR (p.PrecioActual <> n.PrecioAuditadoAnterior
           AND p.PrecioActual <> n.PrecioObjetivo)
)
BEGIN
    SELECT
        n.IdInmueble,
        n.PrecioAuditadoAnterior,
        TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual,
        n.PrecioObjetivo,
        n.MonedaCodigo,
        n.FrecuenciaPrecioCodigo
    FROM @Normalizar n
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = n.IdInmueble
    WHERE TRY_CONVERT(decimal(18,2), i.precio) IS NULL
       OR (TRY_CONVERT(decimal(18,2), i.precio) <> n.PrecioAuditadoAnterior
           AND TRY_CONVERT(decimal(18,2), i.precio) <> n.PrecioObjetivo);

    THROW 54811, 'Al menos un precio actual difiere de la auditoria. Normalizacion cancelada sin cambios.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM @Normalizar n
    LEFT JOIN dbo.RSMAPS_Moneda m ON m.Codigo = n.MonedaCodigo AND m.Activo = 1
    LEFT JOIN dbo.RSMAPS_FrecuenciaPrecio f ON f.Codigo = n.FrecuenciaPrecioCodigo AND f.Activo = 1
    WHERE m.Codigo IS NULL OR f.Codigo IS NULL
)
    THROW 54812, 'Catalogo de moneda o frecuencia incompleto. Normalizacion cancelada.', 1;

/* ------------------------------------------------------------
   2. Vista previa del plan
   ------------------------------------------------------------ */
SELECT
    n.IdInmueble,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual,
    n.PrecioObjetivo,
    n.MonedaCodigo,
    n.FrecuenciaPrecioCodigo,
    CASE
        WHEN TRY_CONVERT(decimal(18,2), i.precio) = n.PrecioObjetivo
            THEN 'SIN_CAMBIO_PRECIO'
        ELSE 'CORREGIR_PRECIO'
    END AS AccionPrecio,
    n.Motivo
FROM @Normalizar n
INNER JOIN dbo.RSMAPS_Inmueble i
    ON i.idInmueble = n.IdInmueble
ORDER BY n.IdInmueble;

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @AhoraUtc datetime2(0) = SYSUTCDATETIME();

    /* --------------------------------------------------------
       3. Historial: registrar solo correcciones reales de importe
       -------------------------------------------------------- */
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
        TRY_CONVERT(decimal(18,2), i.precio),
        n.PrecioObjetivo,
        n.MonedaCodigo,
        NULL,
        n.Motivo + N' Fecha historica real de vigencia desconocida.',
        'NORMALIZACION',
        1
    FROM @Normalizar n
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = n.IdInmueble
    WHERE TRY_CONVERT(decimal(18,2), i.precio) <> n.PrecioObjetivo
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_InmueblePrecioHistorial h
          WHERE h.IdInmueble = n.IdInmueble
            AND h.Origen = 'NORMALIZACION'
            AND h.PrecioNuevo = n.PrecioObjetivo
            AND h.Moneda = n.MonedaCodigo
      );

    /* --------------------------------------------------------
       4. Corregir precio estructurado actual solo si hace falta
       -------------------------------------------------------- */
    UPDATE i
    SET i.precio = CONVERT(float, n.PrecioObjetivo)
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN @Normalizar n
        ON n.IdInmueble = i.idInmueble
    WHERE TRY_CONVERT(decimal(18,2), i.precio) <> n.PrecioObjetivo;

    /* --------------------------------------------------------
       5. Upsert de contexto confiable
       -------------------------------------------------------- */
    UPDATE c
    SET
        c.MonedaCodigo = n.MonedaCodigo,
        c.FrecuenciaPrecioCodigo = n.FrecuenciaPrecioCodigo,
        c.EsDatoConfiable = 1,
        c.Origen = 'NORMALIZACION',
        c.Motivo = n.Motivo,
        c.FechaActualizacionUtc = @AhoraUtc
    FROM dbo.RSMAPS_InmueblePrecioContexto c
    INNER JOIN @Normalizar n
        ON n.IdInmueble = c.IdInmueble;

    INSERT dbo.RSMAPS_InmueblePrecioContexto
    (
        IdInmueble,
        MonedaCodigo,
        FrecuenciaPrecioCodigo,
        EsDatoConfiable,
        Origen,
        Motivo,
        FechaActualizacionUtc
    )
    SELECT
        n.IdInmueble,
        n.MonedaCodigo,
        n.FrecuenciaPrecioCodigo,
        1,
        'NORMALIZACION',
        n.Motivo,
        @AhoraUtc
    FROM @Normalizar n
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_InmueblePrecioContexto c
        WHERE c.IdInmueble = n.IdInmueble
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ------------------------------------------------------------
   6. Verificacion final
   ------------------------------------------------------------ */
SELECT
    i.idInmueble,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual,
    c.MonedaCodigo,
    c.FrecuenciaPrecioCodigo,
    c.EsDatoConfiable,
    c.Origen,
    c.Motivo
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_InmueblePrecioContexto c
    ON c.IdInmueble = i.idInmueble
WHERE i.idInmueble IN (79,91,95,110,111,112,115,117,150,151,154)
ORDER BY i.idInmueble;

SELECT
    h.IdPrecioHistorial,
    h.IdInmueble,
    h.PrecioAnterior,
    h.PrecioNuevo,
    h.Moneda,
    h.FechaCambioUtc,
    h.Origen,
    h.EsDatoConfiable,
    h.FechaRegistroUtc
FROM dbo.RSMAPS_InmueblePrecioHistorial h
WHERE h.IdInmueble IN (79,91,95,110,111,112,115,117,150,151,154)
  AND h.Origen = 'NORMALIZACION'
ORDER BY h.IdInmueble, h.IdPrecioHistorial;

SELECT
    COUNT(*) AS ContextosConfirmadosPaso48,
    CASE
        WHEN COUNT(*) = 11
         AND MIN(CASE WHEN c.EsDatoConfiable = 1 THEN 1 ELSE 0 END) = 1
        THEN 'OK'
        ELSE 'REVISAR'
    END AS EstadoPaso48
FROM dbo.RSMAPS_InmueblePrecioContexto c
WHERE c.IdInmueble IN (79,91,95,110,111,112,115,117,150,151,154);
