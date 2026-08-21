/* ============================================================
   RSMaps 2.0 - Paso 34B
   LIMPIAR PRUEBA E2E DE PUBLICACION

   Ejecutar SOLO despues de terminar la validacion Web/publica del Paso 34A.
   Elimina exclusivamente la propiedad marcada para esta prueba.
   No elimina archivos fisicos: las fotos temporales reutilizan archivos del #176.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53550, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @Direccion varchar(max) = 'PRUEBA E2E PUBLICACION - NO ES PROPIEDAD REAL';
DECLARE @NotaPrivada nvarchar(max) = N'[RSMAPS-TEST-PUBLISH-E2E] DATO PRIVADO: NO DEBE APARECER EN EL MARKETPLACE.';
DECLARE @Precio decimal(18,2) = 1987654.00;
DECLARE @Ids TABLE (IdInmueble int PRIMARY KEY);

INSERT @Ids(IdInmueble)
SELECT i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE CONVERT(varchar(max),i.direccion)=@Direccion
  AND CONVERT(nvarchar(max),i.NotasPrivadas)=@NotaPrivada
  AND TRY_CONVERT(decimal(18,2),i.precio)=@Precio;

IF NOT EXISTS(SELECT 1 FROM @Ids)
BEGIN
    SELECT 'OK - NO HAY PRUEBA E2E DE PUBLICACION POR LIMPIAR' AS EstadoLimpieza;
    RETURN;
END;

IF (SELECT COUNT(*) FROM @Ids) <> 1
    THROW 53551, 'Seguridad: se esperaba exactamente una propiedad temporal de publicacion. Limpieza cancelada.', 1;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor AS IdAsesorResponsable,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    TRY_CONVERT(decimal(18,2),i.precio) AS PrecioActual,
    i.FechaPublicacionUtc,
    i.direccion,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=i.idInmueble) AS FotosModernas,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble o WHERE o.IdInmueble=i.idInmueble) AS Operaciones,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado h WHERE h.IdInmueble=i.idInmueble) AS CambiosEstado,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial p WHERE p.IdInmueble=i.idInmueble) AS CambiosPrecio
FROM dbo.RSMAPS_Inmueble i
INNER JOIN @Ids x ON x.IdInmueble=i.idInmueble;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE o
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN @Ids x ON x.IdInmueble=o.IdInmueble;

    DELETE h
    FROM dbo.RSMAPS_InmuebleCambioEstado h
    INNER JOIN @Ids x ON x.IdInmueble=h.IdInmueble;

    DELETE p
    FROM dbo.RSMAPS_InmueblePrecioHistorial p
    INNER JOIN @Ids x ON x.IdInmueble=p.IdInmueble;

    DELETE f
    FROM dbo.RSMAPS_InmuebleImagen f
    INNER JOIN @Ids x ON x.IdInmueble=f.IdInmueble;

    DELETE li
    FROM dbo.RSMAPS_InmuebleImagenes li
    INNER JOIN @Ids x ON x.IdInmueble=li.idInmueble;

    DELETE i
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN @Ids x ON x.IdInmueble=i.idInmueble
    WHERE CONVERT(varchar(max),i.direccion)=@Direccion
      AND CONVERT(nvarchar(max),i.NotasPrivadas)=@NotaPrivada
      AND TRY_CONVERT(decimal(18,2),i.precio)=@Precio;

    IF @@ROWCOUNT <> 1
        THROW 53552, 'Seguridad: no se elimino exactamente una propiedad temporal. Transaccion cancelada.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble i
     WHERE CONVERT(varchar(max),i.direccion)=@Direccion
       AND CONVERT(nvarchar(max),i.NotasPrivadas)=@NotaPrivada
       AND TRY_CONVERT(decimal(18,2),i.precio)=@Precio) AS InmueblesPruebaRestantes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble o INNER JOIN @Ids x ON x.IdInmueble=o.IdInmueble) AS OperacionesPruebaRestantes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado h INNER JOIN @Ids x ON x.IdInmueble=h.IdInmueble) AS HistorialEstadoPruebaRestante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial p INNER JOIN @Ids x ON x.IdInmueble=p.IdInmueble) AS HistorialPrecioPruebaRestante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f INNER JOIN @Ids x ON x.IdInmueble=f.IdInmueble) AS FotosMetadataPruebaRestantes,
    'OK - PRUEBA E2E DE PUBLICACION LIMPIADA COMPLETAMENTE' AS EstadoLimpieza;
