/* ============================================================
   RSMaps 2.0 - Paso 46
   EDICION SEGURA DE INMUEBLES PROPIOS ACTIVOS

   Objetivo:
   - Permitir editar un inmueble propio en BORRADOR, PUBLICADO,
     PAUSADO o RETIRADO sin cambiar su estado comercial.
   - Mantener VENDIDO/RENTADO como estados terminales no editables.
   - Mantener historial de precio cuando cambia una propiedad ya
     comercializada.
   - Permitir administrar fotos y caracteristicas en los mismos
     estados editables.
   - Mantener autorizacion por cuenta + asesor responsable + permiso.

   Nota de compatibilidad:
   Se conservan los nombres historicos de los procedimientos
   "...Borrador..." porque el codigo web existente ya los consume.
   Este paso amplia su semantica sin romper clientes anteriores.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 54600, 'Este script debe ejecutarse en mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 54601, 'Falta dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL OR OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 54602, 'Faltan tablas de permisos RSMaps.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL
    THROW 54603, 'Falta dbo.RSMAPS_InmuebleImagen. Ejecutar primero Paso 32.', 1;
GO

/* ============================================================
   1. Permiso explicito para editar inmueble propio
   ============================================================ */
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo='INMUEBLE_EDITAR_PROPIO')
BEGIN
    INSERT dbo.RSMAPS_Permiso(Codigo,Nombre,Descripcion,Activo)
    VALUES('INMUEBLE_EDITAR_PROPIO',N'Editar inmueble propio',N'Permite editar datos, caracteristicas y fotos de un inmueble propio mientras no tenga una operacion cerrada.',1);
END
ELSE
BEGIN
    UPDATE dbo.RSMAPS_Permiso
    SET Nombre=N'Editar inmueble propio',
        Descripcion=N'Permite editar datos, caracteristicas y fotos de un inmueble propio mientras no tenga una operacion cerrada.',
        Activo=1
    WHERE Codigo='INMUEBLE_EDITAR_PROPIO';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ASESOR' AND PermisoCodigo='INMUEBLE_EDITAR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso(RolCodigo,PermisoCodigo) VALUES('ASESOR','INMUEBLE_EDITAR_PROPIO');
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ADMINISTRADOR' AND PermisoCodigo='INMUEBLE_EDITAR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso(RolCodigo,PermisoCodigo) VALUES('ADMINISTRADOR','INMUEBLE_EDITAR_PROPIO');
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='PROPIETARIO' AND PermisoCodigo='INMUEBLE_EDITAR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso(RolCodigo,PermisoCodigo) VALUES('PROPIETARIO','INMUEBLE_EDITAR_PROPIO');
GO

/* ============================================================
   2. Leer inmueble propio editable
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerBorradorInmueble
    @correo VARCHAR(200),
    @idInmueble INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20);

    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
    ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;

    SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo
    FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;

    IF @actor IS NULL OR @cuenta IS NULL THROW 52921,'Sesion de trabajo invalida.',1;
    IF @ci IS NULL THROW 52924,'El inmueble no existe.',1;
    IF @ci<>@cuenta THROW 52925,'El inmueble pertenece a otra cuenta.',1;
    IF @responsable<>@actor THROW 52926,'Solo el asesor responsable puede editar este inmueble.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 52927,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(
        SELECT 1 FROM dbo.RSMAPS_RolPermiso rp
        JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1
        WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO')
        THROW 52923,'Rol sin permiso para editar inmuebles propios.',1;

    SELECT i.idInmueble,i.IdCuenta,i.idAsesor,i.direccion,i.lat,i.lng,i.idTipo,tp.nombre TipoNombre,
           i.terreno,i.construccion,i.precio,i.Recamaras,i.BanosCompletos,i.MediosBanos,
           i.Estacionamientos,i.Niveles,i.AntiguedadAnos,i.observaciones,i.NotasPrivadas,
           ISNULL(img.Imagenes,0) Imagenes,i.EstadoCodigo,i.VisibilidadCodigo,i.FechaUltimaEdicionUtc
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad=i.idTipo
    OUTER APPLY(SELECT MAX(Imagenes) Imagenes FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=i.idInmueble) img
    WHERE i.idInmueble=@idInmueble;
END;
GO

/* ============================================================
   3. Guardar datos basicos sin cambiar estado comercial
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GuardarBorradorInmueble
    @correo VARCHAR(200),
    @idInmueble INT,
    @direccion VARCHAR(MAX)=NULL,
    @idTipo INT,
    @terreno FLOAT=NULL,
    @construccion FLOAT=NULL,
    @precio DECIMAL(18,2)=NULL,
    @observaciones VARCHAR(MAX)=NULL,
    @notasPrivadas NVARCHAR(MAX)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20);
    DECLARE @precioAnterior DECIMAL(18,2),@ahora DATETIME2(0)=SYSUTCDATETIME();

    IF @idTipo IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad=@idTipo)
        THROW 52930,'El tipo de propiedad seleccionado no existe.',1;
    IF @terreno IS NOT NULL AND @terreno<0 THROW 52931,'El terreno no puede ser negativo.',1;
    IF @construccion IS NOT NULL AND @construccion<0 THROW 52932,'La construccion no puede ser negativa.',1;
    IF @precio IS NOT NULL AND @precio<0 THROW 52933,'El precio no puede ser negativo.',1;

    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
    ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;

    SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo,@precioAnterior=CONVERT(decimal(18,2),precio)
    FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;

    IF @actor IS NULL OR @cuenta IS NULL THROW 52921,'Sesion de trabajo invalida.',1;
    IF @ci IS NULL THROW 52924,'El inmueble no existe.',1;
    IF @ci<>@cuenta THROW 52925,'El inmueble pertenece a otra cuenta.',1;
    IF @responsable<>@actor THROW 52926,'Solo el asesor responsable puede editar este inmueble.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 52927,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(
        SELECT 1 FROM dbo.RSMAPS_RolPermiso rp
        JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1
        WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO')
        THROW 52923,'Rol sin permiso para editar inmuebles propios.',1;

    /* Una propiedad que ya salio del borrador debe conservar requisitos minimos. */
    IF @estado<>'BORRADOR' AND ISNULL(@precio,0)<=0 THROW 52934,'Una propiedad comercializada debe conservar precio mayor que cero.',1;
    IF @estado<>'BORRADOR' AND ISNULL(@terreno,0)<=0 AND ISNULL(@construccion,0)<=0 THROW 52935,'Una propiedad comercializada debe conservar al menos una superficie.',1;
    IF @estado<>'BORRADOR' AND NULLIF(LTRIM(RTRIM(@observaciones)),'') IS NULL THROW 52936,'Una propiedad comercializada debe conservar descripcion.',1;

    BEGIN TRANSACTION;

    UPDATE dbo.RSMAPS_Inmueble
    SET direccion=CASE WHEN NULLIF(LTRIM(RTRIM(@direccion)),'') IS NULL THEN 'Ubicacion registrada en mapa' ELSE LTRIM(RTRIM(@direccion)) END,
        idTipo=@idTipo,
        terreno=ISNULL(@terreno,0),
        construccion=ISNULL(@construccion,0),
        precio=ISNULL(CONVERT(float,@precio),0),
        observaciones=NULLIF(LTRIM(RTRIM(@observaciones)),''),
        NotasPrivadas=NULLIF(LTRIM(RTRIM(@notasPrivadas)),N''),
        FechaUltimaEdicionUtc=@ahora
    WHERE idInmueble=@idInmueble;

    IF @estado<>'BORRADOR' AND ISNULL(@precioAnterior,-1)<>ISNULL(@precio,-1)
       AND OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioHistorial',N'U') IS NOT NULL
    BEGIN
        INSERT dbo.RSMAPS_InmueblePrecioHistorial
            (IdInmueble,IdCuenta,IdAsesor,PrecioAnterior,PrecioNuevo,Moneda,FechaCambioUtc,Motivo,Origen,EsDatoConfiable)
        VALUES(@idInmueble,@cuenta,@responsable,@precioAnterior,@precio,'MXN',@ahora,N'Edicion del inmueble por asesor responsable.','EDICION',1);
    END;

    COMMIT TRANSACTION;

    SELECT idInmueble,EstadoCodigo,VisibilidadCodigo,precio,FechaPublicacionUtc,FechaUltimaEdicionUtc
    FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
END;
GO

/* ============================================================
   4. Amenidades y caracteristicas en inmueble editable
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListarAmenidadesBorrador
    @correo VARCHAR(200),@idInmueble INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20);
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
    IF @actor IS NULL OR @cuenta IS NULL THROW 53520,'Sesion de trabajo invalida.',1;
    IF @ci IS NULL OR @ci<>@cuenta OR @responsable<>@actor OR @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53521,'No puedes editar las amenidades de este inmueble.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53522,'Rol sin permiso.',1;
    SELECT a.Codigo,a.Nombre,a.Grupo,a.Orden,CONVERT(bit,CASE WHEN ia.IdInmueble IS NULL THEN 0 ELSE 1 END) Seleccionada
    FROM dbo.RSMAPS_Amenidad a LEFT JOIN dbo.RSMAPS_InmuebleAmenidad ia ON ia.AmenidadCodigo=a.Codigo AND ia.IdInmueble=@idInmueble
    WHERE a.Activo=1 ORDER BY a.Grupo,a.Orden,a.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GuardarCaracteristicasBorrador
    @correo VARCHAR(200),@idInmueble INT,@recamaras SMALLINT=NULL,@banosCompletos SMALLINT=NULL,@mediosBanos SMALLINT=NULL,
    @estacionamientos SMALLINT=NULL,@niveles SMALLINT=NULL,@antiguedadAnos SMALLINT=NULL,@amenidadesJson NVARCHAR(MAX)=N'[]'
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF ISJSON(ISNULL(@amenidadesJson,N'[]'))<>1 THROW 53530,'Amenidades invalidas.',1;
    IF @recamaras NOT BETWEEN 0 AND 100 OR @banosCompletos NOT BETWEEN 0 AND 100 OR @mediosBanos NOT BETWEEN 0 AND 100 OR @estacionamientos NOT BETWEEN 0 AND 100 OR @niveles NOT BETWEEN 0 AND 100 OR @antiguedadAnos NOT BETWEEN 0 AND 500 THROW 53531,'Caracteristica fuera de rango.',1;
    DECLARE @s TABLE(Codigo VARCHAR(40) PRIMARY KEY);
    INSERT @s SELECT DISTINCT CONVERT(varchar(40),value) FROM OPENJSON(ISNULL(@amenidadesJson,N'[]'));
    IF EXISTS(SELECT 1 FROM @s s LEFT JOIN dbo.RSMAPS_Amenidad a ON a.Codigo=s.Codigo AND a.Activo=1 WHERE a.Codigo IS NULL) THROW 53532,'Amenidad inexistente.',1;

    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20);
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
    IF @actor IS NULL OR @cuenta IS NULL THROW 53520,'Sesion de trabajo invalida.',1;
    IF @ci IS NULL OR @ci<>@cuenta OR @responsable<>@actor OR @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53521,'No puedes editar estas caracteristicas.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53522,'Rol sin permiso.',1;

    BEGIN TRANSACTION;
    UPDATE dbo.RSMAPS_Inmueble
    SET Recamaras=@recamaras,BanosCompletos=@banosCompletos,MediosBanos=@mediosBanos,Estacionamientos=@estacionamientos,Niveles=@niveles,AntiguedadAnos=@antiguedadAnos,FechaUltimaEdicionUtc=SYSUTCDATETIME()
    WHERE idInmueble=@idInmueble;
    DELETE FROM dbo.RSMAPS_InmuebleAmenidad WHERE IdInmueble=@idInmueble AND AmenidadCodigo NOT IN(SELECT Codigo FROM @s);
    INSERT dbo.RSMAPS_InmuebleAmenidad(IdInmueble,AmenidadCodigo,IdAsesorCambio)
    SELECT @idInmueble,s.Codigo,@actor FROM @s s
    WHERE NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleAmenidad x WHERE x.IdInmueble=@idInmueble AND x.AmenidadCodigo=s.Codigo);
    COMMIT TRANSACTION;
END;
GO

/* ============================================================
   5. Fotos en inmueble editable
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_RegistrarFotoBorrador
    @correo VARCHAR(200),@idInmueble INT,@claveAlmacenamiento NVARCHAR(500),@nombreOriginal NVARCHAR(255)=NULL,
    @mimeType VARCHAR(100),@bytes BIGINT,@idImagen BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20),@orden INT,@total INT,@portada BIT;
    SET @idImagen=NULL;
    IF @bytes IS NULL OR @bytes<=0 OR @bytes>12582912 THROW 53220,'El tamano de la imagen no es valido.',1;
    IF @mimeType NOT IN('image/jpeg','image/png','image/webp') THROW 53221,'El formato de imagen no esta permitido.',1;
    IF NULLIF(LTRIM(RTRIM(@claveAlmacenamiento)),N'') IS NULL THROW 53222,'La clave de almacenamiento es obligatoria.',1;
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @ci=IdCuenta,@responsable=idAsesor,@estado=EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
    IF @actor IS NULL OR @cuenta IS NULL THROW 53224,'Sesion de trabajo invalida.',1;
    IF @ci IS NULL THROW 53225,'El inmueble no existe.',1;
    IF @ci<>@cuenta THROW 53226,'El inmueble pertenece a otra cuenta.',1;
    IF @responsable<>@actor THROW 53227,'Solo el asesor responsable puede administrar fotos.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53228,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53229,'Rol sin permiso para editar fotos.',1;

    BEGIN TRANSACTION;
    SELECT @total=COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WITH(UPDLOCK,HOLDLOCK) WHERE IdInmueble=@idInmueble AND Activo=1;
    IF @total>=20 THROW 53230,'El inmueble ya tiene el maximo de 20 fotos.',1;
    SELECT @orden=ISNULL(MAX(Orden),0)+1 FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1;
    SET @portada=CASE WHEN @total=0 THEN 1 ELSE 0 END;
    INSERT dbo.RSMAPS_InmuebleImagen(IdInmueble,IdCuenta,ClaveAlmacenamiento,NombreOriginal,MimeType,Bytes,Orden,EsPortada,Activo)
    VALUES(@idInmueble,@cuenta,@claveAlmacenamiento,NULLIF(@nombreOriginal,N''),@mimeType,@bytes,@orden,@portada,1);
    SET @idImagen=SCOPE_IDENTITY();
    SET @total=@total+1;
    IF EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble=@idInmueble)
        UPDATE dbo.RSMAPS_InmuebleImagenes SET Imagenes=@total WHERE idInmueble=@idInmueble;
    ELSE
        INSERT dbo.RSMAPS_InmuebleImagenes(idInmueble,Imagenes) VALUES(@idInmueble,@total);
    UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc=SYSUTCDATETIME() WHERE idInmueble=@idInmueble;
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_EstablecerPortadaBorrador
    @correo VARCHAR(200),@idInmueble INT,@idImagen BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@estado VARCHAR(20);
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @estado=i.EstadoCodigo FROM dbo.RSMAPS_Inmueble i WHERE i.idInmueble=@idInmueble AND i.IdCuenta=@cuenta AND i.idAsesor=@actor;
    IF @estado IS NULL THROW 53250,'No tienes acceso al inmueble.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53251,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53253,'Rol sin permiso para editar fotos.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleImagen WHERE IdImagen=@idImagen AND IdInmueble=@idInmueble AND Activo=1) THROW 53252,'La imagen no pertenece al inmueble.',1;
    BEGIN TRANSACTION;
    UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada=0 WHERE IdInmueble=@idInmueble AND Activo=1;
    UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada=1 WHERE IdImagen=@idImagen AND IdInmueble=@idInmueble AND Activo=1;
    UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc=SYSUTCDATETIME() WHERE idInmueble=@idInmueble;
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_EliminarFotoBorrador
    @correo VARCHAR(200),@idInmueble INT,@idImagen BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@estado VARCHAR(20),@clave NVARCHAR(500),@eraPortada BIT,@total INT;
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @estado=i.EstadoCodigo FROM dbo.RSMAPS_Inmueble i WHERE i.idInmueble=@idInmueble AND i.IdCuenta=@cuenta AND i.idAsesor=@actor;
    IF @estado IS NULL THROW 53260,'No tienes acceso al inmueble.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53261,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53264,'Rol sin permiso para editar fotos.',1;
    SELECT @clave=ClaveAlmacenamiento,@eraPortada=EsPortada FROM dbo.RSMAPS_InmuebleImagen WHERE IdImagen=@idImagen AND IdInmueble=@idInmueble AND Activo=1;
    IF @clave IS NULL THROW 53262,'La imagen no existe o ya fue eliminada.',1;
    SELECT @total=COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1;
    IF @estado<>'BORRADOR' AND @total<=1 THROW 53263,'Una propiedad comercializada debe conservar al menos una foto.',1;

    BEGIN TRANSACTION;
    UPDATE dbo.RSMAPS_InmuebleImagen SET Activo=0,EsPortada=0 WHERE IdImagen=@idImagen AND IdInmueble=@idInmueble AND Activo=1;
    IF @eraPortada=1
    BEGIN
        DECLARE @nueva BIGINT;
        SELECT TOP(1) @nueva=IdImagen FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1 ORDER BY Orden,IdImagen;
        IF @nueva IS NOT NULL UPDATE dbo.RSMAPS_InmuebleImagen SET EsPortada=1 WHERE IdImagen=@nueva;
    END;
    SELECT @total=COUNT(*) FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1;
    IF EXISTS(SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble=@idInmueble)
        UPDATE dbo.RSMAPS_InmuebleImagenes SET Imagenes=@total WHERE idInmueble=@idInmueble;
    ELSE
        INSERT dbo.RSMAPS_InmuebleImagenes(idInmueble,Imagenes) VALUES(@idInmueble,@total);
    UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc=SYSUTCDATETIME() WHERE idInmueble=@idInmueble;
    COMMIT TRANSACTION;
    SELECT @clave AS ClaveAlmacenamiento;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_MoverFotoBorradorWeb
    @correo VARCHAR(200),@idInmueble INT,@idImagen BIGINT,@direccion INT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@estado VARCHAR(20),@ordenActual INT,@idDestino BIGINT,@ordenDestino INT;
    IF @direccion NOT IN(-1,1) THROW 53320,'La direccion debe ser -1 o 1.',1;
    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo FROM dbo.RSMAPS_CuentaUsuario cu JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 ORDER BY cu.EsPredeterminada DESC,cu.IdCuenta;
    SELECT @estado=i.EstadoCodigo FROM dbo.RSMAPS_Inmueble i WHERE i.idInmueble=@idInmueble AND i.IdCuenta=@cuenta AND i.idAsesor=@actor;
    IF @estado IS NULL THROW 53321,'No tienes acceso para ordenar fotos.',1;
    IF @estado NOT IN('BORRADOR','PUBLICADO','PAUSADO','RETIRADO') THROW 53322,'El inmueble no esta en un estado editable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_RolPermiso rp JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo AND p.Activo=1 WHERE rp.RolCodigo=@rol AND rp.PermisoCodigo='INMUEBLE_EDITAR_PROPIO') THROW 53324,'Rol sin permiso para editar fotos.',1;
    SELECT @ordenActual=Orden FROM dbo.RSMAPS_InmuebleImagen WHERE IdImagen=@idImagen AND IdInmueble=@idInmueble AND Activo=1;
    IF @ordenActual IS NULL THROW 53323,'La imagen no existe o no pertenece al inmueble.',1;
    IF @direccion=-1
        SELECT TOP(1) @idDestino=IdImagen,@ordenDestino=Orden FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1 AND Orden<@ordenActual ORDER BY Orden DESC,IdImagen DESC;
    ELSE
        SELECT TOP(1) @idDestino=IdImagen,@ordenDestino=Orden FROM dbo.RSMAPS_InmuebleImagen WHERE IdInmueble=@idInmueble AND Activo=1 AND Orden>@ordenActual ORDER BY Orden,IdImagen;
    IF @idDestino IS NULL RETURN;
    BEGIN TRANSACTION;
    UPDATE dbo.RSMAPS_InmuebleImagen SET Orden=@ordenDestino WHERE IdImagen=@idImagen;
    UPDATE dbo.RSMAPS_InmuebleImagen SET Orden=@ordenActual WHERE IdImagen=@idDestino;
    UPDATE dbo.RSMAPS_Inmueble SET FechaUltimaEdicionUtc=SYSUTCDATETIME() WHERE idInmueble=@idInmueble;
    COMMIT TRANSACTION;
END;
GO

/* ============================================================
   6. Verificacion no destructiva
   ============================================================ */
SELECT Codigo,Nombre,Activo FROM dbo.RSMAPS_Permiso WHERE Codigo='INMUEBLE_EDITAR_PROPIO';
SELECT RolCodigo,PermisoCodigo FROM dbo.RSMAPS_RolPermiso WHERE PermisoCodigo='INMUEBLE_EDITAR_PROPIO' ORDER BY RolCodigo;
SELECT
    OBJECT_ID(N'dbo.RSMAPS_sp_ObtenerBorradorInmueble',N'P') AS Obtener,
    OBJECT_ID(N'dbo.RSMAPS_sp_GuardarBorradorInmueble',N'P') AS Guardar,
    OBJECT_ID(N'dbo.RSMAPS_sp_GuardarCaracteristicasBorrador',N'P') AS Caracteristicas,
    OBJECT_ID(N'dbo.RSMAPS_sp_RegistrarFotoBorrador',N'P') AS RegistrarFoto,
    OBJECT_ID(N'dbo.RSMAPS_sp_EstablecerPortadaBorrador',N'P') AS Portada,
    OBJECT_ID(N'dbo.RSMAPS_sp_EliminarFotoBorrador',N'P') AS EliminarFoto,
    OBJECT_ID(N'dbo.RSMAPS_sp_MoverFotoBorradorWeb',N'P') AS MoverFoto,
    'OK - EDICION DE INMUEBLES PROPIOS ACTIVOS HABILITADA' AS EstadoPaso46;
