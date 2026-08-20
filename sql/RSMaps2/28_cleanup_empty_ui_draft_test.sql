/* ============================================================
   RSMaps 2.0 - Paso 28
   LIMPIAR BORRADOR VACIO CREADO DESDE LA UI DURANTE PRUEBA

   USO:
   - Cambiar @IdInmueblePrueba por el ID exacto creado desde el flujo nuevo.
   - El script se niega a borrar si el inmueble ya fue completado, publicado,
     tiene precio, fotos, operación, historial de precio o más actividad.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52800, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @IdInmueblePrueba int = 0; -- REEMPLAZAR POR EL ID DEL BORRADOR DE PRUEBA
DECLARE @CorreoPrueba varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdAsesor int;
DECLARE @IdCuenta int;

IF @IdInmueblePrueba <= 0
    THROW 52801, 'Configura @IdInmueblePrueba antes de ejecutar la limpieza.', 1;

SELECT @IdAsesor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoPrueba;

IF @IdAsesor IS NULL
    THROW 52802, 'No existe el usuario configurado para la prueba.', 1;

SELECT TOP (1) @IdCuenta = cu.IdCuenta
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @IdCuenta IS NULL
    THROW 52803, 'No fue posible resolver la cuenta activa del usuario de prueba.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @IdInmueblePrueba
      AND i.IdCuenta = @IdCuenta
      AND i.idAsesor = @IdAsesor
      AND i.EstadoCodigo = 'BORRADOR'
      AND i.VisibilidadCodigo = 'CUENTA'
      AND TRY_CONVERT(decimal(18,2), i.precio) = 0
      AND ISNULL(i.terreno, 0) = 0
      AND ISNULL(i.construccion, 0) = 0
      AND i.FechaPublicacionUtc IS NULL
      AND CONVERT(varchar(max), i.direccion) = 'Ubicacion registrada en mapa'
      AND CONVERT(varchar(max), i.link) = 'BORRADOR'
)
    THROW 52804, 'Seguridad: el inmueble ya no coincide con un borrador vacio de prueba. Limpieza cancelada.', 1;

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba)
    THROW 52805, 'Seguridad: el borrador tiene una operación registrada. Limpieza cancelada.', 1;

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble = @IdInmueblePrueba)
    THROW 52806, 'Seguridad: el borrador ya tiene historial de precio. Limpieza cancelada.', 1;

IF ISNULL((SELECT MAX(Imagenes) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdInmueblePrueba), 0) <> 0
    THROW 52807, 'Seguridad: el borrador ya tiene fotos. Limpieza cancelada.', 1;

IF (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) <> 1
    THROW 52808, 'Seguridad: el borrador ya tiene actividad adicional. Limpieza cancelada.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_InmuebleCambioEstado h
    WHERE h.IdInmueble = @IdInmueblePrueba
      AND h.EstadoAnterior IS NULL
      AND h.EstadoNuevo = 'BORRADOR'
      AND h.VisibilidadAnterior IS NULL
      AND h.VisibilidadNueva = 'CUENTA'
      AND h.IdAsesorResponsable = @IdAsesor
      AND h.IdAsesorCambio = @IdAsesor
      AND h.Origen = 'APLICACION'
)
    THROW 52809, 'Seguridad: el historial inicial no coincide con un borrador creado por este usuario. Limpieza cancelada.', 1;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.precio,
    i.lat,
    i.lng,
    'BORRADOR VACIO APTO PARA LIMPIEZA' AS EstadoPrevio
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueblePrueba;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM dbo.RSMAPS_InmuebleCambioEstado
    WHERE IdInmueble = @IdInmueblePrueba;

    DELETE FROM dbo.RSMAPS_InmuebleImagenes
    WHERE idInmueble = @IdInmueblePrueba;

    DELETE FROM dbo.RSMAPS_Inmueble
    WHERE idInmueble = @IdInmueblePrueba
      AND IdCuenta = @IdCuenta
      AND idAsesor = @IdAsesor
      AND EstadoCodigo = 'BORRADOR'
      AND VisibilidadCodigo = 'CUENTA';

    IF @@ROWCOUNT <> 1
        THROW 52810, 'Seguridad: no se eliminó exactamente un borrador. Transacción cancelada.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE idInmueble = @IdInmueblePrueba) AS InmuebleRestante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) AS HistorialRestante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdInmueblePrueba) AS ImagenesRestantes,
    CASE
        WHEN NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble = @IdInmueblePrueba)
         AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba)
         AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdInmueblePrueba)
        THEN 'OK - BORRADOR DE PRUEBA LIMPIADO COMPLETAMENTE'
        ELSE 'REVISAR'
    END AS EstadoLimpieza;
