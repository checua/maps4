/* RSMaps 2.0 - Paso 33: publicacion segura de borradores */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME()<>'mapsMarkers' THROW 53400,'Ejecutar en mapsMarkers.',1;
IF OBJECT_ID('dbo.RSMAPS_Inmueble','U') IS NULL THROW 53401,'Falta RSMAPS_Inmueble.',1;
IF OBJECT_ID('dbo.RSMAPS_InmuebleImagen','U') IS NULL THROW 53402,'Ejecutar Paso 32.',1;
IF OBJECT_ID('dbo.RSMAPS_InmueblePrecioHistorial','U') IS NULL THROW 53403,'Ejecutar Paso 15.',1;
IF OBJECT_ID('dbo.RSMAPS_InmuebleCambioEstado','U') IS NULL THROW 53404,'Falta historial de estados.',1;
GO

IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_Permiso(Codigo,Nombre,Descripcion,Activo)
    VALUES('INMUEBLE_PUBLICAR_BORRADOR_PROPIO',N'Publicar borrador propio',N'Convierte un borrador propio completo en PUBLICADO/PUBLICO.',1);

IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ASESOR' AND PermisoCodigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso VALUES('ASESOR','INMUEBLE_PUBLICAR_BORRADOR_PROPIO');
IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ADMINISTRADOR' AND PermisoCodigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso VALUES('ADMINISTRADOR','INMUEBLE_PUBLICAR_BORRADOR_PROPIO');
IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='PROPIETARIO' AND PermisoCodigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso VALUES('PROPIETARIO','INMUEBLE_PUBLICAR_BORRADOR_PROPIO');
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_PublicarBorradorInmueble
 @correo varchar(200), @idInmueble int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @actor int,@cuenta int,@rol varchar(30),@responsable int,@cuentaInm int,@estado varchar(20),@vis varchar(20);
 DECLARE @lat decimal(10,6),@lng decimal(10,6),@tipo int,@precio decimal(18,2),@terreno float,@construccion float,@obs varchar(max);
 DECLARE @fotos int,@portadas int,@ahora datetime2(0)=SYSUTCDATETIME();

 SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
 IF @actor IS NULL THROW 53420,'Usuario autenticado no encontrado.',1;

 SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
 FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
 WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
 IF @cuenta IS NULL THROW 53421,'El usuario no pertenece a una cuenta activa.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1
               WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO')
    THROW 53423,'El rol actual no puede publicar borradores.',1;

 SELECT @cuentaInm=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo,@vis=VisibilidadCodigo,
        @lat=lat,@lng=lng,@tipo=idTipo,@precio=TRY_CONVERT(decimal(18,2),precio),@terreno=terreno,@construccion=construccion,@obs=observaciones
 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
 IF @cuentaInm IS NULL THROW 53424,'El inmueble no existe.',1;
 IF @cuentaInm<>@cuenta THROW 53425,'El inmueble pertenece a otra cuenta.',1;
 IF @responsable<>@actor THROW 53426,'Solo el asesor responsable puede publicar este borrador.',1;
 IF @estado<>'BORRADOR' THROW 53427,'El inmueble ya no es BORRADOR.',1;

 IF @lat IS NULL OR @lng IS NULL OR @lat NOT BETWEEN -90 AND 90 OR @lng NOT BETWEEN -180 AND 180 THROW 53430,'Falta una ubicacion valida.',1;
 IF @tipo IS NULL OR @tipo<=1 OR NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad=@tipo) THROW 53431,'Falta un tipo valido.',1;
 IF @precio IS NULL OR @precio<=0 THROW 53432,'El precio debe ser mayor que cero.',1;
 IF ISNULL(@terreno,0)<=0 AND ISNULL(@construccion,0)<=0 THROW 53433,'Registra al menos una superficie.',1;
 IF NULLIF(LTRIM(RTRIM(ISNULL(@obs,''))),'') IS NULL OR LTRIM(RTRIM(@obs))='0' THROW 53434,'Agrega una descripcion comercial.',1;

 SELECT @fotos=COUNT(*),@portadas=SUM(CASE WHEN EsPortada=1 THEN 1 ELSE 0 END)
 FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1;
 IF ISNULL(@fotos,0)<1 THROW 53435,'Agrega al menos una foto.',1;
 IF ISNULL(@portadas,0)<>1 THROW 53436,'Debe existir exactamente una portada.',1;

 BEGIN TRY
  BEGIN TRANSACTION;
  UPDATE dbo.RSMAPS_Inmueble
  SET EstadoCodigo='PUBLICADO',VisibilidadCodigo='PUBLICO',FechaPublicacionUtc=@ahora,
      FechaUltimoCambioEstadoUtc=@ahora,FechaUltimaEdicionUtc=@ahora
  WHERE idInmueble=@idInmueble AND EstadoCodigo='BORRADOR';
  IF @@ROWCOUNT<>1 THROW 53437,'El borrador cambio mientras se publicaba.',1;

  INSERT dbo.RSMAPS_InmuebleCambioEstado
   (IdInmueble,IdCuenta,EstadoAnterior,EstadoNuevo,VisibilidadAnterior,VisibilidadNueva,IdAsesorResponsable,IdAsesorCambio,FechaCambioUtc,Motivo,Origen)
  VALUES(@idInmueble,@cuenta,@estado,'PUBLICADO',@vis,'PUBLICO',@responsable,@actor,@ahora,N'Publicacion inicial desde borrador completo.','APLICACION');

  INSERT dbo.RSMAPS_InmueblePrecioHistorial
   (IdInmueble,IdCuenta,IdAsesor,PrecioAnterior,PrecioNuevo,Moneda,FechaCambioUtc,Motivo,Origen,EsDatoConfiable)
  VALUES(@idInmueble,@cuenta,@responsable,NULL,@precio,'MXN',@ahora,N'Precio inicial confiable al publicar el borrador.','PUBLICACION',1);
  COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK; THROW;
 END CATCH;

 SELECT idInmueble,IdCuenta,idAsesor AS IdAsesorResponsable,@actor AS IdAsesorActor,EstadoCodigo,VisibilidadCodigo,
        TRY_CONVERT(decimal(18,2),precio) PrecioPublicado,FechaPublicacionUtc,@fotos Fotos,
        'OK - BORRADOR PUBLICADO CON LINEA BASE Y AUDITORIA' EstadoPublicacion
 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden
 @idInmueble int,@orden int
AS
BEGIN
 SET NOCOUNT ON;
 IF @orden<1 OR @orden>20 RETURN;
 ;WITH f AS
 (
  SELECT im.IdImagen,im.IdInmueble,im.ClaveAlmacenamiento,im.NombreOriginal,im.MimeType,im.Bytes,im.Orden,im.EsPortada,im.FechaAltaUtc,
         ROW_NUMBER() OVER(ORDER BY im.EsPortada DESC,im.Orden,im.IdImagen) Posicion
  FROM dbo.RSMAPS_InmuebleImagen im JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble=im.IdInmueble
  WHERE im.IdInmueble=@idInmueble AND im.Activo=1 AND i.EstadoCodigo='PUBLICADO' AND i.VisibilidadCodigo='PUBLICO'
 )
 SELECT IdImagen,IdInmueble,ClaveAlmacenamiento,NombreOriginal,MimeType,Bytes,Orden,EsPortada,FechaAltaUtc
 FROM f WHERE Posicion=@orden;
END;
GO

/* Prueba sobre un borrador listo; usa ROLLBACK. */
DECLARE @id int,@correo varchar(200),@m0 int,@h0 int,@p0 int;
SELECT TOP(1) @id=i.idInmueble,@correo=u.correo
FROM dbo.RSMAPS_Inmueble i JOIN dbo.RSMAPS_Usuario u ON u.idAsesor=i.idAsesor
WHERE i.EstadoCodigo='BORRADOR' AND i.idTipo>1 AND TRY_CONVERT(decimal(18,2),i.precio)>0
  AND (ISNULL(i.terreno,0)>0 OR ISNULL(i.construccion,0)>0) AND NULLIF(LTRIM(RTRIM(ISNULL(i.observaciones,''))),'') IS NOT NULL
  AND EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleImagen x WHERE x.IdInmueble=i.idInmueble AND x.Activo=1 AND x.EsPortada=1)
ORDER BY CASE WHEN i.idInmueble=176 THEN 0 ELSE 1 END,i.idInmueble;

IF @id IS NOT NULL
BEGIN
 SELECT @m0=COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO';
 SELECT @h0=COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@id;
 SELECT @p0=COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@id;
 BEGIN TRANSACTION;
 EXEC dbo.RSMAPS_sp_PublicarBorradorInmueble @correo=@correo,@idInmueble=@id;
 SELECT @id idInmueble,
        (SELECT EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id) EstadoDurante,
        (SELECT VisibilidadCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id) VisibilidadDurante,
        (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO') MarketplaceDurante,
        (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@id) HistorialEstadoDurante,
        (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@id) HistorialPrecioDurante,
        CASE WHEN (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO')=@m0+1
               AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@id)=@h0+1
               AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@id)=@p0+1
             THEN 'OK - PUBLICACION COMPLETA CON HISTORIAL' ELSE 'REVISAR' END EstadoPrueba;
 ROLLBACK;
 SELECT (SELECT EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id) EstadoDespues,
        (SELECT VisibilidadCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id) VisibilidadDespues,
        CASE WHEN (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@id)=@h0
               AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@id)=@p0
               AND (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO')=@m0
             THEN 'OK - ROLLBACK COMPLETO' ELSE 'REVISAR' END EstadoRollback;
END
ELSE
 SELECT 'OK - PROCEDIMIENTOS INSTALADOS; NO HAY BORRADOR LISTO PARA PRUEBA' EstadoPaso33;

SELECT RolCodigo,PermisoCodigo FROM dbo.RSMAPS_RolPermiso
WHERE PermisoCodigo='INMUEBLE_PUBLICAR_BORRADOR_PROPIO' ORDER BY RolCodigo;
