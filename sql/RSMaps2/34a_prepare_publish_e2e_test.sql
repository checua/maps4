/* ============================================================
   RSMaps 2.0 - Paso 34A
   PREPARAR BORRADOR TEMPORAL PARA PRUEBA E2E DE PUBLICACION

   Objetivo:
   - Crear un borrador temporal, completo y claramente identificable.
   - Reutilizar SOLO como referencia dos archivos fisicos existentes del #176.
   - No modificar ni borrar fotos del #176.
   - Dejar el inmueble listo para pulsar "Publicar propiedad" desde la Web.
   - NO ejecutar 34B hasta terminar la prueba E2E.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_sp_CrearBorradorInmueble', N'P') IS NULL
    THROW 53501, 'Falta RSMAPS_sp_CrearBorradorInmueble. Ejecutar Paso 27.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_sp_GuardarBorradorInmueble', N'P') IS NULL
    THROW 53502, 'Falta RSMAPS_sp_GuardarBorradorInmueble. Ejecutar Paso 29.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_sp_RegistrarFotoBorrador', N'P') IS NULL
    THROW 53503, 'Falta RSMAPS_sp_RegistrarFotoBorrador. Ejecutar Paso 32.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_sp_PublicarBorradorInmueble', N'P') IS NULL
    THROW 53504, 'Falta RSMAPS_sp_PublicarBorradorInmueble. Ejecutar Paso 33.', 1;

DECLARE @Correo varchar(200) = 'profesor76@hotmail.com';
DECLARE @Direccion varchar(max) = 'PRUEBA E2E PUBLICACION - NO ES PROPIEDAD REAL';
DECLARE @NotaPrivada nvarchar(max) = N'[RSMAPS-TEST-PUBLISH-E2E] DATO PRIVADO: NO DEBE APARECER EN EL MARKETPLACE.';
DECLARE @Descripcion varchar(max) = 'PRUEBA E2E PUBLICACION. Inmueble temporal para validar alta desde borrador, marketplace, fotos y auditoria. NO ES PROPIEDAD REAL.';
DECLARE @Precio decimal(18,2) = 1987654.00;
DECLARE @IdTipo int = 2;
DECLARE @IdNuevo int;
DECLARE @IdCuenta int;
DECLARE @IdAsesor int;
DECLARE @IdFoto bigint;

IF EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_Inmueble
    WHERE CONVERT(varchar(max), direccion) = @Direccion
       OR CONVERT(nvarchar(max), NotasPrivadas) = @NotaPrivada
)
    THROW 53505, 'Ya existe una prueba E2E de publicacion pendiente. Ejecutar 34B antes de crear otra.', 1;

SELECT @IdAsesor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @Correo;

IF @IdAsesor IS NULL
    THROW 53506, 'No existe el usuario configurado para la prueba.', 1;

SELECT TOP (1) @IdCuenta = cu.IdCuenta
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @IdCuenta IS NULL
    THROW 53507, 'El usuario de prueba no pertenece a una cuenta activa.', 1;

IF (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble = 176 AND Activo = 1) < 1
    THROW 53508, 'El borrador #176 no tiene fotos modernas activas para reutilizar en la prueba.', 1;

EXEC dbo.RSMAPS_sp_CrearBorradorInmueble
    @correo = @Correo,
    @lat = 24.027600,
    @lng = -104.653200,
    @idTipo = @IdTipo,
    @idInmueble = @IdNuevo OUTPUT;

EXEC dbo.RSMAPS_sp_GuardarBorradorInmueble
    @correo = @Correo,
    @idInmueble = @IdNuevo,
    @direccion = @Direccion,
    @idTipo = @IdTipo,
    @terreno = 321,
    @construccion = 210,
    @precio = @Precio,
    @observaciones = @Descripcion,
    @notasPrivadas = @NotaPrivada;

DECLARE @Clave nvarchar(500), @Nombre nvarchar(255), @Mime varchar(100), @Bytes bigint;
DECLARE fotos CURSOR LOCAL FAST_FORWARD FOR
SELECT TOP (2)
    f.ClaveAlmacenamiento,
    f.NombreOriginal,
    f.MimeType,
    f.Bytes
FROM dbo.RSMAPS_InmuebleImagen f
WHERE f.IdInmueble = 176
  AND f.Activo = 1
ORDER BY f.EsPortada DESC, f.Orden, f.IdImagen;

OPEN fotos;
FETCH NEXT FROM fotos INTO @Clave, @Nombre, @Mime, @Bytes;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC dbo.RSMAPS_sp_RegistrarFotoBorrador
        @correo = @Correo,
        @idInmueble = @IdNuevo,
        @claveAlmacenamiento = @Clave,
        @nombreOriginal = @Nombre,
        @mimeType = @Mime,
        @bytes = @Bytes,
        @idImagen = @IdFoto OUTPUT;

    FETCH NEXT FROM fotos INTO @Clave, @Nombre, @Mime, @Bytes;
END;
CLOSE fotos;
DEALLOCATE fotos;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_InmuebleImagen
    WHERE IdInmueble = @IdNuevo
      AND Activo = 1
      AND EsPortada = 1
)
    THROW 53509, 'No se pudo preparar una portada para la propiedad temporal.', 1;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    u.nombres + ' ' + u.aPaterno AS AsesorResponsable,
    u.correo,
    i.idTipo,
    tp.nombre AS TipoPropiedad,
    TRY_CONVERT(decimal(18,2), i.precio) AS precio,
    i.terreno,
    i.construccion,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.FechaPublicacionUtc,
    i.lat,
    i.lng,
    i.direccion,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=i.idInmueble AND f.Activo=1) AS Fotos,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=i.idInmueble AND f.Activo=1 AND f.EsPortada=1) AS Portadas,
    CASE
        WHEN i.EstadoCodigo='BORRADOR'
         AND i.VisibilidadCodigo='CUENTA'
         AND TRY_CONVERT(decimal(18,2),i.precio)=@Precio
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=i.idInmueble AND f.Activo=1) >= 1
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.IdInmueble=i.idInmueble AND f.Activo=1 AND f.EsPortada=1)=1
        THEN 'OK - BORRADOR TEMPORAL LISTO PARA PUBLICAR DESDE LA WEB'
        ELSE 'REVISAR'
    END AS EstadoPreparacion
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor=i.idAsesor
LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad=i.idTipo
WHERE i.idInmueble=@IdNuevo;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@IdNuevo) AS HistorialPrecioAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@IdNuevo) AS HistorialEstadoAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble=@IdNuevo) AS OperacionesAntes,
    'AHORA ABRE /Borrador/Editar/ID, PULSA PUBLICAR PROPIEDAD Y NO EJECUTES 34B HASTA TERMINAR LA VALIDACION.' AS SiguientePaso;
