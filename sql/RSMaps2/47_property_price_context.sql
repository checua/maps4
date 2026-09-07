/* ============================================================
   RSMaps 2.0 - Paso 47
   CONTEXTO DE PRECIO POR INMUEBLE

   Objetivo:
   - Mantener compatibilidad con RSMAPS_Inmueble.precio (FLOAT legacy).
   - Registrar por separado moneda y periodicidad del precio.
   - Evitar que RADAR compare como equivalentes importes que pertenecen
     a distinta moneda o distinta periodicidad (ej. diario vs mensual).
   - No reescribir ni reinterpretar automaticamente el historial legacy.

   Principios:
   - Si no conocemos moneda o periodicidad con certeza, se deja NULL.
   - RSMAPS_InmueblePrecioHistorial conserva su valor historico actual.
     Los registros MIGRACION con EsDatoConfiable = 0 no se modifican.
   - Este paso SOLO crea estructura y catalogos. No corrige precios ni
     asigna contexto a inmuebles existentes.
   - El script es idempotente.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 54700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 54701, 'No existe dbo.RSMAPS_Inmueble.', 1;

/* ------------------------------------------------------------
   1. Catalogo de monedas
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_Moneda', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_Moneda
    (
        Codigo      char(3) NOT NULL,
        Nombre      nvarchar(80) NOT NULL,
        Activo      bit NOT NULL
            CONSTRAINT DF_RSMAPS_Moneda_Activo DEFAULT (1),
        CONSTRAINT PK_RSMAPS_Moneda PRIMARY KEY (Codigo)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Moneda WHERE Codigo = 'MXN')
    INSERT dbo.RSMAPS_Moneda (Codigo, Nombre) VALUES ('MXN', N'Peso mexicano');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Moneda WHERE Codigo = 'USD')
    INSERT dbo.RSMAPS_Moneda (Codigo, Nombre) VALUES ('USD', N'Dolar estadounidense');

/* ------------------------------------------------------------
   2. Catalogo de periodicidad del precio
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_FrecuenciaPrecio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_FrecuenciaPrecio
    (
        Codigo      varchar(20) NOT NULL,
        Nombre      nvarchar(80) NOT NULL,
        Activo      bit NOT NULL
            CONSTRAINT DF_RSMAPS_FrecuenciaPrecio_Activo DEFAULT (1),
        CONSTRAINT PK_RSMAPS_FrecuenciaPrecio PRIMARY KEY (Codigo)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'UNICO')
    INSERT dbo.RSMAPS_FrecuenciaPrecio (Codigo, Nombre) VALUES ('UNICO', N'Precio unico de venta');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'MENSUAL')
    INSERT dbo.RSMAPS_FrecuenciaPrecio (Codigo, Nombre) VALUES ('MENSUAL', N'Renta mensual');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'SEMANAL')
    INSERT dbo.RSMAPS_FrecuenciaPrecio (Codigo, Nombre) VALUES ('SEMANAL', N'Renta semanal');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'DIARIO')
    INSERT dbo.RSMAPS_FrecuenciaPrecio (Codigo, Nombre) VALUES ('DIARIO', N'Renta diaria / por noche');

/* ------------------------------------------------------------
   3. Contexto monetario por inmueble

   Ausencia de fila o columnas NULL significa: dato no confirmado.
   EsDatoConfiable indica que el contexto fue confirmado por una fuente
   confiable (usuario/aplicacion) y no solo inferido desde texto legacy.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioContexto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_InmueblePrecioContexto
    (
        IdInmueble               int NOT NULL,
        MonedaCodigo             char(3) NULL,
        FrecuenciaPrecioCodigo   varchar(20) NULL,
        EsDatoConfiable          bit NOT NULL
            CONSTRAINT DF_RSMAPS_InmueblePrecioContexto_Confiable DEFAULT (0),
        Origen                   varchar(30) NOT NULL
            CONSTRAINT DF_RSMAPS_InmueblePrecioContexto_Origen DEFAULT ('MIGRACION'),
        Motivo                   nvarchar(500) NULL,
        FechaActualizacionUtc    datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_InmueblePrecioContexto_Fecha DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_RSMAPS_InmueblePrecioContexto PRIMARY KEY (IdInmueble),
        CONSTRAINT FK_RSMAPS_InmueblePrecioContexto_Inmueble
            FOREIGN KEY (IdInmueble)
            REFERENCES dbo.RSMAPS_Inmueble(idInmueble),
        CONSTRAINT FK_RSMAPS_InmueblePrecioContexto_Moneda
            FOREIGN KEY (MonedaCodigo)
            REFERENCES dbo.RSMAPS_Moneda(Codigo),
        CONSTRAINT FK_RSMAPS_InmueblePrecioContexto_Frecuencia
            FOREIGN KEY (FrecuenciaPrecioCodigo)
            REFERENCES dbo.RSMAPS_FrecuenciaPrecio(Codigo),
        CONSTRAINT CK_RSMAPS_InmueblePrecioContexto_Origen
            CHECK (Origen IN ('MIGRACION','USUARIO','APLICACION','INFERIDO','NORMALIZACION'))
    );
END;

/* ------------------------------------------------------------
   4. Indices para consultas de inventario / RADAR
   ------------------------------------------------------------ */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioContexto')
      AND name = N'IX_RSMAPS_InmueblePrecioContexto_Moneda_Frecuencia'
)
BEGIN
    CREATE INDEX IX_RSMAPS_InmueblePrecioContexto_Moneda_Frecuencia
        ON dbo.RSMAPS_InmueblePrecioContexto(MonedaCodigo, FrecuenciaPrecioCodigo)
        INCLUDE (EsDatoConfiable, Origen);
END;

/* ------------------------------------------------------------
   5. Verificacion final
   ------------------------------------------------------------ */
SELECT
    m.Codigo,
    m.Nombre,
    m.Activo
FROM dbo.RSMAPS_Moneda m
ORDER BY m.Codigo;

SELECT
    f.Codigo,
    f.Nombre,
    f.Activo
FROM dbo.RSMAPS_FrecuenciaPrecio f
ORDER BY f.Codigo;

SELECT
    OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioContexto', N'U') AS IdTablaContexto,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioContexto) AS ContextosRegistrados,
    CASE
        WHEN OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioContexto', N'U') IS NOT NULL
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_Moneda WHERE Codigo = 'MXN')
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_Moneda WHERE Codigo = 'USD')
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'UNICO')
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'MENSUAL')
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'SEMANAL')
         AND EXISTS (SELECT 1 FROM dbo.RSMAPS_FrecuenciaPrecio WHERE Codigo = 'DIARIO')
        THEN 'OK'
        ELSE 'ERROR'
    END AS EstadoPaso47;
