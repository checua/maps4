/* ============================================================
   RSMaps 2.0 - Paso 50
   LIMITE DE FOTOS POR INMUEBLE: 20 -> 40

   Objetivo:
   - Permitir migrar inventario legacy con mas de 20 fotos sin truncar.
   - Mantener la misma autorizacion y flujo de edicion del Paso 46.
   - Cambiar unicamente el limite de alta de fotos a 40.

   Este script:
   - NO inserta ni elimina fotos.
   - NO modifica inmuebles existentes.
   - NO cambia estado, visibilidad, precio ni contador de fotos.
   - Es idempotente.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 55000, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55001, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 55002, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL
    THROW 55003, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;

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

    DECLARE @actor INT,
            @cuenta INT,
            @rol VARCHAR(30),
            @ci INT,
            @responsable INT,
            @estado VARCHAR(20),
            @orden INT,
            @total INT,
            @portada BIT;

    SET @idImagen = NULL;

    IF @bytes IS NULL OR @bytes <= 0 OR @bytes > 12582912
        THROW 53220, 'El tamano de la imagen no es valido.', 1;
    IF @mimeType NOT IN ('image/jpeg','image/png','image/webp')
        THROW 53221, 'El formato de imagen no esta permitido.', 1;
    IF NULLIF(LTRIM(RTRIM(@claveAlmacenamiento)), N'') IS NULL
        THROW 53222, 'La clave de almacenamiento es obligatoria.', 1;

    SELECT @actor = idAsesor
    FROM dbo.RSMAPS_Usuario
    WHERE correo = @correo;

    SELECT TOP (1)
        @cuenta = cu.IdCuenta,
        @rol = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @actor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @cuenta IS NULL
       AND
       (
           SELECT COUNT(*)
           FROM dbo.RSMAPS_CuentaUsuario cu
           INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
           WHERE cu.IdAsesor = @actor
             AND cu.Activo = 1
             AND c.Activo = 1
       ) = 1
    BEGIN
        SELECT TOP (1)
            @cuenta = cu.IdCuenta,
            @rol = cu.RolCodigo
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @actor
          AND cu.Activo = 1
          AND c.Activo = 1
        ORDER BY cu.IdCuenta;
    END;

    SELECT
        @ci = IdCuenta,
        @responsable = idAsesor,
        @estado = EstadoCodigo
    FROM dbo.RSMAPS_Inmueble
    WHERE idInmueble = @idInmueble;

    IF @actor IS NULL OR @cuenta IS NULL
        THROW 53224, 'Sesion de trabajo invalida.', 1;
    IF @ci IS NULL
        THROW 53225, 'El inmueble no existe.', 1;
    IF @ci <> @cuenta
        THROW 53226, 'El inmueble pertenece a otra cuenta.', 1;
    IF @responsable <> @actor
        THROW 53227, 'Solo el asesor responsable puede administrar fotos.', 1;
    IF @estado NOT IN ('BORRADOR','PUBLICADO','PAUSADO','RETIRADO')
        THROW 53228, 'El inmueble no esta en un estado editable.', 1;
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p
            ON p.Codigo = rp.PermisoCodigo
           AND p.Activo = 1
        WHERE rp.RolCodigo = @rol
          AND rp.PermisoCodigo = 'INMUEBLE_EDITAR_PROPIO'
    )
        THROW 53229, 'Rol sin permiso para editar fotos.', 1;

    BEGIN TRANSACTION;

    SELECT @total = COUNT(*)
    FROM dbo.RSMAPS_InmuebleImagen WITH (UPDLOCK, HOLDLOCK)
    WHERE IdInmueble = @idInmueble
      AND Activo = 1;

    IF @total >= 40
        THROW 53230, 'El inmueble ya tiene el maximo de 40 fotos.', 1;

    SELECT @orden = ISNULL(MAX(Orden), 0) + 1
    FROM dbo.RSMAPS_InmuebleImagen
    WHERE IdInmueble = @idInmueble
      AND Activo = 1;

    SET @portada = CASE WHEN @total = 0 THEN 1 ELSE 0 END;

    INSERT dbo.RSMAPS_InmuebleImagen
    (
        IdInmueble,
        IdCuenta,
        ClaveAlmacenamiento,
        NombreOriginal,
        MimeType,
        Bytes,
        Orden,
        EsPortada,
        Activo
    )
    VALUES
    (
        @idInmueble,
        @cuenta,
        @claveAlmacenamiento,
        NULLIF(@nombreOriginal, N''),
        @mimeType,
        @bytes,
        @orden,
        @portada,
        1
    );

    SET @idImagen = SCOPE_IDENTITY();
    SET @total = @total + 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_InmuebleImagenes
        WHERE idInmueble = @idInmueble
    )
        UPDATE dbo.RSMAPS_InmuebleImagenes
        SET Imagenes = @total
        WHERE idInmueble = @idInmueble;
    ELSE
        INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
        VALUES (@idInmueble, @total);

    UPDATE dbo.RSMAPS_Inmueble
    SET FechaUltimaEdicionUtc = SYSUTCDATETIME()
    WHERE idInmueble = @idInmueble;

    COMMIT TRANSACTION;
END;
GO

DECLARE @Definicion nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.RSMAPS_sp_RegistrarFotoBorrador'));
DECLARE @Normalizada nvarchar(max) =
    REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(@Definicion, N''), N' ', N''), CHAR(9), N''), CHAR(13), N''), CHAR(10), N'');

SELECT
    OBJECT_ID(N'dbo.RSMAPS_sp_RegistrarFotoBorrador', N'P') AS IdProcedimiento,
    CASE WHEN @Normalizada LIKE N'%IF@total>=40THROW53230%' THEN 40 ELSE NULL END AS LimiteFotosDetectado,
    CASE
        WHEN OBJECT_ID(N'dbo.RSMAPS_sp_RegistrarFotoBorrador', N'P') IS NOT NULL
         AND @Normalizada LIKE N'%IF@total>=40THROW53230%'
        THEN 'OK - LIMITE DE 40 FOTOS ACTIVO'
        ELSE 'ERROR - REVISAR PROCEDIMIENTO'
    END AS EstadoPaso50;
