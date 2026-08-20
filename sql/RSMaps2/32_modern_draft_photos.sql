/* ============================================================
   RSMaps 2.0 - Paso 32
   FOTOS MODERNAS PARA BORRADORES

   Objetivo:
   - Registrar cada imagen como entidad individual.
   - Mantener orden y portada.
   - Mantener sincronizado RSMAPS_InmuebleImagenes.Imagenes para compatibilidad.
   - Mantener las fotos privadas mientras el inmueble sea BORRADOR.
   - Autorizar lectura por propietario o permiso de inventario de cuenta.
   - Autorizar alta/eliminacion/portada solo al asesor responsable por ahora.
   - Preparar almacenamiento desacoplado para migrar despues a Azure Blob.

   La prueba final usa ROLLBACK y no requiere archivos fisicos.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 53201, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    THROW 53202, 'No existe dbo.RSMAPS_Cuenta.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 53203, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL
    THROW 53204, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;

/* ============================================================
   1. Tabla moderna por imagen
   ============================================================ */
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_InmuebleImagen
    (
        IdImagen BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_RSMAPS_InmuebleImagen PRIMARY KEY,
        IdInmueble INT NOT NULL,
        IdCuenta INT NOT NULL,
        ClaveAlmacenamiento NVARCHAR(500) NOT NULL,
        NombreOriginal NVARCHAR(255) NULL,
        MimeType VARCHAR(100) NOT NULL,
        Bytes BIGINT NOT NULL,
        Orden INT NOT NULL,
        EsPortada BIT NOT NULL
            CONSTRAINT DF_RSMAPS_InmuebleImagen_EsPortada DEFAULT (0),
        Activo BIT NOT NULL
            CONSTRAINT DF_RSMAPS_InmuebleImagen_Activo DEFAULT (1),
        FechaAltaUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_InmuebleImagen_FechaAlta DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_RSMAPS_InmuebleImagen_Inmueble
            FOREIGN KEY (IdInmueble) REFERENCES dbo.RSMAPS_Inmueble(idInmueble),
        CONSTRAINT FK_RSMAPS_InmuebleImagen_Cuenta
            FOREIGN KEY (IdCuenta) REFERENCES dbo.RSMAPS_Cuenta(IdCuenta),
        CONSTRAINT CK_RSMAPS_InmuebleImagen_Bytes CHECK (Bytes > 0),
        CONSTRAINT CK_RSMAPS_InmuebleImagen_Orden CHECK (Orden > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
      AND name = N'IX_RSMAPS_InmuebleImagen_InmuebleOrden'
)
    CREATE INDEX IX_RSMAPS_InmuebleImagen_InmuebleOrden
        ON dbo.RSMAPS_InmuebleImagen (IdInmueble, Activo, Orden);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
      AND name = N'UX_RSMAPS_InmuebleImagen_Portada'
)
    CREATE UNIQUE INDEX UX_RSMAPS_InmuebleImagen_Portada
        ON dbo.RSMAPS_InmuebleImagen (IdInmueble)
        WHERE EsPortada = 1 AND Activo = 1;
GO

/* ============================================================
   2. Registrar foto de un borrador propio
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_RegistrarFotoBorrador
    @correo VARCHAR(200),
    @idInmueble INT,
    @claveAlmacenamiento NVARCHAR(500),
    @nombreOriginal NVARCHAR(255) = NULL,
    @mimeType VARCHAR(100),
    @bytes BIGINT,
    @idImagen BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @IdCuentaInmueble INT;
    DECLARE @IdAsesorInmueble INT;
    DECLARE @Estado VARCHAR(20);
    DECLARE @PuedeEditar BIT = 0;
    DECLARE @Orden INT;
    DECLARE @Total INT;
    DECLARE @EsPortada BIT;

    SET @idImagen = NULL;

    IF @bytes IS NULL OR @bytes <= 0 OR @bytes > 12582912
        THROW 53220, 'El tamaño de la imagen no es valido.', 1;
    IF @mimeType NOT IN ('image/jpeg','image/png','image/webp')
        THROW 53221, 'El formato de imagen no esta permitido.', 1;
    IF NULLIF(LTRIM(RTRIM(@claveAlmacenamiento)), N'') IS NULL
        THROW 53222, 'La clave de almacenamiento es obligatoria.', 1;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53223, 'No existe el usuario autenticado.', 1;

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
        THROW 53224, 'El usuario no pertenece a una cuenta activa.', 1;

    SELECT
        @IdCuentaInmueble = i.IdCuenta,
        @IdAsesorInmueble = i.idAsesor,
        @Estado = i.EstadoCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @IdCuentaInmueble IS NULL
        THROW 53225, 'El inmueble no existe.', 1;
    IF @IdCuentaInmueble <> @IdCuenta
        THROW 53226, 'El inmueble pertenece a otra cuenta.', 1;
    IF @IdAsesorInmueble <> @IdAsesor
        THROW 53227, 'Por ahora solo el asesor responsable puede administrar fotos del borrador.', 1;
    IF @Estado <> 'BORRADOR'
        THROW 53228, 'Las fotos de este flujo solo pueden modificarse mientras el inmueble sea borrador.', 1;

    SELECT @PuedeEditar = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo AND p.Activo = 1
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'INMUEBLE_EDITAR_BORRADOR_PROPIO'
    ) THEN 1 ELSE 0 END;

    IF @PuedeEditar = 0
        THROW 53229, 'El rol actual no puede editar borradores.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Total = COUNT(*)
        FROM dbo.RSMAPS_InmuebleImagen WITH (UPDLOCK, HOLDLOCK)
        WHERE IdInmueble = @idInmueble AND Activo = 1;

        IF @Total >= 20
            THROW 53230, 'El borrador ya tiene el maximo de 20 fotos.', 1;

        SELECT @Orden = ISNULL(MAX(Orden), 0) + 1
        FROM dbo.RSMAPS_InmuebleImagen
        WHERE IdInmueble = @idInmueble AND Activo = 1;

        SET @EsPortada = CASE WHEN @Total = 0 THEN 1 ELSE 0 END;

        INSERT dbo.RSMAPS_InmuebleImagen
        (
            IdInmueble, IdCuenta, ClaveAlmacenamiento, NombreOriginal,
            MimeType, Bytes, Orden, EsPortada, Activo
        )
        VALUES
        (
            @idInmueble, @IdCuenta, @claveAlmacenamiento, NULLIF(@nombreOriginal, N''),
            @mimeType, @bytes, @Orden, @EsPortada, 1
        );

        SET @idImagen = SCOPE_IDENTITY();
        SET @Total = @Total + 1;

        IF EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @idInmueble)
            UPDATE dbo.RSMAPS_InmuebleImagenes SET Imagenes = @Total WHERE idInmueble = @idInmueble;
        ELSE
            INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes) VALUES (@idInmueble, @Total);

        IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimaEdicionUtc') IS NOT NULL
            UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc = SYSUTCDATETIME() WHERE idInmueble = @idInmueble;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ============================================================
   3. Leer una foto privada autorizada
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerFotoPrivada
    @correo VARCHAR(200),
    @idImagen BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @PuedeVerCuenta BIT = 0;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53240, 'No existe el usuario autenticado.', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor AND cu.Activo = 1 AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdCuenta IS NULL
        THROW 53241, 'El usuario no pertenece a una cuenta activa.', 1;

    SELECT @PuedeVerCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo AND p.Activo = 1
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'INVENTARIO_VER_CUENTA'
    ) THEN 1 ELSE 0 END;

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
    INNER JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = f.IdInmueble
    WHERE f.IdImagen = @idImagen
      AND f.Activo = 1
      AND f.IdCuenta = @IdCuenta
      AND (i.idAsesor = @IdAsesor OR @PuedeVerCuenta = 1);
END;
GO

/* ============================================================
   4. Elegir portada de borrador propio
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_EstablecerPortadaBorrador
    @correo VARCHAR(200),
    @idInmueble INT,
    @idImagen BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @Estado VARCHAR(20);

    SELECT @IdAsesor = u.idAsesor FROM dbo.RSMAPS_Usuario u WHERE u.correo = @correo;
    SELECT TOP (1) @IdCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor AND cu.Activo = 1 AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    SELECT @Estado = i.EstadoCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble
      AND i.IdCuenta = @IdCuenta
      AND i.idAsesor = @IdAsesor;

    IF @Estado IS NULL THROW 53250, 'No tienes acceso al borrador.', 1;
    IF @Estado <> 'BORRADOR' THROW 53251, 'La portada solo puede cambiarse desde este flujo mientras sea borrador.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagen WHERE IdImagen = @idImagen AND IdInmueble = @idInmueble AND Activo = 1)
        THROW 53252, 'La imagen no pertenece al borrador.', 1;

    BEGIN TRANSACTION;
    UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada = 0 WHERE IdInmueble = @idInmueble AND Activo = 1;
    UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada = 1 WHERE IdImagen = @idImagen AND IdInmueble = @idInmueble AND Activo = 1;
    COMMIT TRANSACTION;
END;
GO

/* ============================================================
   5. Eliminar foto de borrador propio (soft delete de metadata)
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_EliminarFotoBorrador
    @correo VARCHAR(200),
    @idInmueble INT,
    @idImagen BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @Estado VARCHAR(20);
    DECLARE @Clave NVARCHAR(500);
    DECLARE @EraPortada BIT;
    DECLARE @Total INT;

    SELECT @IdAsesor = u.idAsesor FROM dbo.RSMAPS_Usuario u WHERE u.correo = @correo;
    SELECT TOP (1) @IdCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor AND cu.Activo = 1 AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    SELECT @Estado = i.EstadoCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble
      AND i.IdCuenta = @IdCuenta
      AND i.idAsesor = @IdAsesor;

    IF @Estado IS NULL THROW 53260, 'No tienes acceso al borrador.', 1;
    IF @Estado <> 'BORRADOR' THROW 53261, 'Las fotos solo pueden eliminarse desde este flujo mientras sea borrador.', 1;

    SELECT @Clave = f.ClaveAlmacenamiento, @EraPortada = f.EsPortada
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdImagen = @idImagen AND f.IdInmueble = @idInmueble AND f.Activo = 1;

    IF @Clave IS NULL THROW 53262, 'La imagen no existe o ya fue eliminada.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.RSMAPS_InmuebleImagen
        SET Activo = 0, EsPortada = 0
        WHERE IdImagen = @idImagen AND IdInmueble = @idInmueble AND Activo = 1;

        IF @EraPortada = 1
        BEGIN
            DECLARE @NuevaPortada BIGINT;
            SELECT TOP (1) @NuevaPortada = IdImagen
            FROM dbo.RSMAPS_InmuebleImagen
            WHERE IdInmueble = @idInmueble AND Activo = 1
            ORDER BY Orden, IdImagen;

            IF @NuevaPortada IS NOT NULL
                UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada = 1 WHERE IdImagen = @NuevaPortada;
        END;

        SELECT @Total = COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = @idInmueble AND Activo = 1;

        IF EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @idInmueble)
            UPDATE dbo.RSMAPS_InmuebleImagenes SET Imagenes = @Total WHERE idInmueble = @idInmueble;
        ELSE
            INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes) VALUES (@idInmueble, @Total);

        IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimaEdicionUtc') IS NOT NULL
            UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc = SYSUTCDATETIME() WHERE idInmueble = @idInmueble;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @Clave AS ClaveAlmacenamiento;
END;
GO

/* ============================================================
   6. Prueba de metadata con rollback sobre un borrador real existente
   ============================================================ */
DECLARE @CorreoPrueba VARCHAR(200) = 'profesor76@hotmail.com';
DECLARE @IdBorrador INT;
DECLARE @IdImagenPrueba BIGINT;
DECLARE @ConteoAntes INT;
DECLARE @ConteoDurante INT;
DECLARE @LegacyAntes INT;
DECLARE @LegacyDurante INT;

SELECT TOP (1) @IdBorrador = i.idInmueble
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
WHERE u.correo = @CorreoPrueba
  AND i.EstadoCodigo = 'BORRADOR'
  AND i.VisibilidadCodigo = 'CUENTA'
ORDER BY i.idInmueble;

IF @IdBorrador IS NULL
    THROW 53280, 'No existe un borrador propio para ejecutar la prueba controlada.', 1;

SELECT @ConteoAntes = COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = @IdBorrador AND Activo = 1;
SELECT @LegacyAntes = ISNULL(MAX(Imagenes),0) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdBorrador;

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_RegistrarFotoBorrador
    @correo = @CorreoPrueba,
    @idInmueble = @IdBorrador,
    @claveAlmacenamiento = N'PRUEBA/rollback.jpg',
    @nombreOriginal = N'rollback.jpg',
    @mimeType = 'image/jpeg',
    @bytes = 12345,
    @idImagen = @IdImagenPrueba OUTPUT;

SELECT @ConteoDurante = COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = @IdBorrador AND Activo = 1;
SELECT @LegacyDurante = ISNULL(MAX(Imagenes),0) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdBorrador;

SELECT
    @IdBorrador AS IdBorrador,
    @IdImagenPrueba AS IdImagenPrueba,
    @ConteoAntes AS ModernasAntes,
    @ConteoDurante AS ModernasDurante,
    @LegacyAntes AS LegacyAntes,
    @LegacyDurante AS LegacyDurante,
    CASE WHEN @ConteoDurante = @ConteoAntes + 1 AND @LegacyDurante = @LegacyAntes + 1
         THEN 'OK - FOTO MODERNA REGISTRADA Y CONTEO LEGACY SINCRONIZADO'
         ELSE 'REVISAR' END AS EstadoPrueba;

ROLLBACK TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = @IdBorrador AND Activo = 1) AS ModernasDespues,
    ISNULL((SELECT MAX(Imagenes) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdBorrador),0) AS LegacyDespues,
    CASE WHEN (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = @IdBorrador AND Activo = 1) = @ConteoAntes
              AND ISNULL((SELECT MAX(Imagenes) FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @IdBorrador),0) = @LegacyAntes
         THEN 'OK - ROLLBACK COMPLETO'
         ELSE 'REVISAR' END AS EstadoRollback;

SELECT
    c.name AS Columna,
    t.name AS TipoDato,
    c.max_length AS LongitudBytes,
    c.is_nullable AS PermiteNull
FROM sys.columns c
INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
ORDER BY c.column_id;
