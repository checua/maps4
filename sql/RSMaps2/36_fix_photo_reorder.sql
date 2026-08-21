/* ============================================================
   RSMaps 2.0 - Paso 36
   CORREGIR REORDENAMIENTO DE FOTOS MODERNAS

   Objetivo:
   - Evitar colisiones al intercambiar Orden entre dos fotos.
   - Usar un orden temporal positivo y único dentro del inmueble.
   - Mantener el cambio limitado a BORRADOR propio.
   - Probar sobre un borrador existente con ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53600, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 53601, 'Falta dbo.RSMAPS_InmuebleImagen. Ejecutar primero Paso 32.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_MoverFotoBorradorWeb
    @correo VARCHAR(200),
    @idInmueble INT,
    @idImagen BIGINT,
    @direccion INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @Estado VARCHAR(20);
    DECLARE @OrdenActual INT;
    DECLARE @IdImagenDestino BIGINT;
    DECLARE @OrdenDestino INT;
    DECLARE @OrdenTemporal INT;

    IF @direccion NOT IN (-1, 1)
        THROW 53320, 'La dirección de movimiento debe ser -1 o 1.', 1;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53310, 'No existe el usuario autenticado.', 1;

    SELECT TOP (1) @IdCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdCuenta IS NULL
        THROW 53311, 'El usuario no pertenece a una cuenta activa.', 1;

    SELECT @Estado = i.EstadoCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble
      AND i.IdCuenta = @IdCuenta
      AND i.idAsesor = @IdAsesor;

    IF @Estado IS NULL
        THROW 53321, 'No tienes acceso para ordenar fotos de este inmueble.', 1;
    IF @Estado <> 'BORRADOR'
        THROW 53322, 'El orden solo puede editarse desde este flujo mientras sea borrador.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @OrdenActual = f.Orden
        FROM dbo.RSMAPS_InmuebleImagen f WITH (UPDLOCK, HOLDLOCK)
        WHERE f.IdImagen = @idImagen
          AND f.IdInmueble = @idInmueble
          AND f.Activo = 1;

        IF @OrdenActual IS NULL
            THROW 53323, 'La imagen no existe o no pertenece al borrador.', 1;

        IF @direccion = -1
        BEGIN
            SELECT TOP (1)
                @IdImagenDestino = f.IdImagen,
                @OrdenDestino = f.Orden
            FROM dbo.RSMAPS_InmuebleImagen f WITH (UPDLOCK, HOLDLOCK)
            WHERE f.IdInmueble = @idInmueble
              AND f.Activo = 1
              AND f.Orden < @OrdenActual
            ORDER BY f.Orden DESC, f.IdImagen DESC;
        END
        ELSE
        BEGIN
            SELECT TOP (1)
                @IdImagenDestino = f.IdImagen,
                @OrdenDestino = f.Orden
            FROM dbo.RSMAPS_InmuebleImagen f WITH (UPDLOCK, HOLDLOCK)
            WHERE f.IdInmueble = @idInmueble
              AND f.Activo = 1
              AND f.Orden > @OrdenActual
            ORDER BY f.Orden, f.IdImagen;
        END;

        IF @IdImagenDestino IS NULL
        BEGIN
            COMMIT TRANSACTION;
            RETURN;
        END;

        SELECT @OrdenTemporal = ISNULL(MAX(f.Orden), 0) + 1000
        FROM dbo.RSMAPS_InmuebleImagen f WITH (UPDLOCK, HOLDLOCK)
        WHERE f.IdInmueble = @idInmueble
          AND f.Activo = 1;

        /* Intercambio seguro en tres pasos. */
        UPDATE dbo.RSMAPS_InmuebleImagen
        SET Orden = @OrdenTemporal
        WHERE IdImagen = @idImagen;

        UPDATE dbo.RSMAPS_InmuebleImagen
        SET Orden = @OrdenActual
        WHERE IdImagen = @IdImagenDestino;

        UPDATE dbo.RSMAPS_InmuebleImagen
        SET Orden = @OrdenDestino
        WHERE IdImagen = @idImagen;

        IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimaEdicionUtc') IS NOT NULL
            UPDATE dbo.RSMAPS_Inmueble
            SET FechaUltimaEdicionUtc = SYSUTCDATETIME()
            WHERE idInmueble = @idInmueble;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ============================================================
   Prueba controlada con ROLLBACK sobre un borrador con >= 2 fotos.
   ============================================================ */
DECLARE @IdPrueba INT;
DECLARE @Correo VARCHAR(200);
DECLARE @IdImagen BIGINT;

SELECT TOP (1)
    @IdPrueba = i.idInmueble,
    @Correo = u.correo
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
WHERE i.EstadoCodigo = 'BORRADOR'
  AND EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_InmuebleImagen f
      WHERE f.IdInmueble = i.idInmueble AND f.Activo = 1
      GROUP BY f.IdInmueble
      HAVING COUNT(*) >= 2
  )
ORDER BY CASE WHEN i.idInmueble = 176 THEN 0 ELSE 1 END, i.idInmueble;

IF @IdPrueba IS NOT NULL
BEGIN
    SELECT TOP (1) @IdImagen = f.IdImagen
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @IdPrueba AND f.Activo = 1
    ORDER BY f.Orden, f.IdImagen;

    BEGIN TRANSACTION;

    SELECT
        f.IdImagen,
        f.Orden,
        f.EsPortada,
        'ANTES' AS Momento
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @IdPrueba AND f.Activo = 1
    ORDER BY f.Orden, f.IdImagen;

    EXEC dbo.RSMAPS_sp_MoverFotoBorradorWeb
        @correo = @Correo,
        @idInmueble = @IdPrueba,
        @idImagen = @IdImagen,
        @direccion = 1;

    SELECT
        f.IdImagen,
        f.Orden,
        f.EsPortada,
        'DURANTE' AS Momento
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @IdPrueba AND f.Activo = 1
    ORDER BY f.Orden, f.IdImagen;

    ROLLBACK TRANSACTION;

    SELECT
        @IdPrueba AS IdInmueblePrueba,
        CASE WHEN @@TRANCOUNT = 0 THEN 'OK - REORDENAMIENTO PROBADO Y ROLLBACK COMPLETO' ELSE 'REVISAR' END AS EstadoPrueba;
END
ELSE
BEGIN
    SELECT
        CAST(NULL AS INT) AS IdInmueblePrueba,
        'OK - PROCEDIMIENTO ACTUALIZADO; NO HAY BORRADOR CON 2 FOTOS PARA PRUEBA AUTOMATICA' AS EstadoPrueba;
END;
GO
