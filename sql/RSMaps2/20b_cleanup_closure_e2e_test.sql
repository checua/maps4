/* ============================================================
   RSMaps 2.0 - Paso 20B
   LIMPIAR INMUEBLE TEMPORAL DE PRUEBA E2E DE CIERRE

   Ejecutar SOLO después de validar el cierre desde la interfaz.
   Elimina únicamente la propiedad exacta creada por Paso 20A.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52050, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @Marcador nvarchar(100) = N'[RSMAPS-TEST-CIERRE-E2E]';
DECLARE @DireccionPrueba varchar(max) = 'PRUEBA E2E CIERRE - NO ES PROPIEDAD REAL';
DECLARE @Ids TABLE (IdInmueble int PRIMARY KEY);

/* Buscar exclusivamente la propiedad creada por 20A.
   CHARINDEX trata los corchetes del marcador como texto literal. */
INSERT @Ids (IdInmueble)
SELECT i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE CHARINDEX(@Marcador, CONVERT(nvarchar(max), i.observaciones)) > 0
  AND CONVERT(varchar(max), i.direccion) = @DireccionPrueba
  AND CONVERT(varchar(max), i.link) = 'TEST-E2E'
  AND CONVERT(varchar(max), i.contacto_a) = 'PRUEBA CONTROLADA';

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    SELECT 'OK - NO HAY PROPIEDAD TEMPORAL E2E POR LIMPIAR' AS EstadoLimpieza;
    RETURN;
END;

IF (SELECT COUNT(*) FROM @Ids) <> 1
    THROW 52051, 'Seguridad: se esperaba exactamente una propiedad temporal E2E. Limpieza cancelada.', 1;

SELECT
    i.idInmueble,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioActual,
    i.direccion,
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
    WHERE CHARINDEX(@Marcador, CONVERT(nvarchar(max), i.observaciones)) > 0
      AND CONVERT(varchar(max), i.direccion) = @DireccionPrueba
      AND CONVERT(varchar(max), i.link) = 'TEST-E2E'
      AND CONVERT(varchar(max), i.contacto_a) = 'PRUEBA CONTROLADA';

    IF @@ROWCOUNT <> 1
        THROW 52052, 'Seguridad: no se eliminó exactamente una propiedad temporal. Transacción cancelada.', 1;

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
     WHERE CHARINDEX(@Marcador, CONVERT(nvarchar(max), observaciones)) > 0
       AND CONVERT(varchar(max), direccion) = @DireccionPrueba
       AND CONVERT(varchar(max), link) = 'TEST-E2E'
       AND CONVERT(varchar(max), contacto_a) = 'PRUEBA CONTROLADA') AS InmueblesPruebaRestantes,
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
