/* ============================================================
   RSMaps 2.0 - Paso 29
   CAPTURA PROGRESIVA DE BORRADORES

   Objetivo:
   - Permitir completar un BORRADOR por etapas sin publicarlo.
   - Mantener EstadoCodigo = BORRADOR y VisibilidadCodigo = CUENTA.
   - Guardar datos parciales y continuar despues.
   - No registrar historial de precio mientras siga en BORRADOR; la linea
     base comercial se registrara al publicar.
   - Separar NotasPrivadas de contacto_a para evitar exponer informacion
     interna a lecturas legacy/publicas.
   - Registrar FechaUltimaEdicionUtc para UX/autoguardado futuro.

   Politica inicial:
   - ASESOR, ADMINISTRADOR y PROPIETARIO editan sus propios borradores.
   - Edicion de borradores ajenos se habilitara despues con permiso explicito.

   Las pruebas usan ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52900, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 52901, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 52902, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL OR OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 52903, 'Falta la base RBAC.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_sp_CrearBorradorInmueble', N'P') IS NULL
    THROW 52904, 'Falta el Paso 27: RSMAPS_sp_CrearBorradorInmueble.', 1;

/* ============================================================
   1. Columnas modernas del borrador
   ============================================================ */
IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimaEdicionUtc') IS NULL
    ALTER TABLE dbo.RSMAPS_Inmueble ADD FechaUltimaEdicionUtc datetime2(0) NULL;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'NotasPrivadas') IS NULL
    ALTER TABLE dbo.RSMAPS_Inmueble ADD NotasPrivadas nvarchar(max) NULL;
GO

/* ============================================================
   2. Permiso explicito
   ============================================================ */
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_EDITAR_BORRADOR_PROPIO')
BEGIN
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion, Activo)
    VALUES
    (
        'INMUEBLE_EDITAR_BORRADOR_PROPIO',
        N'Editar borrador propio',
        N'Permite completar progresivamente un inmueble BORRADOR propio sin publicarlo.',
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ASESOR' AND PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo) VALUES ('ASESOR','INMUEBLE_EDITAR_BORRADOR_PROPIO');
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='ADMINISTRADOR' AND PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo) VALUES ('ADMINISTRADOR','INMUEBLE_EDITAR_BORRADOR_PROPIO');
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_RolPermiso WHERE RolCodigo='PROPIETARIO' AND PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO')
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo) VALUES ('PROPIETARIO','INMUEBLE_EDITAR_BORRADOR_PROPIO');
GO

/* ============================================================
   3. Lectura segura de borrador propio
   ============================================================ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerBorradorInmueble
    @correo VARCHAR(200),
    @idInmueble INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 52920, 'No existe un usuario RSMaps con el correo autenticado.', 1;

    SELECT TOP (1) @IdCuenta=cu.IdCuenta, @RolCodigo=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1 AND cu.EsPredeterminada=1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas=COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
        WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1;

        IF @MembresiasActivas=1
        BEGIN
            SELECT TOP (1) @IdCuenta=cu.IdCuenta, @RolCodigo=cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
            WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1;
        END
        ELSE IF @MembresiasActivas=0
            THROW 52921, 'El usuario no pertenece a ninguna cuenta activa.', 1;
        ELSE
            THROW 52922, 'El usuario pertenece a varias cuentas y no tiene una predeterminada.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo
        WHERE rp.RolCodigo=@RolCodigo
          AND rp.PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO'
          AND p.Activo=1
    )
        THROW 52923, 'El rol actual no tiene permiso para editar borradores propios.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble)
        THROW 52924, 'El inmueble no existe.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND IdCuenta<>@IdCuenta)
        THROW 52925, 'El inmueble pertenece a otra cuenta.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND idAsesor<>@IdAsesor)
        THROW 52926, 'Solo el asesor responsable puede completar este borrador en esta etapa.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND EstadoCodigo<>'BORRADOR')
        THROW 52927, 'El inmueble ya no se encuentra en estado BORRADOR.', 1;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        tp.nombre AS TipoNombre,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.NotasPrivadas,
        ISNULL(img.Imagenes,0) AS Imagenes,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaUltimaEdicionUtc
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad=i.idTipo
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble=i.idInmueble
    ) img
    WHERE i.idInmueble=@idInmueble;
END;
GO

/* ============================================================
   4. Guardado progresivo seguro
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

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;
    DECLARE @AhoraUtc DATETIME2(0)=SYSUTCDATETIME();

    IF @idTipo IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad=@idTipo)
        THROW 52930, 'El tipo de propiedad seleccionado no existe.', 1;
    IF @terreno IS NOT NULL AND @terreno<0
        THROW 52931, 'El terreno no puede ser negativo.', 1;
    IF @construccion IS NOT NULL AND @construccion<0
        THROW 52932, 'La construccion no puede ser negativa.', 1;
    IF @precio IS NOT NULL AND @precio<0
        THROW 52933, 'El precio no puede ser negativo.', 1;

    SELECT @IdAsesor=u.idAsesor FROM dbo.RSMAPS_Usuario u WHERE u.correo=@correo;
    IF @IdAsesor IS NULL
        THROW 52920, 'No existe un usuario RSMaps con el correo autenticado.', 1;

    SELECT TOP (1) @IdCuenta=cu.IdCuenta, @RolCodigo=cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
    WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1 AND cu.EsPredeterminada=1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas=COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
        WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1;

        IF @MembresiasActivas=1
        BEGIN
            SELECT TOP (1) @IdCuenta=cu.IdCuenta, @RolCodigo=cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta=cu.IdCuenta
            WHERE cu.IdAsesor=@IdAsesor AND cu.Activo=1 AND c.Activo=1;
        END
        ELSE IF @MembresiasActivas=0
            THROW 52921, 'El usuario no pertenece a ninguna cuenta activa.', 1;
        ELSE
            THROW 52922, 'El usuario pertenece a varias cuentas y no tiene una predeterminada.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo=rp.PermisoCodigo
        WHERE rp.RolCodigo=@RolCodigo
          AND rp.PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO'
          AND p.Activo=1
    )
        THROW 52923, 'El rol actual no tiene permiso para editar borradores propios.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble)
        THROW 52924, 'El inmueble no existe.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND IdCuenta<>@IdCuenta)
        THROW 52925, 'El inmueble pertenece a otra cuenta.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND idAsesor<>@IdAsesor)
        THROW 52926, 'Solo el asesor responsable puede completar este borrador en esta etapa.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@idInmueble AND EstadoCodigo<>'BORRADOR')
        THROW 52927, 'El inmueble ya no se encuentra en estado BORRADOR.', 1;

    UPDATE dbo.RSMAPS_Inmueble
    SET
        direccion=CASE WHEN NULLIF(LTRIM(RTRIM(@direccion)),'') IS NULL THEN 'Ubicacion registrada en mapa' ELSE LTRIM(RTRIM(@direccion)) END,
        idTipo=@idTipo,
        terreno=ISNULL(@terreno,0),
        construccion=ISNULL(@construccion,0),
        precio=ISNULL(CONVERT(float,@precio),0),
        observaciones=NULLIF(LTRIM(RTRIM(@observaciones)),''),
        NotasPrivadas=NULLIF(LTRIM(RTRIM(@notasPrivadas)),N''),
        FechaUltimaEdicionUtc=@AhoraUtc
    WHERE idInmueble=@idInmueble;

    SELECT idInmueble, EstadoCodigo, VisibilidadCodigo, precio, FechaPublicacionUtc, FechaUltimaEdicionUtc
    FROM dbo.RSMAPS_Inmueble
    WHERE idInmueble=@idInmueble;
END;
GO

/* ============================================================
   5. Prueba controlada
   ============================================================ */
DECLARE @CorreoPrueba varchar(200)='profesor76@hotmail.com';
DECLARE @IdTipoPrueba int;
DECLARE @IdNuevo int;
DECLARE @InventarioAntes int;
DECLARE @MarketplaceAntes int;
DECLARE @MarketplaceDurante int;
DECLARE @HistorialPrecioDurante int;
DECLARE @HistorialEstadoDurante int;

SELECT TOP (1) @IdTipoPrueba=idTipoPropiedad
FROM dbo.RSMAPS_TipoPropiedades
WHERE idTipoPropiedad>1
ORDER BY idTipoPropiedad;

IF @IdTipoPrueba IS NULL
    THROW 52940, 'No existe tipo de propiedad para la prueba.', 1;

SELECT @InventarioAntes=COUNT(*) FROM dbo.RSMAPS_Inmueble;
SELECT @MarketplaceAntes=COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO';

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CrearBorradorInmueble
    @correo=@CorreoPrueba,
    @lat=24.030000,
    @lng=-104.650000,
    @idTipo=@IdTipoPrueba,
    @idInmueble=@IdNuevo OUTPUT;

EXEC dbo.RSMAPS_sp_GuardarBorradorInmueble
    @correo=@CorreoPrueba,
    @idInmueble=@IdNuevo,
    @direccion='PRUEBA CAPTURA PROGRESIVA - ROLLBACK',
    @idTipo=@IdTipoPrueba,
    @terreno=250,
    @construccion=180,
    @precio=3456789,
    @observaciones='Descripcion temporal de prueba progresiva.',
    @notasPrivadas=N'Nota privada temporal que no debe salir al marketplace.';

SELECT @MarketplaceDurante=COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO';
SELECT @HistorialPrecioDurante=COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble=@IdNuevo;
SELECT @HistorialEstadoDurante=COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble=@IdNuevo;

SELECT
    i.idInmueble,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.direccion,
    i.terreno,
    i.construccion,
    i.precio,
    i.observaciones,
    i.NotasPrivadas,
    i.contacto_a AS ContactoLegacyIntacto,
    i.FechaPublicacionUtc,
    i.FechaUltimaEdicionUtc,
    @HistorialEstadoDurante AS HistorialEstado,
    @HistorialPrecioDurante AS HistorialPrecio,
    CASE
        WHEN i.EstadoCodigo='BORRADOR'
         AND i.VisibilidadCodigo='CUENTA'
         AND i.FechaPublicacionUtc IS NULL
         AND TRY_CONVERT(decimal(18,2),i.precio)=3456789
         AND i.NotasPrivadas=N'Nota privada temporal que no debe salir al marketplace.'
         AND @HistorialEstadoDurante=1
         AND @HistorialPrecioDurante=0
         AND @MarketplaceDurante=@MarketplaceAntes
        THEN 'OK - BORRADOR GUARDADO SIN PUBLICAR NI EXPONER NOTAS PRIVADAS'
        ELSE 'REVISAR'
    END AS EstadoPrueba
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble=@IdNuevo;

ROLLBACK TRANSACTION;

SELECT
    @InventarioAntes AS InventarioAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble) AS InventarioDespues,
    @MarketplaceAntes AS MarketplaceAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO') AS MarketplaceDespues,
    CASE
        WHEN @InventarioAntes=(SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble)
         AND @MarketplaceAntes=(SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE EstadoCodigo='PUBLICADO' AND VisibilidadCodigo='PUBLICO')
         AND NOT EXISTS(SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble=@IdNuevo)
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

SELECT rp.RolCodigo, rp.PermisoCodigo
FROM dbo.RSMAPS_RolPermiso rp
WHERE rp.PermisoCodigo='INMUEBLE_EDITAR_BORRADOR_PROPIO'
ORDER BY rp.RolCodigo;

PRINT 'Paso 29 RSMaps 2.0 terminado correctamente.';
