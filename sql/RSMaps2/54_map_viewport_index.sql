/* ============================================================
   RSMaps 2.0 - Paso 54
   INDICE PARA CARGA DEL MARKETPLACE POR VIEWPORT

   Objetivo:
   - Acelerar consultas PUBLICADO + PUBLICO limitadas por lat/lng.
   - No cambia datos ni estados de inmuebles.
   - El codigo funciona sin este indice; este paso es de rendimiento.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 55400, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55401, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF COL_LENGTH('dbo.RSMAPS_Inmueble', 'EstadoCodigo') IS NULL
   OR COL_LENGTH('dbo.RSMAPS_Inmueble', 'VisibilidadCodigo') IS NULL
   OR COL_LENGTH('dbo.RSMAPS_Inmueble', 'lat') IS NULL
   OR COL_LENGTH('dbo.RSMAPS_Inmueble', 'lng') IS NULL
    THROW 55402, 'Faltan columnas requeridas para el viewport.', 1;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
      AND name = N'IX_RSMAPS_Inmueble_PublicViewport'
)
BEGIN
    CREATE INDEX IX_RSMAPS_Inmueble_PublicViewport
        ON dbo.RSMAPS_Inmueble
        (
            EstadoCodigo,
            VisibilidadCodigo,
            lat,
            lng
        )
        INCLUDE
        (
            idInmueble,
            idAsesor,
            idTipo,
            precio
        );
END;
GO

SELECT
    i.name AS Indice,
    i.type_desc AS Tipo,
    i.is_disabled AS Deshabilitado
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
  AND i.name = N'IX_RSMAPS_Inmueble_PublicViewport';

SELECT 'OK - INDICE DE VIEWPORT DISPONIBLE' AS EstadoPaso54;
GO
