/* ============================================================
   RSMaps 2.0 - Paso 26B
   LIMPIAR INMUEBLE TEMPORAL DE PRUEBA E2E ADMINISTRATIVA

   Ejecutar SOLO despues de validar la prueba administrativa desde UI.
   Elimina unicamente la propiedad exacta creada por Paso 26A.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52650, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @Marcador nvarchar(120) = N'[RSMAPS-TEST-ADMIN-E2E]';
DECLARE @DireccionPrueba varchar(max) = 'PRUEBA E2E ADMIN - NO ES PROPIEDAD REAL';
DECLARE @CorreoActor varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdActor int;
DECLARE @IdCuenta int;
DECLARE @RolActual varchar(30);
DECLARE @Ids TABLE (IdInmueble int PRIMARY KEY);

SELECT @IdActor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoActor;

IF @IdActor IS NULL
    THROW 52651, 'No existe el usuario actor configurado para la prueba.', 1;

SELECT TOP (1)
    @IdCuenta = cu.IdCuenta,
    @RolActual = cu.RolCodigo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdActor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @IdCuenta IS NULL
    THROW 52652, 'El usuario actor no pertenece a una cuenta activa.', 1;

/* Como proteccion operativa, primero devolver al usuario a ASESOR mediante 22B. */
IF @RolActual <> 'ASESOR'
    THROW 52653, 'Antes de limpiar, restaura el rol del usuario con Paso 22B.', 1;

INSERT @Ids (IdInmueble)
SELECT i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE i.IdCuenta = @IdCuenta
  AND CHARINDEX(@Marcador, CONVERT(nvarchar(max), i.observaciones)) > 0
  AND CONVERT(varchar(max), i.direccion) = @DireccionPrueba
  AND CONVERT(varchar(max), i.link) = 'TEST-ADMIN-E2E'
  AND CONVERT(varchar(max), i.contacto_a) = 'PRUEBA ADMINISTRATIVA';

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    SELECT 'OK - NO HAY PROPIEDAD TEMPORAL ADMIN E2E POR LIMPIAR' AS EstadoLimpieza;
    RETURN;
END;

IF (SELECT COUNT(*) FROM @Ids) <> 1
    THROW 52654, 'Seguridad: se esperaba exactamente una propiedad temporal ADMIN E2E. Limpieza cancelada.', 1;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor AS IdAsesorResponsable,
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
    WHERE i.IdCuenta = @IdCuenta
      AND CHARINDEX(@Marcador, CONVERT(nvarchar(max), i.observaciones)) > 0
      AND CONVERT(varchar(max), i.direccion) = @DireccionPrueba
      AND CONVERT(varchar(max), i.link) = 'TEST-ADMIN-E2E'
      AND CONVERT(varchar(max), i.contacto_a) = 'PRUEBA ADMINISTRATIVA';

    IF @@ROWCOUNT <> 1
        THROW 52655, 'Seguridad: no se elimino exactamente una propiedad temporal. Transaccion cancelada.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_Inmueble
     WHERE IdCuenta = @IdCuenta
       AND CHARINDEX(@Marcador, CONVERT(nvarchar(max), observaciones)) > 0
       AND CONVERT(varchar(max), direccion) = @DireccionPrueba
       AND CONVERT(varchar(max), link) = 'TEST-ADMIN-E2E'
       AND CONVERT(varchar(max), contacto_a) = 'PRUEBA ADMINISTRATIVA') AS InmueblesPruebaRestantes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble o INNER JOIN @Ids x ON x.IdInmueble = o.IdInmueble) AS OperacionesPruebaRestantes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado h INNER JOIN @Ids x ON x.IdInmueble = h.IdInmueble) AS HistorialEstadoPruebaRestante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial p INNER JOIN @Ids x ON x.IdInmueble = p.IdInmueble) AS HistorialPrecioPruebaRestante,
    'OK - PRUEBA ADMIN E2E LIMPIADA COMPLETAMENTE' AS EstadoLimpieza;
