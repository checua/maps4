/* ============================================================
   RSMaps 2.0 - Paso 12
   MARKETPLACE PUBLICO E INVENTARIO PRIVADO

   Base esperada: mapsMarkers

   Objetivo:
   - Hacer que RSMAPS_sp_ListaInmuebles represente exclusivamente el
     marketplace publico: PUBLICADO + PUBLICO.
   - Evitar que un inmueble valido desaparezca por no tener registro en
     RSMAPS_InmuebleImagenes.
   - Soportar cuentas INDIVIDUAL sin inmobiliaria legacy.
   - Crear RSMAPS_sp_ListaInmueblesCuenta como lectura privada por Cuenta,
     sin conectarla todavia a una pantalla.
   - Mantener la forma de columnas que consume actualmente el mapa publico.

   IMPORTANTE:
   - NO cambia estados ni visibilidades de forma persistente.
   - NO elimina inmuebles ni imagenes.
   - NO cambia todavia la experiencia visual del inventario privado.
   - La prueba controlada usa ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 51200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51201, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'EstadoCodigo') IS NULL
    THROW 51202, 'Falta EstadoCodigo. Ejecutar primero Paso 11.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'VisibilidadCodigo') IS NULL
    THROW 51203, 'Falta VisibilidadCodigo. Ejecutar primero Paso 11.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 51204, 'No existe dbo.RSMAPS_Usuario.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Marketplace publico

       Se conserva el nombre legacy RSMAPS_sp_ListaInmuebles para no
       modificar todavia HomeController/InmuebleRepository.

       OUTER APPLY garantiza una sola fila por inmueble y permite que
       un inmueble sin registro de imagenes siga apareciendo con 0.
       LEFT JOIN a inmobiliaria permite cuentas INDIVIDUAL.
       ------------------------------------------------------------ */
    DECLARE @sqlPublico nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListaInmuebles
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.idInmueble,
        i.idInmobiliaria,
        inm.nombre,
        i.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        i.telefono,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.exclusiva,
        i.link,
        i.contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
        ON inm.idInmobiliaria = i.idInmobiliaria
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    WHERE i.EstadoCodigo = ''PUBLICADO''
      AND i.VisibilidadCodigo = ''PUBLICO''
    ORDER BY i.idInmueble;
END;
';

    EXEC sys.sp_executesql @sqlPublico;

    /* ------------------------------------------------------------
       2. Inventario privado por cuenta

       Esta lectura NO se conecta aun a la UI. Devuelve todos los estados
       de la cuenta y opcionalmente limita a un asesor concreto.
       ------------------------------------------------------------ */
    DECLARE @sqlCuenta nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListaInmueblesCuenta
    @IdCuenta INT,
    @IdAsesor INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idInmobiliaria,
        inm.nombre,
        i.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        i.telefono,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.exclusiva,
        i.link,
        i.contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaPublicacionUtc,
        i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
        ON inm.idInmobiliaria = i.idInmobiliaria
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    WHERE i.IdCuenta = @IdCuenta
      AND (@IdAsesor IS NULL OR i.idAsesor = @IdAsesor)
    ORDER BY
        CASE i.EstadoCodigo
            WHEN ''PUBLICADO'' THEN 10
            WHEN ''BORRADOR'' THEN 20
            WHEN ''PAUSADO'' THEN 30
            WHEN ''RETIRADO'' THEN 40
            WHEN ''VENDIDO'' THEN 50
            WHEN ''RENTADO'' THEN 60
            ELSE 99
        END,
        i.idInmueble DESC;
END;
';

    EXEC sys.sp_executesql @sqlCuenta;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   3. VALIDACION DE FORMA Y CONTEOS
   ============================================================ */
DECLARE @MarketplaceActual int;
DECLARE @ElegiblesPublicos int;
DECLARE @PublicosSinRegistroImagen int;

SELECT @ElegiblesPublicos = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT @MarketplaceActual = COUNT(*)
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = i.idAsesor
LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
    ON inm.idInmobiliaria = i.idInmobiliaria
OUTER APPLY
(
    SELECT MAX(ii.Imagenes) AS Imagenes
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = i.idInmueble
) img
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO';

SELECT @PublicosSinRegistroImagen = COUNT(*)
FROM dbo.RSMAPS_Inmueble i
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_InmuebleImagenes ii
      WHERE ii.idInmueble = i.idInmueble
  );

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble) AS TotalInventario,
    @ElegiblesPublicos AS ElegiblesMarketplace,
    @MarketplaceActual AS FilasQueDevuelveMarketplace,
    @PublicosSinRegistroImagen AS PublicosSinRegistroImagen,
    CASE
        WHEN @ElegiblesPublicos = @MarketplaceActual
        THEN 'OK - MARKETPLACE NO PIERDE INMUEBLES POR IMAGEN/INMOBILIARIA'
        ELSE 'REVISAR'
    END AS EstadoListado;

/* Mostrar hasta 10 inmuebles publicos sin registro de imagenes. */
SELECT TOP (10)
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.idInmobiliaria,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    'INCLUIDO CON imagenes = 0' AS ComportamientoMarketplace
FROM dbo.RSMAPS_Inmueble i
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_InmuebleImagenes ii
      WHERE ii.idInmueble = i.idInmueble
  )
ORDER BY i.idInmueble;

/* ============================================================
   4. PRUEBA CONTROLADA: PAUSAR DEBE SACAR DEL MARKETPLACE,
      PERO NO DEL INVENTARIO DE SU CUENTA
   ============================================================ */
DECLARE @IdInmueblePrueba int;
DECLARE @IdCuentaPrueba int;
DECLARE @PublicosAntes int;
DECLARE @PublicosDurante int;
DECLARE @CuentaAntes int;
DECLARE @CuentaDurante int;
DECLARE @PublicosDespues int;

SELECT TOP (1)
    @IdInmueblePrueba = idInmueble,
    @IdCuentaPrueba = IdCuenta
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO'
ORDER BY idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 51210, 'No existe un inmueble PUBLICADO + PUBLICO para la prueba.', 1;

SELECT @PublicosAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT @CuentaAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE IdCuenta = @IdCuentaPrueba;

BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_Inmueble
SET EstadoCodigo = 'PAUSADO'
WHERE idInmueble = @IdInmueblePrueba;

SELECT @PublicosDurante = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT @CuentaDurante = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE IdCuenta = @IdCuentaPrueba;

SELECT
    @IdInmueblePrueba AS idInmueblePrueba,
    @PublicosAntes AS PublicosAntes,
    @PublicosDurante AS PublicosDurantePausa,
    @CuentaAntes AS InventarioCuentaAntes,
    @CuentaDurante AS InventarioCuentaDurantePausa,
    CASE
        WHEN @PublicosDurante = @PublicosAntes - 1
         AND @CuentaDurante = @CuentaAntes
        THEN 'OK - PAUSADO SALE DEL MARKETPLACE, PERMANECE EN INVENTARIO'
        ELSE 'REVISAR'
    END AS EstadoPrueba;

ROLLBACK TRANSACTION;

SELECT @PublicosDespues = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT
    @PublicosAntes AS PublicosAntes,
    @PublicosDespues AS PublicosDespuesRollback,
    CASE
        WHEN @PublicosAntes = @PublicosDespues
         AND EXISTS
         (
             SELECT 1
             FROM dbo.RSMAPS_Inmueble
             WHERE idInmueble = @IdInmueblePrueba
               AND EstadoCodigo = 'PUBLICADO'
         )
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

/* Verificar columnas de ambos procedimientos. */
SELECT
    OBJECT_NAME(d.object_id) AS Procedimiento,
    d.column_ordinal,
    d.name AS Columna,
    d.system_type_name AS TipoDato,
    d.is_nullable AS PermiteNull
FROM
(
    SELECT
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmuebles') AS object_id,
        *
    FROM sys.dm_exec_describe_first_result_set_for_object
    (
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmuebles'), NULL
    )

    UNION ALL

    SELECT
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmueblesCuenta') AS object_id,
        *
    FROM sys.dm_exec_describe_first_result_set_for_object
    (
        OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmueblesCuenta'), NULL
    )
) d
WHERE d.is_hidden = 0
ORDER BY Procedimiento, column_ordinal;

PRINT 'Paso 12 RSMaps 2.0 terminado correctamente.';
