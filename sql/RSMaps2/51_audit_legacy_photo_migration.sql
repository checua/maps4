/* ============================================================
   RSMaps 2.0 - Paso 51
   AUDITORIA PREVIA A MIGRACION MASIVA DE FOTOS LEGACY

   Objetivo:
   - Comparar contador legacy contra metadata moderna por inmueble.
   - Identificar candidatos seguros para migracion.
   - Separar inventario principal, historico y cuentas aisladas/prueba.
   - Detectar cualquier diferencia antes de copiar o insertar fotos.

   IMPORTANTE:
   - SOLO LECTURA.
   - NO inserta, actualiza ni elimina datos.
   - NO modifica archivos fisicos.
   ============================================================ */

SET NOCOUNT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 55100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55101, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL
    THROW 55102, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 55103, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;

;WITH Legacy AS
(
    SELECT
        ii.idInmueble,
        MAX(ISNULL(ii.Imagenes, 0)) AS FotosLegacy
    FROM dbo.RSMAPS_InmuebleImagenes ii
    GROUP BY ii.idInmueble
),
Modernas AS
(
    SELECT
        f.IdInmueble,
        COUNT(*) AS MetadataModernasTotal,
        SUM(CASE WHEN f.Activo = 1 THEN 1 ELSE 0 END) AS FotosModernasActivas,
        SUM(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 ELSE 0 END) AS PortadasActivas
    FROM dbo.RSMAPS_InmuebleImagen f
    GROUP BY f.IdInmueble
),
Auditoria AS
(
    SELECT
        i.idInmueble AS IdInmueble,
        i.IdCuenta,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        ISNULL(l.FotosLegacy, 0) AS FotosLegacy,
        ISNULL(m.MetadataModernasTotal, 0) AS MetadataModernasTotal,
        ISNULL(m.FotosModernasActivas, 0) AS FotosModernasActivas,
        ISNULL(m.PortadasActivas, 0) AS PortadasActivas,
        CASE
            WHEN i.IdCuenta <> 1 THEN 'AISLADO_OTRA_CUENTA'
            WHEN i.EstadoCodigo IN ('VENDIDO','RENTADO') THEN 'HISTORICO'
            WHEN i.EstadoCodigo IN ('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THEN 'INVENTARIO_PRINCIPAL'
            ELSE 'REVISAR_ESTADO'
        END AS GrupoMigracion,
        CASE
            WHEN ISNULL(l.FotosLegacy, 0) = 0 AND ISNULL(m.FotosModernasActivas, 0) = 0
                THEN 'SIN_FOTOS'
            WHEN ISNULL(l.FotosLegacy, 0) > 40
                THEN 'REVISAR_EXCEDE_40'
            WHEN ISNULL(l.FotosLegacy, 0) > 0 AND ISNULL(m.FotosModernasActivas, 0) = 0
                THEN 'PENDIENTE_MIGRAR'
            WHEN ISNULL(l.FotosLegacy, 0) = ISNULL(m.FotosModernasActivas, 0)
                 AND ISNULL(l.FotosLegacy, 0) > 0
                 AND ISNULL(m.PortadasActivas, 0) = 1
                THEN 'YA_MODERNO_OK'
            WHEN ISNULL(m.FotosModernasActivas, 0) > 0
                THEN 'REVISAR_DIFERENCIA'
            ELSE 'REVISAR'
        END AS EstadoMigracion
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN Legacy l ON l.idInmueble = i.idInmueble
    LEFT JOIN Modernas m ON m.IdInmueble = i.idInmueble
)
SELECT
    IdInmueble,
    IdCuenta,
    EstadoCodigo,
    VisibilidadCodigo,
    FotosLegacy,
    FotosModernasActivas,
    MetadataModernasTotal,
    PortadasActivas,
    GrupoMigracion,
    EstadoMigracion
FROM Auditoria
WHERE FotosLegacy > 0 OR FotosModernasActivas > 0 OR MetadataModernasTotal > 0
ORDER BY
    CASE GrupoMigracion
        WHEN 'INVENTARIO_PRINCIPAL' THEN 1
        WHEN 'HISTORICO' THEN 2
        WHEN 'AISLADO_OTRA_CUENTA' THEN 3
        ELSE 4
    END,
    IdInmueble;

;WITH Legacy AS
(
    SELECT ii.idInmueble, MAX(ISNULL(ii.Imagenes, 0)) AS FotosLegacy
    FROM dbo.RSMAPS_InmuebleImagenes ii
    GROUP BY ii.idInmueble
),
Modernas AS
(
    SELECT
        f.IdInmueble,
        SUM(CASE WHEN f.Activo = 1 THEN 1 ELSE 0 END) AS FotosModernasActivas,
        SUM(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 ELSE 0 END) AS PortadasActivas
    FROM dbo.RSMAPS_InmuebleImagen f
    GROUP BY f.IdInmueble
),
Resumen AS
(
    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.EstadoCodigo,
        ISNULL(l.FotosLegacy, 0) AS FotosLegacy,
        ISNULL(m.FotosModernasActivas, 0) AS FotosModernasActivas,
        ISNULL(m.PortadasActivas, 0) AS PortadasActivas
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN Legacy l ON l.idInmueble = i.idInmueble
    LEFT JOIN Modernas m ON m.IdInmueble = i.idInmueble
)
SELECT
    SUM(CASE WHEN IdCuenta = 1
                  AND EstadoCodigo IN ('BORRADOR','PUBLICADO','PAUSADO','RETIRADO')
                  AND FotosLegacy > 0
                  AND FotosModernasActivas = 0
                  AND FotosLegacy <= 40
             THEN 1 ELSE 0 END) AS CandidatosInventarioPrincipal,
    SUM(CASE WHEN IdCuenta = 1
                  AND EstadoCodigo IN ('VENDIDO','RENTADO')
                  AND FotosLegacy > 0
                  AND FotosModernasActivas = 0
                  AND FotosLegacy <= 40
             THEN 1 ELSE 0 END) AS CandidatosHistoricos,
    SUM(CASE WHEN IdCuenta <> 1 AND FotosLegacy > 0 THEN 1 ELSE 0 END) AS InmueblesOtrasCuentasConLegacy,
    SUM(CASE WHEN FotosLegacy > 40 THEN 1 ELSE 0 END) AS InmueblesQueExceden40,
    SUM(CASE WHEN FotosModernasActivas > 0
                  AND (FotosLegacy <> FotosModernasActivas OR PortadasActivas <> 1)
             THEN 1 ELSE 0 END) AS DiferenciasARevisar,
    SUM(FotosLegacy) AS FotosLegacyDeclaradas,
    SUM(FotosModernasActivas) AS FotosModernasActivas
FROM Resumen;

SELECT
    'SOLO LECTURA - NO SE MODIFICO NINGUN DATO' AS EstadoPaso51;
