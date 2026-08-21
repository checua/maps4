/* RSMaps 2.0 - Paso 35: caracteristicas estructuradas y amenidades */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>'mapsMarkers' THROW 53500,'Ejecutar en mapsMarkers.',1;
GO

IF COL_LENGTH('dbo.RSMAPS_Inmueble','Recamaras') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD Recamaras smallint NULL;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','BanosCompletos') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD BanosCompletos smallint NULL;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','MediosBanos') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD MediosBanos smallint NULL;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','Estacionamientos') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD Estacionamientos smallint NULL;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','Niveles') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD Niveles smallint NULL;
IF COL_LENGTH('dbo.RSMAPS_Inmueble','AntiguedadAnos') IS NULL ALTER TABLE dbo.RSMAPS_Inmueble ADD AntiguedadAnos smallint NULL;
GO

IF OBJECT_ID('dbo.RSMAPS_Amenidad','U') IS NULL
CREATE TABLE dbo.RSMAPS_Amenidad(
 Codigo varchar(40) NOT NULL PRIMARY KEY,
 Nombre nvarchar(100) NOT NULL,
 Grupo varchar(30) NOT NULL,
 Activo bit NOT NULL DEFAULT(1),
 EsFiltro bit NOT NULL DEFAULT(1),
 Orden int NOT NULL);
GO

IF OBJECT_ID('dbo.RSMAPS_InmuebleAmenidad','U') IS NULL
CREATE TABLE dbo.RSMAPS_InmuebleAmenidad(
 IdInmueble int NOT NULL,
 AmenidadCodigo varchar(40) NOT NULL,
 IdAsesorCambio int NULL,
 FechaCambioUtc datetime2(0) NOT NULL DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT PK_RSMAPS_InmuebleAmenidad PRIMARY KEY(IdInmueble,AmenidadCodigo),
 CONSTRAINT FK_RSMAPS_InmuebleAmenidad_Inmueble FOREIGN KEY(IdInmueble) REFERENCES dbo.RSMAPS_Inmueble(idInmueble),
 CONSTRAINT FK_RSMAPS_InmuebleAmenidad_Amenidad FOREIGN KEY(AmenidadCodigo) REFERENCES dbo.RSMAPS_Amenidad(Codigo));
GO

DECLARE @a TABLE(Codigo varchar(40),Nombre nvarchar(100),Grupo varchar(30),Orden int);
INSERT @a VALUES
('JARDIN',N'Jardín','EXTERIOR',10),('TERRAZA',N'Terraza','EXTERIOR',20),('BALCON',N'Balcón','EXTERIOR',30),('PATIO',N'Patio','EXTERIOR',40),('ALBERCA',N'Alberca','EXTERIOR',50),('ROOF_GARDEN',N'Roof garden','EXTERIOR',60),
('CISTERNA',N'Cisterna','SERVICIOS',110),('TINACO',N'Tinaco','SERVICIOS',120),('AIRE_ACONDICIONADO',N'Aire acondicionado','SERVICIOS',130),('CALEFACCION',N'Calefacción','SERVICIOS',140),
('ACCESO_CONTROLADO',N'Acceso controlado','SEGURIDAD',210),('VIGILANCIA_24H',N'Vigilancia 24 h','SEGURIDAD',220),
('ELEVADOR',N'Elevador','ACCESIBILIDAD',310),('ACCESIBILIDAD',N'Accesibilidad','ACCESIBILIDAD',320),
('COCINA_EQUIPADA',N'Cocina equipada','EQUIPAMIENTO',410),('BODEGA',N'Bodega','EQUIPAMIENTO',420),('AREA_LAVADO',N'Área de lavado','EQUIPAMIENTO',430),('CUARTO_SERVICIO',N'Cuarto de servicio','EQUIPAMIENTO',440),('AMUEBLADO',N'Amueblado','EQUIPAMIENTO',450),('MASCOTAS',N'Acepta mascotas','OTROS',510);
INSERT dbo.RSMAPS_Amenidad(Codigo,Nombre,Grupo,Activo,EsFiltro,Orden)
SELECT Codigo,Nombre,Grupo,1,1,Orden FROM @a s WHERE NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_Amenidad x WHERE x.Codigo=s.Codigo);
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListarAmenidadesBorrador @correo varchar(200),@idInmueble int AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @actor int,@cuenta int,@ci int,@responsable int,@estado varchar(20);
 SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
 SELECT TOP(1) @cuenta=cu.IdCuenta FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
 SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
 IF @actor IS NULL OR @cuenta IS NULL THROW 53520,'Sesion de trabajo invalida.',1;
 IF @ci IS NULL OR @ci<>@cuenta OR @responsable<>@actor OR @estado<>'BORRADOR' THROW 53521,'No puedes editar estas amenidades.',1;
 SELECT a.Codigo,a.Nombre,a.Grupo,a.Orden,CONVERT(bit,CASE WHEN ia.IdInmueble IS NULL THEN 0 ELSE 1 END) Seleccionada
 FROM dbo.RSMAPS_Amenidad a LEFT JOIN dbo.RSMAPS_InmuebleAmenidad ia ON ia.AmenidadCodigo=a.Codigo AND ia.IdInmueble=@idInmueble
 WHERE a.Activo=1 ORDER BY a.Grupo,a.Orden,a.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GuardarCaracteristicasBorrador
 @correo varchar(200),@idInmueble int,@recamaras smallint=NULL,@banosCompletos smallint=NULL,@mediosBanos smallint=NULL,
 @estacionamientos smallint=NULL,@niveles smallint=NULL,@antiguedadAnos smallint=NULL,@amenidadesJson nvarchar(max)=N'[]' AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF ISJSON(ISNULL(@amenidadesJson,N'[]'))<>1 THROW 53530,'Amenidades invalidas.',1;
 IF @recamaras NOT BETWEEN 0 AND 100 OR @banosCompletos NOT BETWEEN 0 AND 100 OR @mediosBanos NOT BETWEEN 0 AND 100 OR @estacionamientos NOT BETWEEN 0 AND 100 OR @niveles NOT BETWEEN 0 AND 100 OR @antiguedadAnos NOT BETWEEN 0 AND 500 THROW 53531,'Caracteristica fuera de rango.',1;
 DECLARE @s TABLE(Codigo varchar(40) PRIMARY KEY);
 INSERT @s SELECT DISTINCT CONVERT(varchar(40),value) FROM OPENJSON(ISNULL(@amenidadesJson,N'[]'));
 IF EXISTS(SELECT 1 FROM @s s LEFT JOIN dbo.RSMAPS_Amenidad a ON a.Codigo=s.Codigo AND a.Activo=1 WHERE a.Codigo IS NULL) THROW 53532,'Amenidad inexistente.',1;
 DECLARE @actor int,@cuenta int,@rol varchar(30),@ci int,@responsable int,@estado varchar(20);
 SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
 SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
 SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
 IF @actor IS NULL OR @cuenta IS NULL THROW 53520,'Sesion de trabajo invalida.',1;
 IF @ci IS NULL OR @ci<>@cuenta OR @responsable<>@actor OR @estado<>'BORRADOR' THROW 53521,'No puedes editar estas caracteristicas.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO') THROW 53522,'Rol sin permiso.',1;
 BEGIN TRANSACTION;
 UPDATE dbo.RSMAPS_Inmueble SET Recamaras=@recamaras,BanosCompletos=@banosCompletos,MediosBanos=@mediosBanos,Estacionamientos=@estacionamientos,Niveles=@niveles,AntiguedadAnos=@antiguedadAnos,FechaUltimaEdicionUtc=SYSUTCDATETIME() WHERE idInmueble=@idInmueble;
 DELETE FROM dbo.RSMAPS_InmuebleAmenidad WHERE IdInmueble=@idInmueble AND AmenidadCodigo NOT IN(SELECT Codigo FROM @s);
 INSERT dbo.RSMAPS_InmuebleAmenidad(IdInmueble,AmenidadCodigo,IdAsesorCambio) SELECT @idInmueble,s.Codigo,@actor FROM @s s WHERE NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleAmenidad x WHERE x.IdInmueble=@idInmueble AND x.AmenidadCodigo=s.Codigo);
 COMMIT;
 SELECT idInmueble,Recamaras,BanosCompletos,MediosBanos,Estacionamientos,Niveles,AntiguedadAnos,(SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleAmenidad WHERE IdInmueble=@idInmueble) Amenidades,'OK - CARACTERISTICAS Y AMENIDADES GUARDADAS' EstadoGuardado FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerBorradorInmueble @correo varchar(200),@idInmueble int AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @actor int,@cuenta int,@rol varchar(30),@ci int,@responsable int,@estado varchar(20);
 SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
 SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
 SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
 IF @actor IS NULL OR @cuenta IS NULL THROW 52921,'Sesion de trabajo invalida.',1;
 IF @ci IS NULL THROW 52924,'El inmueble no existe.',1;
 IF @ci<>@cuenta THROW 52925,'El inmueble pertenece a otra cuenta.',1;
 IF @responsable<>@actor THROW 52926,'Solo el asesor responsable puede completar este borrador.',1;
 IF @estado<>'BORRADOR' THROW 52927,'El inmueble ya no es BORRADOR.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO') THROW 52923,'Rol sin permiso.',1;
 SELECT i.idInmueble,i.IdCuenta,i.idAsesor,i.direccion,i.lat,i.lng,i.idTipo,tp.nombre TipoNombre,i.terreno,i.construccion,i.precio,i.Recamaras,i.BanosCompletos,i.MediosBanos,i.Estacionamientos,i.Niveles,i.AntiguedadAnos,i.observaciones,i.NotasPrivadas,ISNULL(img.Imagenes,0) Imagenes,i.EstadoCodigo,i.VisibilidadCodigo,i.FechaUltimaEdicionUtc
 FROM dbo.RSMAPS_Inmueble i LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad=i.idTipo OUTER APPLY(SELECT MAX(Imagenes) Imagenes FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=i.idInmueble) img WHERE i.idInmueble=@idInmueble;
END;
GO

DECLARE @correo varchar(200)='profesor76@hotmail.com',@tipo int,@id int;
SELECT TOP(1) @tipo=idTipoPropiedad FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad>1 ORDER BY idTipoPropiedad;
BEGIN TRANSACTION;
EXEC dbo.RSMAPS_sp_CrearBorradorInmueble @correo=@correo,@lat=24.031,@lng=-104.651,@idTipo=@tipo,@idInmueble=@id OUTPUT;
EXEC dbo.RSMAPS_sp_GuardarCaracteristicasBorrador @correo=@correo,@idInmueble=@id,@recamaras=3,@banosCompletos=2,@mediosBanos=1,@estacionamientos=2,@niveles=2,@antiguedadAnos=8,@amenidadesJson=N'["JARDIN","CISTERNA","COCINA_EQUIPADA"]';
SELECT idInmueble,EstadoCodigo,VisibilidadCodigo,Recamaras,BanosCompletos,MediosBanos,Estacionamientos,Niveles,AntiguedadAnos,(SELECT STRING_AGG(AmenidadCodigo,',') FROM dbo.RSMAPS_InmuebleAmenidad WHERE IdInmueble=@id) Amenidades,'OK - DATOS ESTRUCTURADOS GUARDADOS SIN PUBLICAR' EstadoPrueba FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id;
ROLLBACK;
SELECT (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@id) InmueblePruebaRestante,(SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleAmenidad WHERE IdInmueble=@id) AmenidadesPruebaRestantes,'OK - ROLLBACK COMPLETO' EstadoRollback;
SELECT Codigo,Nombre,Grupo,Orden FROM dbo.RSMAPS_Amenidad WHERE Activo=1 ORDER BY Grupo,Orden;
