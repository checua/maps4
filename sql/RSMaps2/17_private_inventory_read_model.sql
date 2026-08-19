/* ============================================================
   RSMaps 2.0 - Paso 17
   MODELO DE LECTURA DEL INVENTARIO PRIVADO

   Objetivo:
   - Mantener el inventario privado aislado por Cuenta/Asesor.
   - Exponer el nombre humano del tipo de propiedad.
   - Conservar todos los estados, incluida la historia cerrada.
   - No modificar datos del inmueble.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 51700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51701, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_TipoPropiedades', N'U') IS NULL
    THROW 51702, 'No existe dbo.RSMAPS_TipoPropiedades.', 1;

DECLARE @sql nvarchar(max) = N'
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
        tp.nombre AS TipoNombre,
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
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp
        ON tp.idTipoPropiedad = i.idTipo
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

EXEC sys.sp_executesql @sql;

/* Validar que el procedimiento exponga TipoNombre. */
SELECT
    d.column_ordinal,
    d.name AS Columna,
    d.system_type_name AS TipoDato,
    d.is_nullable AS PermiteNull
FROM sys.dm_exec_describe_first_result_set_for_object
(
    OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmueblesCuenta'), NULL
) d
WHERE d.is_hidden = 0
ORDER BY d.column_ordinal;

SELECT
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM sys.dm_exec_describe_first_result_set_for_object
        (
            OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmueblesCuenta'), NULL
        ) d
        WHERE d.name = 'TipoNombre'
    )
    THEN 'OK - INVENTARIO EXPONE TIPO HUMANO'
    ELSE 'REVISAR'
    END AS EstadoPaso17;

PRINT 'Paso 17 RSMaps 2.0 terminado correctamente.';
