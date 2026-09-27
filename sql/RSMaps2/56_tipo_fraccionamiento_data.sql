/* ============================================================
   RSMaps 2.0 - Paso 56
   TIPO DE FRACCIONAMIENTO AUTORITATIVO EN INMUEBLE

   - Cambio aditivo e idempotente.
   - Sin backfill: los registros existentes permanecen NULL.
   - Un consumidor legacy que omite los parametros nuevos conserva
     el valor previamente almacenado.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 55600, 'Este script debe ejecutarse en mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 55601, 'Falta dbo.RSMAPS_Inmueble.', 1;
GO

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'TipoFraccionamientoCodigo') IS NULL
    ALTER TABLE dbo.RSMAPS_Inmueble ADD TipoFraccionamientoCodigo VARCHAR(20) NULL;
GO

IF OBJECT_ID(N'dbo.CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo', N'C') IS NULL
BEGIN
    ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
    ADD CONSTRAINT CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo
        CHECK (TipoFraccionamientoCodigo IS NULL OR (
            DATALENGTH(TipoFraccionamientoCodigo)=7
            AND TipoFraccionamientoCodigo COLLATE Latin1_General_100_BIN2 IN ('PRIVADO','ABIERTO')));
END;

ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
    CHECK CONSTRAINT CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo;
GO

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
    WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 AND cu.EsPredeterminada=1
    ORDER BY cu.IdCuenta;

    IF @cuenta IS NULL
       AND (SELECT COUNT(*)
            FROM dbo.RSMAPS_CuentaUsuario cu
            JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
            WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1)=1
    BEGIN
        SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
        FROM dbo.RSMAPS_CuentaUsuario cu
        JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
        WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
        ORDER BY cu.IdCuenta;
    END;

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
           ISNULL(img.Imagenes,0) Imagenes,i.EstadoCodigo,i.VisibilidadCodigo,i.FechaUltimaEdicionUtc,
           i.TipoFraccionamientoCodigo
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad=i.idTipo
    OUTER APPLY(SELECT MAX(Imagenes) Imagenes FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=i.idInmueble) img
    WHERE i.idInmueble=@idInmueble;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GuardarBorradorInmueble
    @correo VARCHAR(200),
    @idInmueble INT,
    @direccion VARCHAR(MAX)=NULL,
    @idTipo INT,
    @terreno FLOAT=NULL,
    @construccion FLOAT=NULL,
    @precio DECIMAL(18,2)=NULL,
    @observaciones VARCHAR(MAX)=NULL,
    @notasPrivadas NVARCHAR(MAX)=NULL,
    @tipoFraccionamientoCodigo VARCHAR(20)=NULL,
    @actualizarTipoFraccionamiento BIT=0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @actor INT,@cuenta INT,@rol VARCHAR(30),@ci INT,@responsable INT,@estado VARCHAR(20);
    DECLARE @precioAnterior DECIMAL(18,2),@ahora DATETIME2(0)=SYSUTCDATETIME();

    SET @tipoFraccionamientoCodigo=NULLIF(@tipoFraccionamientoCodigo,'');

    IF @idTipo IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad=@idTipo)
        THROW 52930,'El tipo de propiedad seleccionado no existe.',1;
    IF @terreno IS NOT NULL AND @terreno<0 THROW 52931,'El terreno no puede ser negativo.',1;
    IF @construccion IS NOT NULL AND @construccion<0 THROW 52932,'La construccion no puede ser negativa.',1;
    IF @precio IS NOT NULL AND @precio<0 THROW 52933,'El precio no puede ser negativo.',1;
    IF @actualizarTipoFraccionamiento=1 AND @tipoFraccionamientoCodigo IS NOT NULL
       AND (DATALENGTH(@tipoFraccionamientoCodigo)<>7
            OR @tipoFraccionamientoCodigo COLLATE Latin1_General_100_BIN2 NOT IN('PRIVADO','ABIERTO'))
        THROW 55604,'El tipo de fraccionamiento no es valido.',1;

    SELECT @actor=idAsesor FROM dbo.RSMAPS_Usuario WHERE correo=@correo;
    SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1 AND cu.EsPredeterminada=1
    ORDER BY cu.IdCuenta;

    IF @cuenta IS NULL
       AND (SELECT COUNT(*)
            FROM dbo.RSMAPS_CuentaUsuario cu
            JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
            WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1)=1
    BEGIN
        SELECT TOP(1) @cuenta=cu.IdCuenta,@rol=cu.RolCodigo
        FROM dbo.RSMAPS_CuentaUsuario cu
        JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
        WHERE cu.IdAsesor=@actor AND cu.Activo=1 AND c.Activo=1
        ORDER BY cu.IdCuenta;
    END;

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
        TipoFraccionamientoCodigo=CASE WHEN @actualizarTipoFraccionamiento=1 THEN @tipoFraccionamientoCodigo ELSE TipoFraccionamientoCodigo END,
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

    SELECT idInmueble,EstadoCodigo,VisibilidadCodigo,precio,FechaPublicacionUtc,FechaUltimaEdicionUtc,TipoFraccionamientoCodigo
    FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble;
END;
GO

SELECT
    COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'TipoFraccionamientoCodigo') AS TipoFraccionamientoCodigoBytes,
    OBJECT_ID(N'dbo.CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo', N'C') AS CheckTipoFraccionamiento,
    OBJECT_ID(N'dbo.RSMAPS_sp_ObtenerBorradorInmueble', N'P') AS ObtenerBorrador,
    OBJECT_ID(N'dbo.RSMAPS_sp_GuardarBorradorInmueble', N'P') AS GuardarBorrador;
GO
