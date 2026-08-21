/* ============================================================
   RSMaps 2.0 - Paso 37
   DATOS ESTRUCTURADOS PARA FILTROS DEL MARKETPLACE

   Objetivo:
   - Mantener el marketplace en PUBLICADO + PUBLICO.
   - Exponer recamaras, banos, estacionamientos y amenidades sin
     exponer NotasPrivadas/contacto_a.
   - Preparar filtros precisos en el mapa sin cambiar estados.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','Recamaras') IS NULL
    THROW 53701, 'Falta Paso 35: Recamaras no existe.', 1;
IF OBJECT_ID('dbo.RSMAPS_Amenidad','U') IS NULL OR OBJECT_ID('dbo.RSMAPS_InmuebleAmenidad','U') IS NULL
    THROW 53702, 'Falta Paso 35: catalogo de amenidades.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListaInmuebles
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
        CAST(NULL AS varchar(max)) AS contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes,
        i.Recamaras,
        i.BanosCompletos,
        i.MediosBanos,
        i.Estacionamientos,
        i.Niveles,
        i.AntiguedadAnos,
        am.AmenidadesCsv
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm ON inm.idInmobiliaria = i.idInmobiliaria
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    OUTER APPLY
    (
        SELECT STRING_AGG(CONVERT(varchar(max), ia.AmenidadCodigo), ',')
               WITHIN GROUP (ORDER BY ia.AmenidadCodigo) AS AmenidadesCsv
        FROM dbo.RSMAPS_InmuebleAmenidad ia
        INNER JOIN dbo.RSMAPS_Amenidad a
            ON a.Codigo = ia.AmenidadCodigo
           AND a.Activo = 1
           AND a.EsFiltro = 1
        WHERE ia.IdInmueble = i.idInmueble
    ) am
    WHERE i.EstadoCodigo = 'PUBLICADO'
      AND i.VisibilidadCodigo = 'PUBLICO'
    ORDER BY i.idInmueble;
END;
GO

SELECT
    COUNT(*) AS TotalMarketplace,
    SUM(CASE WHEN Recamaras IS NOT NULL THEN 1 ELSE 0 END) AS ConRecamaras,
    SUM(CASE WHEN BanosCompletos IS NOT NULL THEN 1 ELSE 0 END) AS ConBanos,
    SUM(CASE WHEN Estacionamientos IS NOT NULL THEN 1 ELSE 0 END) AS ConEstacionamientos
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO';

SELECT
    a.Codigo,a.Nombre,a.Grupo,a.Orden
FROM dbo.RSMAPS_Amenidad a
WHERE a.Activo=1 AND a.EsFiltro=1
ORDER BY a.Grupo,a.Orden,a.Nombre;

SELECT TOP (10)
    p.idInmueble,p.idTipo,p.precio,p.Recamaras,p.BanosCompletos,
    p.Estacionamientos,p.AmenidadesCsv,
    'OK - MARKETPLACE ENTREGA DATOS DE FILTRO SIN NOTAS PRIVADAS' AS EstadoPaso37
FROM OPENQUERY([LOCALSERVER], 'SELECT 1') q
CROSS APPLY (SELECT TOP(0) 1 x) dummy;
-- La validacion real de forma se hace ejecutando el procedimiento abajo.
EXEC dbo.RSMAPS_sp_ListaInmuebles;
GO
