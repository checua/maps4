/* ============================================================
   RSMaps 2.0 - Paso 49
   PILOTO DE MIGRACION DE FOTOS LEGACY -> METADATA MODERNA
   Inmueble: 87

   Precondicion fisica:
   - Debe haberse ejecutado tools/Prepare-RSMapsLegacyPhotoPilot87.ps1
   - Deben existir 18 archivos en App_Data/RSMapsImages/87
     con nombres 87_1.jpg ... 87_18.jpg.

   Objetivo:
   - Registrar metadata moderna para las 18 fotos legacy del inmueble 87.
   - Conservar intactos los archivos legacy de wwwroot/Cargas.
   - Conservar el contador legacy en 18 por compatibilidad.
   - Preservar orden 1..18 y establecer 87_1.jpg como portada.
   - Ser idempotente: una segunda ejecucion valida y no duplica.

   IMPORTANTE:
   - Este script NO mueve ni elimina archivos fisicos.
   - Este script NO cambia precio, estado ni visibilidad del inmueble.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 54900, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 54901, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 54902, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL
    THROW 54903, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;

DECLARE @IdInmueble int = 87;
DECLARE @Esperadas int = 18;
DECLARE @IdCuenta int;
DECLARE @Estado varchar(20);
DECLARE @Visibilidad varchar(20);
DECLARE @Legacy int;

SELECT
    @IdCuenta = i.IdCuenta,
    @Estado = i.EstadoCodigo,
    @Visibilidad = i.VisibilidadCodigo
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueble;

IF @IdCuenta IS NULL
    THROW 54904, 'El inmueble 87 no existe.', 1;
IF @IdCuenta <> 1
    THROW 54905, 'Seguridad: el inmueble 87 ya no pertenece a la cuenta esperada.', 1;
IF @Estado <> 'PUBLICADO'
    THROW 54906, 'Seguridad: el inmueble 87 ya no esta PUBLICADO. Revisar antes de migrar.', 1;

SELECT @Legacy = ISNULL(MAX(ii.Imagenes), 0)
FROM dbo.RSMAPS_InmuebleImagenes ii
WHERE ii.idInmueble = @IdInmueble;

IF @Legacy <> @Esperadas
    THROW 54907, 'El contador legacy del inmueble 87 ya no es 18. Revisar antes de migrar.', 1;

DECLARE @Esperado TABLE
(
    Orden int NOT NULL PRIMARY KEY,
    Nombre nvarchar(255) NOT NULL,
    Clave nvarchar(500) NOT NULL,
    Bytes bigint NOT NULL
);

INSERT @Esperado (Orden, Nombre, Clave, Bytes)
VALUES
(1,  N'87_1.jpg',  N'87/87_1.jpg',  113500),
(2,  N'87_2.jpg',  N'87/87_2.jpg',   88837),
(3,  N'87_3.jpg',  N'87/87_3.jpg',   55474),
(4,  N'87_4.jpg',  N'87/87_4.jpg',   80432),
(5,  N'87_5.jpg',  N'87/87_5.jpg',   33484),
(6,  N'87_6.jpg',  N'87/87_6.jpg',   89301),
(7,  N'87_7.jpg',  N'87/87_7.jpg',   47977),
(8,  N'87_8.jpg',  N'87/87_8.jpg',   93105),
(9,  N'87_9.jpg',  N'87/87_9.jpg',   41683),
(10, N'87_10.jpg', N'87/87_10.jpg',  53493),
(11, N'87_11.jpg', N'87/87_11.jpg',  47690),
(12, N'87_12.jpg', N'87/87_12.jpg',  73924),
(13, N'87_13.jpg', N'87/87_13.jpg',  60903),
(14, N'87_14.jpg', N'87/87_14.jpg',  56223),
(15, N'87_15.jpg', N'87/87_15.jpg',  74217),
(16, N'87_16.jpg', N'87/87_16.jpg',  92945),
(17, N'87_17.jpg', N'87/87_17.jpg',  37547),
(18, N'87_18.jpg', N'87/87_18.jpg',  47122);

DECLARE @TotalActual int =
(
    SELECT COUNT(*)
    FROM dbo.RSMAPS_InmuebleImagen
    WHERE IdInmueble = @IdInmueble
);

/* ------------------------------------------------------------
   Idempotencia: si ya existen exactamente las 18 filas esperadas,
   validar y terminar sin insertar duplicados.
   ------------------------------------------------------------ */
IF @TotalActual > 0
BEGIN
    IF @TotalActual <> @Esperadas
        THROW 54908, 'Ya existe metadata moderna parcial o adicional para el inmueble 87. No se modifica nada.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @Esperado e
        LEFT JOIN dbo.RSMAPS_InmuebleImagen f
          ON f.IdInmueble = @IdInmueble
         AND f.Orden = e.Orden
         AND f.ClaveAlmacenamiento = e.Clave
         AND f.NombreOriginal = e.Nombre
         AND f.MimeType = 'image/jpeg'
         AND f.Bytes = e.Bytes
         AND f.Activo = 1
         AND f.EsPortada = CASE WHEN e.Orden = 1 THEN 1 ELSE 0 END
        WHERE f.IdImagen IS NULL
    )
        THROW 54909, 'La metadata moderna existente no coincide exactamente con el piloto esperado.', 1;

    SELECT
        f.IdImagen,
        f.IdInmueble,
        f.Orden,
        f.EsPortada,
        f.ClaveAlmacenamiento,
        f.NombreOriginal,
        f.Bytes,
        f.Activo
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @IdInmueble
    ORDER BY f.Orden;

    SELECT
        @Legacy AS FotosLegacy,
        @TotalActual AS FotosModernas,
        (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@IdInmueble AND Activo=1 AND EsPortada=1) AS PortadasModernas,
        'OK - PILOTO 87 YA ESTABA MIGRADO' AS EstadoPaso49;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

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
        Activo,
        FechaAltaUtc
    )
    SELECT
        @IdInmueble,
        @IdCuenta,
        e.Clave,
        e.Nombre,
        'image/jpeg',
        e.Bytes,
        e.Orden,
        CASE WHEN e.Orden = 1 THEN 1 ELSE 0 END,
        1,
        SYSUTCDATETIME()
    FROM @Esperado e
    ORDER BY e.Orden;

    /* Mantener contador legacy sincronizado. */
    UPDATE dbo.RSMAPS_InmuebleImagenes
    SET Imagenes = @Esperadas
    WHERE idInmueble = @IdInmueble;

    IF @@ROWCOUNT = 0
        INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
        VALUES (@IdInmueble, @Esperadas);

    DECLARE @Modernas int =
    (
        SELECT COUNT(*)
        FROM dbo.RSMAPS_InmuebleImagen
        WHERE IdInmueble = @IdInmueble AND Activo = 1
    );

    DECLARE @Portadas int =
    (
        SELECT COUNT(*)
        FROM dbo.RSMAPS_InmuebleImagen
        WHERE IdInmueble = @IdInmueble AND Activo = 1 AND EsPortada = 1
    );

    IF @Modernas <> @Esperadas
        THROW 54910, 'Verificacion fallida: no quedaron 18 fotos modernas activas.', 1;
    IF @Portadas <> 1
        THROW 54911, 'Verificacion fallida: debe existir exactamente una portada moderna.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @Esperado e
        LEFT JOIN dbo.RSMAPS_InmuebleImagen f
          ON f.IdInmueble = @IdInmueble
         AND f.Orden = e.Orden
         AND f.ClaveAlmacenamiento = e.Clave
         AND f.NombreOriginal = e.Nombre
         AND f.MimeType = 'image/jpeg'
         AND f.Bytes = e.Bytes
         AND f.Activo = 1
         AND f.EsPortada = CASE WHEN e.Orden = 1 THEN 1 ELSE 0 END
        WHERE f.IdImagen IS NULL
    )
        THROW 54912, 'Verificacion fallida: la metadata insertada no coincide con el manifiesto esperado.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    f.IdImagen,
    f.IdInmueble,
    f.Orden,
    f.EsPortada,
    f.ClaveAlmacenamiento,
    f.NombreOriginal,
    f.Bytes,
    f.Activo
FROM dbo.RSMAPS_InmuebleImagen f
WHERE f.IdInmueble = @IdInmueble
ORDER BY f.Orden;

SELECT
    (SELECT MAX(ii.Imagenes) FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=@IdInmueble) AS FotosLegacy,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@IdInmueble AND Activo=1) AS FotosModernas,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@IdInmueble AND Activo=1 AND EsPortada=1) AS PortadasModernas,
    'OK - PILOTO 87 MIGRADO A METADATA MODERNA' AS EstadoPaso49;
