/* ============================================================
   RSMaps 2.0 - Paso 53
   VERIFICACION POSTERIOR DE MIGRACION DE FOTOS ACTIVAS

   Objetivo:
   - Verificar que la migracion del Paso 52 quedo consistente.
   - Confirmar 72 inmuebles migrados, 808 fotos activas y 72 portadas.
   - Confirmar que los 3 inmuebles principales que ya eran modernos
     (87, 186, 187) siguen correctos.
   - Confirmar que el historico 152 sigue sin metadata moderna.
   - No modificar ningun dato.
   ============================================================ */

SET NOCOUNT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 55300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55301, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 55302, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL
    THROW 55303, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;

DECLARE @Migrados TABLE
(
    IdInmueble int NOT NULL PRIMARY KEY,
    Esperadas int NOT NULL
);

INSERT @Migrados (IdInmueble, Esperadas) VALUES
(79,6),(80,11),(81,15),(84,5),(85,21),(86,25),(88,17),(89,17),(90,13),(91,3),(92,13),(93,22),(94,14),(95,15),(96,29),(97,10),(98,22),(99,21),
(100,7),(101,5),(102,7),(103,7),(104,15),(105,9),(106,11),(107,12),(108,16),(109,6),(110,9),(111,10),(112,7),(113,25),(114,6),(115,10),(116,11),(117,19),
(118,21),(119,20),(120,5),(121,5),(122,6),(123,24),(124,12),(125,12),(130,4),(132,13),(133,12),(135,22),(138,25),(142,33),(145,1),(146,1),(147,1),(148,2),
(150,34),(151,2),(153,19),(154,13),(155,1),(156,1),(157,1),(158,1),(159,1),(161,1),(162,1),(163,1),(164,1),(165,1),(166,10),(167,1),(168,1),(169,28);

/* Detalle por inmueble migrado. */
SELECT
    m.IdInmueble,
    m.Esperadas AS FotosEsperadas,
    ISNULL(l.Legacy, 0) AS FotosLegacy,
    ISNULL(x.ModernasActivas, 0) AS FotosModernasActivas,
    ISNULL(x.PortadasActivas, 0) AS PortadasActivas,
    CASE
        WHEN i.idInmueble IS NULL THEN 'REVISAR_INMUEBLE_NO_EXISTE'
        WHEN i.IdCuenta <> 1 THEN 'REVISAR_CUENTA'
        WHEN i.EstadoCodigo <> 'PUBLICADO' OR i.VisibilidadCodigo <> 'PUBLICO' THEN 'REVISAR_ESTADO_VISIBILIDAD'
        WHEN ISNULL(l.Legacy, 0) <> m.Esperadas THEN 'REVISAR_LEGACY'
        WHEN ISNULL(x.ModernasActivas, 0) <> m.Esperadas THEN 'REVISAR_MODERNAS'
        WHEN ISNULL(x.PortadasActivas, 0) <> 1 THEN 'REVISAR_PORTADA'
        ELSE 'OK'
    END AS EstadoVerificacion
FROM @Migrados m
LEFT JOIN dbo.RSMAPS_Inmueble i
  ON i.idInmueble = m.IdInmueble
OUTER APPLY
(
    SELECT ISNULL(MAX(ii.Imagenes), 0) AS Legacy
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = m.IdInmueble
) l
OUTER APPLY
(
    SELECT
        SUM(CASE WHEN f.Activo = 1 THEN 1 ELSE 0 END) AS ModernasActivas,
        SUM(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 ELSE 0 END) AS PortadasActivas
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = m.IdInmueble
) x
ORDER BY m.IdInmueble;

/* Resumen principal del Paso 52. */
DECLARE @InmueblesMigrados int = (SELECT COUNT(*) FROM @Migrados);
DECLARE @FotosEsperadas int = (SELECT SUM(Esperadas) FROM @Migrados);
DECLARE @FotosModernasActivas int =
(
    SELECT COUNT(*)
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.Activo = 1
      AND EXISTS (SELECT 1 FROM @Migrados m WHERE m.IdInmueble = f.IdInmueble)
);
DECLARE @PortadasModernas int =
(
    SELECT COUNT(*)
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.Activo = 1
      AND f.EsPortada = 1
      AND EXISTS (SELECT 1 FROM @Migrados m WHERE m.IdInmueble = f.IdInmueble)
);
DECLARE @InmueblesConProblema int =
(
    SELECT COUNT(*)
    FROM @Migrados m
    LEFT JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = m.IdInmueble
    OUTER APPLY
    (
        SELECT ISNULL(MAX(ii.Imagenes), 0) AS Legacy
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = m.IdInmueble
    ) l
    OUTER APPLY
    (
        SELECT
            SUM(CASE WHEN f.Activo = 1 THEN 1 ELSE 0 END) AS ModernasActivas,
            SUM(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 ELSE 0 END) AS PortadasActivas
        FROM dbo.RSMAPS_InmuebleImagen f
        WHERE f.IdInmueble = m.IdInmueble
    ) x
    WHERE i.idInmueble IS NULL
       OR i.IdCuenta <> 1
       OR i.EstadoCodigo <> 'PUBLICADO'
       OR i.VisibilidadCodigo <> 'PUBLICO'
       OR ISNULL(l.Legacy, 0) <> m.Esperadas
       OR ISNULL(x.ModernasActivas, 0) <> m.Esperadas
       OR ISNULL(x.PortadasActivas, 0) <> 1
);

SELECT
    @InmueblesMigrados AS InmueblesObjetivo,
    @FotosEsperadas AS FotosEsperadas,
    @FotosModernasActivas AS FotosModernasActivas,
    @PortadasModernas AS PortadasModernas,
    @InmueblesConProblema AS InmueblesConProblema,
    CASE
        WHEN @InmueblesMigrados = 72
         AND @FotosEsperadas = 808
         AND @FotosModernasActivas = 808
         AND @PortadasModernas = 72
         AND @InmueblesConProblema = 0
        THEN 'OK - PASO 52 VERIFICADO'
        ELSE 'REVISAR - VERIFICACION POSTERIOR NO COINCIDE'
    END AS EstadoPaso53;

/* Confirmar que los principales ya modernos siguen intactos. */
SELECT
    v.IdInmueble,
    v.Esperadas,
    COUNT(CASE WHEN f.Activo = 1 THEN 1 END) AS FotosModernasActivas,
    COUNT(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 END) AS PortadasActivas,
    CASE
        WHEN COUNT(CASE WHEN f.Activo = 1 THEN 1 END) = v.Esperadas
         AND COUNT(CASE WHEN f.Activo = 1 AND f.EsPortada = 1 THEN 1 END) = 1
        THEN 'OK'
        ELSE 'REVISAR'
    END AS Estado
FROM (VALUES (87,18),(186,20),(187,14)) v(IdInmueble, Esperadas)
LEFT JOIN dbo.RSMAPS_InmuebleImagen f
  ON f.IdInmueble = v.IdInmueble
GROUP BY v.IdInmueble, v.Esperadas
ORDER BY v.IdInmueble;

/* El historico 152 debe seguir fuera de la migracion moderna. */
SELECT
    152 AS IdInmueble,
    (SELECT ISNULL(MAX(ii.Imagenes),0) FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=152) AS FotosLegacy,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=152 AND f.Activo=1) AS FotosModernasActivas,
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=152 AND f.Activo=1) = 0
        THEN 'OK - HISTORICO SIGUE FUERA'
        ELSE 'REVISAR - HISTORICO TIENE METADATA MODERNA'
    END AS EstadoHistorico152;

SELECT 'SOLO LECTURA - NO SE MODIFICO NINGUN DATO' AS EstadoLectura;
