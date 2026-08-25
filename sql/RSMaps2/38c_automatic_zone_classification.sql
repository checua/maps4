/* ============================================================
   RSMaps 2.0 - Paso 38C
   CLASIFICACION GEOGRAFICA AUTOMATICA DE INMUEBLES

   Objetivo:
   - Clasificar silenciosamente cada inmueble por sus coordenadas.
   - No agregar campos obligatorios al formulario del asesor.
   - Funcionar para altas modernas, legacy e importaciones.
   - Recalcular al insertar o cambiar lat/lng/IdCuenta.
   - Respetar correcciones MANUALES y su zona principal.

   Requiere:
   - Paso 38A instalado.
   - dbo.RSMAPS_Zona / RSMAPS_ZonaPoligono / RSMAPS_InmuebleZona.

   Regla de zona principal automatica:
   1) Prioridad de zona DESC.
   2) Area del poligono que contiene el punto ASC.
   3) IdZona ASC.

   Una zona principal MANUAL siempre tiene precedencia.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53870, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 53871, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Zona', N'U') IS NULL
    THROW 53872, 'No existe dbo.RSMAPS_Zona. Ejecutar Paso 38A.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_ZonaPoligono', N'U') IS NULL
    THROW 53873, 'No existe dbo.RSMAPS_ZonaPoligono. Ejecutar Paso 38A.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleZona', N'U') IS NULL
    THROW 53874, 'No existe dbo.RSMAPS_InmuebleZona. Ejecutar Paso 38A.', 1;
GO

CREATE OR ALTER TRIGGER dbo.RSMAPS_tr_Inmueble_AutoClasificarZona
ON dbo.RSMAPS_Inmueble
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT (UPDATE(lat) OR UPDATE(lng) OR UPDATE(IdCuenta))
        RETURN;

    DECLARE @Afectados TABLE
    (
        IdInmueble int NOT NULL PRIMARY KEY,
        IdCuenta int NULL,
        Lat float NULL,
        Lng float NULL,
        Punto geometry NULL
    );

    INSERT @Afectados (IdInmueble, IdCuenta, Lat, Lng, Punto)
    SELECT
        i.idInmueble,
        i.IdCuenta,
        TRY_CONVERT(float, i.lat),
        TRY_CONVERT(float, i.lng),
        CASE
            WHEN i.IdCuenta IS NOT NULL
             AND TRY_CONVERT(float, i.lat) BETWEEN -90 AND 90
             AND TRY_CONVERT(float, i.lng) BETWEEN -180 AND 180
            THEN geometry::Point(TRY_CONVERT(float, i.lng), TRY_CONVERT(float, i.lat), 4326)
            ELSE NULL
        END
    FROM inserted i;

    DELETE iz
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN @Afectados a ON a.IdInmueble = iz.IdInmueble
    INNER JOIN dbo.RSMAPS_Zona z ON z.IdZona = iz.IdZona
    WHERE a.IdCuenta IS NULL
       OR z.IdCuenta <> a.IdCuenta;

    DELETE iz
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN @Afectados a ON a.IdInmueble = iz.IdInmueble
    WHERE iz.Origen = 'AUTOMATICA';

    INSERT dbo.RSMAPS_InmuebleZona
    (
        IdInmueble, IdZona, Origen, EsPrincipal, Confianza,
        IdAsesorCambio, FechaAsignacionUtc
    )
    SELECT DISTINCT
        a.IdInmueble,
        z.IdZona,
        'AUTOMATICA',
        0,
        CONVERT(decimal(5,2), 100.00),
        NULL,
        SYSUTCDATETIME()
    FROM @Afectados a
    INNER JOIN dbo.RSMAPS_Zona z
        ON z.IdCuenta = a.IdCuenta
       AND z.Activa = 1
    WHERE a.Punto IS NOT NULL
      AND EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_ZonaPoligono zp
          WHERE zp.IdZona = z.IdZona
            AND zp.Activo = 1
            AND zp.Poligono.STIntersects(a.Punto) = 1
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_InmuebleZona existente
          WHERE existente.IdInmueble = a.IdInmueble
            AND existente.IdZona = z.IdZona
      );

    ;WITH Candidatas AS
    (
        SELECT
            iz.IdInmueble,
            iz.IdZona,
            ROW_NUMBER() OVER
            (
                PARTITION BY iz.IdInmueble
                ORDER BY
                    z.Prioridad DESC,
                    ISNULL(areaZona.AreaMinima, 1.0E20) ASC,
                    z.IdZona ASC
            ) AS rn
        FROM dbo.RSMAPS_InmuebleZona iz
        INNER JOIN @Afectados a ON a.IdInmueble = iz.IdInmueble
        INNER JOIN dbo.RSMAPS_Zona z ON z.IdZona = iz.IdZona
        OUTER APPLY
        (
            SELECT MIN(zp.Poligono.STArea()) AS AreaMinima
            FROM dbo.RSMAPS_ZonaPoligono zp
            WHERE zp.IdZona = z.IdZona
              AND zp.Activo = 1
              AND a.Punto IS NOT NULL
              AND zp.Poligono.STIntersects(a.Punto) = 1
        ) areaZona
        WHERE iz.Origen = 'AUTOMATICA'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.RSMAPS_InmuebleZona manualPrincipal
              WHERE manualPrincipal.IdInmueble = iz.IdInmueble
                AND manualPrincipal.EsPrincipal = 1
                AND manualPrincipal.Origen = 'MANUAL'
          )
    )
    UPDATE iz
    SET EsPrincipal = CASE WHEN c.rn = 1 THEN 1 ELSE 0 END
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN Candidatas c
        ON c.IdInmueble = iz.IdInmueble
       AND c.IdZona = iz.IdZona;
END;
GO

DECLARE @IdInmueblePrueba int;
DECLARE @IdZonaEsperada int;
DECLARE @CodigoEsperado varchar(60);

SELECT TOP (1)
    @IdInmueblePrueba = iz.IdInmueble,
    @IdZonaEsperada = iz.IdZona,
    @CodigoEsperado = z.Codigo
FROM dbo.RSMAPS_InmuebleZona iz
INNER JOIN dbo.RSMAPS_Zona z ON z.IdZona = iz.IdZona
INNER JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = iz.IdInmueble
WHERE iz.Origen = 'AUTOMATICA'
  AND iz.EsPrincipal = 1
  AND TRY_CONVERT(float, i.lat) BETWEEN -90 AND 90
  AND TRY_CONVERT(float, i.lng) BETWEEN -180 AND 180
ORDER BY iz.IdInmueble;

IF @IdInmueblePrueba IS NULL
BEGIN
    SELECT 'OMITIDA - AUN NO HAY INMUEBLE CLASIFICADO PARA PROBAR EL TRIGGER' AS EstadoPrueba;
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    DELETE FROM dbo.RSMAPS_InmuebleZona
    WHERE IdInmueble = @IdInmueblePrueba
      AND Origen = 'AUTOMATICA';

    UPDATE dbo.RSMAPS_Inmueble
    SET lat = lat
    WHERE idInmueble = @IdInmueblePrueba;

    SELECT
        @IdInmueblePrueba AS IdInmueblePrueba,
        @CodigoEsperado AS ZonaEsperada,
        z.Codigo AS ZonaDetectada,
        iz.Origen,
        iz.EsPrincipal,
        iz.Confianza,
        CASE
            WHEN iz.IdZona = @IdZonaEsperada
             AND iz.Origen = 'AUTOMATICA'
             AND iz.EsPrincipal = 1
            THEN 'OK - TRIGGER RECLASIFICO AUTOMATICAMENTE'
            ELSE 'REVISAR'
        END AS EstadoPrueba
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN dbo.RSMAPS_Zona z ON z.IdZona = iz.IdZona
    WHERE iz.IdInmueble = @IdInmueblePrueba
      AND iz.EsPrincipal = 1;

    ROLLBACK;

    SELECT
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_InmuebleZona
            WHERE IdInmueble = @IdInmueblePrueba
              AND IdZona = @IdZonaEsperada
              AND Origen = 'AUTOMATICA'
              AND EsPrincipal = 1
        ) THEN 1 ELSE 0 END AS AsignacionOriginalRestaurada,
        'OK - ROLLBACK COMPLETO' AS EstadoRollback;
END;
GO

SELECT
    OBJECT_ID(N'dbo.RSMAPS_tr_Inmueble_AutoClasificarZona', N'TR') AS TriggerAutoZona,
    'OK - PASO 38C INSTALADO' AS Estado;
