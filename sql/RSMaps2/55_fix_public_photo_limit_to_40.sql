/* ============================================================
   RSMaps 2.0 - Paso 55
   ALINEAR LIMITE PUBLICO DE FOTOS: 20 -> 40

   Causa corregida:
   - El Paso 50 permite hasta 40 fotos activas por inmueble.
   - RSMAPS_sp_ObtenerFotoPublicaPorOrden conservaba un limite
     legacy de 20 posiciones.
   - Las fotos 21..40 existian correctamente en SQL y Azure Blob,
     pero /cargas/{id}_{orden}.jpg devolvia HTTP 404.

   Este script:
   - NO inserta, elimina ni modifica fotografias.
   - NO modifica metadata de imagenes.
   - NO modifica inmuebles.
   - Mantiene la validacion PUBLICADO / PUBLICO.
   - Alinea la lectura publica con el limite actual de 40 fotos.
   - Es idempotente.
   - El guard de base de datos protege tambien la redefinicion
     del procedimiento porque todo el cambio se ejecuta en el
     mismo batch mediante SQL dinamico.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'mapsMarkers'
    THROW 55500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55501, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 55502, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;

DECLARE @Sql NVARCHAR(MAX) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden
    @idInmueble INT,
    @orden INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @orden < 1 OR @orden > 40
        RETURN;

    ;WITH f AS
    (
        SELECT
            im.IdImagen,
            im.IdInmueble,
            im.ClaveAlmacenamiento,
            im.NombreOriginal,
            im.MimeType,
            im.Bytes,
            im.Orden,
            im.EsPortada,
            im.FechaAltaUtc,
            ROW_NUMBER() OVER
            (
                ORDER BY
                    im.EsPortada DESC,
                    im.Orden,
                    im.IdImagen
            ) AS Posicion
        FROM dbo.RSMAPS_InmuebleImagen im
        INNER JOIN dbo.RSMAPS_Inmueble i
            ON i.idInmueble = im.IdInmueble
        WHERE
            im.IdInmueble = @idInmueble
            AND im.Activo = 1
            AND i.EstadoCodigo = ''PUBLICADO''
            AND i.VisibilidadCodigo = ''PUBLICO''
    )
    SELECT
        IdImagen,
        IdInmueble,
        ClaveAlmacenamiento,
        NombreOriginal,
        MimeType,
        Bytes,
        Orden,
        EsPortada,
        FechaAltaUtc
    FROM f
    WHERE Posicion = @orden;
END;
';

EXEC sys.sp_executesql @Sql;

/* Verificacion deterministica del Paso 55. */
DECLARE @Definicion NVARCHAR(MAX) =
    OBJECT_DEFINITION(
        OBJECT_ID(N'dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden')
    );

DECLARE @Normalizada NVARCHAR(MAX) =
    REPLACE(
        REPLACE(
            REPLACE(
                REPLACE(
                    ISNULL(@Definicion, N''),
                    N' ',
                    N''
                ),
                CHAR(9),
                N''
            ),
            CHAR(13),
            N''
        ),
        CHAR(10),
        N''
    );

SELECT
    OBJECT_ID(
        N'dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden',
        N'P'
    ) AS IdProcedimiento,
    CASE
        WHEN @Normalizada LIKE N'%IF@orden<1OR@orden>40RETURN%'
        THEN 40
        ELSE NULL
    END AS LimitePublicoDetectado,
    CASE
        WHEN OBJECT_ID(
                 N'dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden',
                 N'P'
             ) IS NOT NULL
         AND @Normalizada LIKE N'%IF@orden<1OR@orden>40RETURN%'
        THEN 'OK - LIMITE PUBLICO DE 40 FOTOS ACTIVO'
        ELSE 'ERROR - REVISAR PROCEDIMIENTO'
    END AS EstadoPaso55;
