/* ============================================================
   RSMaps 2.0 - Paso 05
   PRUEBAS CONTROLADAS DE EDICION Y AUTORIZACION

   Base esperada: mapsMarkers

   Objetivo:
   1. Probar que un usuario con membresia valida puede editar un inmueble
      de su Cuenta mediante RSMAPS_sp_insertar_coordenadas.
   2. Hacer ROLLBACK y confirmar que el inmueble queda exactamente como
      estaba antes de la prueba.
   3. Confirmar que un usuario sin Cuenta activa no puede escribir.

   IMPORTANTE:
   - NO deja cambios persistentes.
   - La prueba positiva usa el inmueble 1 y su asesor actual.
   - La prueba negativa usa danielortega@gmail.com, usuario legado sin Cuenta.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble = 1)
    THROW 50501, 'No existe el inmueble 1 requerido para esta prueba.', 1;

DECLARE
    @IdInmueble int = 1,
    @Correo varchar(200),
    @IdTipo int,
    @Terreno float,
    @Construccion float,
    @PrecioOriginal float,
    @PrecioPrueba float,
    @ObservacionesOriginal varchar(max),
    @ContactoOriginal varchar(max),
    @IdCuentaOriginal int,
    @IdAsesorOriginal int;

SELECT
    @Correo = u.correo,
    @IdTipo = i.idTipo,
    @Terreno = i.terreno,
    @Construccion = i.construccion,
    @PrecioOriginal = i.precio,
    @ObservacionesOriginal = i.observaciones,
    @ContactoOriginal = i.contacto_a,
    @IdCuentaOriginal = i.IdCuenta,
    @IdAsesorOriginal = i.idAsesor
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = i.idAsesor
WHERE i.idInmueble = @IdInmueble;

IF @Correo IS NULL
    THROW 50502, 'No se pudo resolver el correo del asesor del inmueble 1.', 1;

SET @PrecioPrueba = ISNULL(@PrecioOriginal, 0) + 1;

/* ============================================================
   PRUEBA A - EDICION VALIDA + ROLLBACK
   ============================================================ */
BEGIN TRANSACTION;

DECLARE @IdSalida int = @IdInmueble;

EXEC dbo.RSMAPS_sp_insertar_coordenadas
    @idInmueble = @IdSalida OUTPUT,
    @correo = @Correo,
    @idTipo = @IdTipo,
    @terreno = @Terreno,
    @construccion = @Construccion,
    @precio = @PrecioPrueba,
    @observaciones = 'PRUEBA PASO 05 - DEBE HACER ROLLBACK',
    @contacto = @ContactoOriginal;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    u.correo,
    i.precio AS PrecioDurantePrueba,
    i.observaciones AS ObservacionesDurantePrueba,
    CASE
        WHEN i.IdCuenta = @IdCuentaOriginal
         AND i.idAsesor = @IdAsesorOriginal
         AND i.precio = @PrecioPrueba
         AND i.observaciones = 'PRUEBA PASO 05 - DEBE HACER ROLLBACK'
        THEN 'OK - EDICION VALIDA'
        ELSE 'REVISAR'
    END AS EstadoEdicion
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = i.idAsesor
WHERE i.idInmueble = @IdInmueble;

ROLLBACK TRANSACTION;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.precio AS PrecioDespuesRollback,
    i.observaciones AS ObservacionesDespuesRollback,
    CASE
        WHEN i.IdCuenta = @IdCuentaOriginal
         AND i.idAsesor = @IdAsesorOriginal
         AND ((i.precio = @PrecioOriginal) OR (i.precio IS NULL AND @PrecioOriginal IS NULL))
         AND ((i.observaciones = @ObservacionesOriginal) OR (i.observaciones IS NULL AND @ObservacionesOriginal IS NULL))
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueble;

/* ============================================================
   PRUEBA B - USUARIO SIN CUENTA DEBE SER RECHAZADO
   No se abre transaccion porque el procedimiento debe fallar antes
   de cualquier UPDATE.
   ============================================================ */
DECLARE @IdSalidaNoAutorizado int = @IdInmueble;
DECLARE @ResultadoBloqueo varchar(100) = 'REVISAR - NO HUBO ERROR';
DECLARE @NumeroError int = NULL;
DECLARE @MensajeError nvarchar(2048) = NULL;

BEGIN TRY
    EXEC dbo.RSMAPS_sp_insertar_coordenadas
        @idInmueble = @IdSalidaNoAutorizado OUTPUT,
        @correo = 'danielortega@gmail.com',
        @idTipo = @IdTipo,
        @terreno = @Terreno,
        @construccion = @Construccion,
        @precio = @PrecioOriginal,
        @observaciones = @ObservacionesOriginal,
        @contacto = @ContactoOriginal;
END TRY
BEGIN CATCH
    SET @NumeroError = ERROR_NUMBER();
    SET @MensajeError = ERROR_MESSAGE();

    IF @NumeroError = 50321
        SET @ResultadoBloqueo = 'OK - USUARIO SIN CUENTA BLOQUEADO';
    ELSE
        SET @ResultadoBloqueo = 'REVISAR - ERROR DISTINTO';
END CATCH;

SELECT
    @ResultadoBloqueo AS EstadoAutorizacion,
    @NumeroError AS NumeroError,
    @MensajeError AS MensajeError;

/* Confirmacion final de que el inmueble 1 sigue intacto. */
SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.precio,
    i.observaciones,
    CASE
        WHEN i.IdCuenta = @IdCuentaOriginal
         AND i.idAsesor = @IdAsesorOriginal
         AND ((i.precio = @PrecioOriginal) OR (i.precio IS NULL AND @PrecioOriginal IS NULL))
         AND ((i.observaciones = @ObservacionesOriginal) OR (i.observaciones IS NULL AND @ObservacionesOriginal IS NULL))
        THEN 'OK - DATOS ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueble;
