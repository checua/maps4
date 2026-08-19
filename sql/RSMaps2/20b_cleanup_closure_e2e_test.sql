/* ============================================================
   RSMaps 2.0 - Paso 20B
   LIMPIAR INMUEBLE TEMPORAL DE PRUEBA E2E DE CIERRE

   Ejecutar SOLO después de validar el cierre desde la interfaz.
   Elimina únicamente registros identificados por el marcador de prueba.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52050, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @Marcador nvarchar(100) = N'[RSMAPS-TEST-CIERRE-E2E]';
DECLARE @Ids TABLE (IdInmueble int PRIMARY KEY);

INSERT @Ids (IdInmueble)
SELECT i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE CONVERT(nvarchar(max), i.observaciones) LIKE N'%' + @Marcador + N'%';

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    SELECT 'OK - NO HAY PROPIEDADES TEMPORALES E2E POR LIMPIAR' AS EstadoLimpieza;
    RETURN;
END;

SELECT
    i.idInmueble,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble o WHERE o.IdInmueble = i.idInmueble) AS Operaciones,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado h WHERE h.IdInmueble = i.idInmueble) AS CambiosEstado,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial p WHERE p.IdInmueble = i.idInmueble) AS CambiosPrecio
FROM dbo.RSMAPS_Inmueble i
INNER JOIN @Ids x ON x.IdInmueble = i.idInmueble;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE o
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN @Ids x ON x.IdInmueble = o.IdInmueble;

    DELETE h
    FROM dbo.RSMAPS_InmuebleCambioEstado h
    INNER JOIN @Ids x ON x.IdInmueble = h.IdInmueble;

    DELETE p
    FROM dbo.RSMAPS_InmueblePrecioHistorial p
    INNER JOIN @Ids x ON x.IdInmueble = p.IdInmueble;

    DELETE img
    FROM dbo.RSMAPS_InmuebleImagenes img
    INNER JOIN @Ids x ON x.IdInmueble = img.idInmueble;

    DELETE i
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN @Ids x ON x.IdInmueble = i.idInmueble
    WHERE CONVERT(nvarchar(max), i.observaciones) LIKE N'%' + @Marcador + N'%';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_Inmueble
     WHERE CONVERT(nvarchar(max), observaciones) LIKE N'%' + @Marcador + N'%') AS InmueblesPruebaRestantes,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_OperacionInmueble o
     INNER JOIN @Ids x ON x.IdInmueble = o.IdInmueble) AS OperacionesPruebaRestantes,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmuebleCambioEstado h
     INNER JOIN @Ids x ON x.IdInmueble = h.IdInmueble) AS HistorialEstadoPruebaRestante,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmueblePrecioHistorial p
     INNER JOIN @Ids x ON x.IdInmueble = p.IdInmueble) AS HistorialPrecioPruebaRestante,
    'OK - PRUEBA E2E LIMPIADA COMPLETAMENTE' AS EstadoLimpieza;
