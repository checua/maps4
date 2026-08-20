/* ============================================================
   RSMaps 2.0 - Paso 32B
   FLUJO WEB DE FOTOS PRIVADAS: LISTADO Y ORDEN

   Requiere Paso 32.
   - Lista fotos modernas con autorización vigente.
   - Permite mover fotos dentro de un BORRADOR propio.
   - Mantiene las fotos privadas; no publica archivos ni inmueble.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 53301, 'Falta dbo.RSMAPS_InmuebleImagen. Ejecutar primero Paso 32.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListarFotosBorradorWeb
    @correo VARCHAR(200),
    @idInmueble INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @IdCuentaInmueble INT;
    DECLARE @IdAsesorInmueble INT;
    DECLARE @PuedeVerCuenta BIT = 0;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53310, 'No existe el usuario autenticado.', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdCuenta IS NULL
        THROW 53311, 'El usuario no pertenece a una cuenta activa.', 1;

    SELECT
        @IdCuentaInmueble = i.IdCuenta,
        @IdAsesorInmueble = i.idAsesor
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @IdCuentaInmueble IS NULL
        THROW 53312, 'El inmueble no existe.', 1;
    IF @IdCuentaInmueble <> @IdCuenta
        THROW 53313, 'El inmueble pertenece a otra cuenta.', 1;

    SELECT @PuedeVerCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p
            ON p.Codigo = rp.PermisoCodigo
           AND p.Activo = 1
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'INVENTARIO_VER_CUENTA'
    ) THEN 1 ELSE 0 END;

    IF @IdAsesorInmueble <> @IdAsesor AND @PuedeVerCuenta = 0
        THROW 53314, 'No tienes permiso para ver las fotos de este inmueble.', 1;

    SELECT
        f.IdImagen,
        f.IdInmueble,
        f.ClaveAlmacenamiento,
        f.NombreOriginal,
        f.MimeType,
        f.Bytes,
        f.Orden,
        f.EsPortada,
        f.FechaAltaUtc
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @idInmueble
      AND f.Activo = 1
    ORDER BY f.Orden, f.IdImagen;
END;
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

    IF @direccion NOT IN (-1, 1)
        THROW 53320, 'La dirección de movimiento debe ser -1 o 1.', 1;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    SELECT TOP (1) @IdCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    SELECT @Estado = i.EstadoCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble
      AND i.IdCuenta = @IdCuenta
      AND i.idAsesor = @IdAsesor;

    IF @Estado IS NULL
        THROW 53321, 'No tienes acceso para ordenar fotos de este inmueble.', 1;
    IF @Estado <> 'BORRADOR'
        THROW 53322, 'El orden solo puede editarse desde este flujo mientras sea borrador.', 1;

    SELECT @OrdenActual = f.Orden
    FROM dbo.RSMAPS_InmuebleImagen f
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
        FROM dbo.RSMAPS_InmuebleImagen f
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
        FROM dbo.RSMAPS_InmuebleImagen f
        WHERE f.IdInmueble = @idInmueble
          AND f.Activo = 1
          AND f.Orden > @OrdenActual
        ORDER BY f.Orden, f.IdImagen;
    END;

    IF @IdImagenDestino IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE dbo.RSMAPS_InmuebleImagen
    SET Orden = @OrdenDestino
    WHERE IdImagen = @idImagen;

    UPDATE dbo.RSMAPS_InmuebleImagen
    SET Orden = @OrdenActual
    WHERE IdImagen = @IdImagenDestino;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimaEdicionUtc') IS NOT NULL
        UPDATE dbo.RSMAPS_Inmueble
        SET FechaUltimaEdicionUtc = SYSUTCDATETIME()
        WHERE idInmueble = @idInmueble;

    COMMIT TRANSACTION;
END;
GO

SELECT
    OBJECT_ID(N'dbo.RSMAPS_sp_ListarFotosBorradorWeb', N'P') AS ProcListar,
    OBJECT_ID(N'dbo.RSMAPS_sp_MoverFotoBorradorWeb', N'P') AS ProcMover,
    CASE
        WHEN OBJECT_ID(N'dbo.RSMAPS_sp_ListarFotosBorradorWeb', N'P') IS NOT NULL
         AND OBJECT_ID(N'dbo.RSMAPS_sp_MoverFotoBorradorWeb', N'P') IS NOT NULL
        THEN 'OK - FLUJO WEB DE FOTOS LISTO'
        ELSE 'REVISAR'
    END AS EstadoPaso32B;
