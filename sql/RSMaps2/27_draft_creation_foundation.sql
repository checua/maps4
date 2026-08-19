/* ============================================================
   RSMaps 2.0 - Paso 27
   FUNDACION DE ALTA MODERNA COMO BORRADOR

   Objetivo:
   - Crear un flujo nuevo de alta SIN romper RSMAPS_sp_insertar_coordenadas.
   - Una propiedad nueva nace BORRADOR + CUENTA.
   - La identidad, cuenta y asesor responsable se resuelven en servidor/SQL.
   - La ubicacion es obligatoria y el tipo se elige al crear el borrador.
   - Precio, superficies, fotos y descripcion pueden completarse despues.
   - El borrador NO aparece en el marketplace publico.
   - Registrar historial inicial real de estado/auditoria.
   - No crear historial de precio para el valor provisional 0.

   Politica inicial:
   - ASESOR, ADMINISTRADOR y PROPIETARIO pueden crear borradores propios.
   - CAPTURISTA se habilitara despues mediante un flujo explicito
     "capturar para otro asesor" para no atribuirle inventario por error.

   Las pruebas usan ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 52701, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 52702, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL OR OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 52703, 'Falta la base RBAC. Ejecutar primero Paso 21.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado', N'U') IS NULL
    THROW 52704, 'Falta historial de estados. Ejecutar primero Paso 11.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_InmueblePrecioHistorial', N'U') IS NULL
    THROW 52705, 'Falta historial de precios. Ejecutar primero Paso 15.', 1;

/* ============================================================
   1. Permiso explicito para crear borrador propio
   ============================================================ */
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_CREAR_PROPIO')
BEGIN
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion, Activo)
    VALUES
    (
        'INMUEBLE_CREAR_PROPIO',
        N'Crear borrador propio',
        N'Permite crear un inmueble BORRADOR asignado al usuario autenticado dentro de su cuenta activa.',
        1
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ASESOR' AND PermisoCodigo = 'INMUEBLE_CREAR_PROPIO'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ASESOR', 'INMUEBLE_CREAR_PROPIO');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ADMINISTRADOR' AND PermisoCodigo = 'INMUEBLE_CREAR_PROPIO'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'INMUEBLE_CREAR_PROPIO');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'PROPIETARIO' AND PermisoCodigo = 'INMUEBLE_CREAR_PROPIO'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'INMUEBLE_CREAR_PROPIO');

/* ============================================================
   2. Procedimiento nuevo de alta como borrador
   ============================================================ */
DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_CrearBorradorInmueble
    @correo VARCHAR(200),
    @lat DECIMAL(10,6),
    @lng DECIMAL(10,6),
    @idTipo INT,
    @idInmueble INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @IdInmobiliariaLegacy INT;
    DECLARE @Telefono VARCHAR(MAX);
    DECLARE @MembresiasActivas INT;
    DECLARE @PuedeCrear BIT = 0;
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();

    SET @idInmueble = NULL;

    IF @lat IS NULL OR @lat < -90 OR @lat > 90
        THROW 52720, ''La latitud del borrador no es valida.'', 1;

    IF @lng IS NULL OR @lng < -180 OR @lng > 180
        THROW 52721, ''La longitud del borrador no es valida.'', 1;

    IF @idTipo IS NULL OR NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_TipoPropiedades
        WHERE idTipoPropiedad = @idTipo
    )
        THROW 52722, ''El tipo de propiedad seleccionado no existe.'', 1;

    SELECT
        @IdAsesor = u.idAsesor,
        @Telefono = u.telefono
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 52723, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo,
        @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @IdAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @IdCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo,
                @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @IdAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 52724, ''El usuario autenticado no pertenece a ninguna cuenta activa.'', 1;
        ELSE
            THROW 52725, ''El usuario pertenece a varias cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT @PuedeCrear = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INMUEBLE_CREAR_PROPIO''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @PuedeCrear = 0
        THROW 52726, ''El rol actual no tiene permiso para crear borradores propios.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.RSMAPS_Inmueble
        (
            idInmobiliaria,
            idAsesor,
            direccion,
            lat,
            lng,
            idTipo,
            telefono,
            terreno,
            construccion,
            precio,
            observaciones,
            exclusiva,
            link,
            contacto_a,
            IdCuenta,
            EstadoCodigo,
            VisibilidadCodigo,
            FechaPublicacionUtc,
            FechaUltimoCambioEstadoUtc
        )
        VALUES
        (
            @IdInmobiliariaLegacy,
            @IdAsesor,
            ''Ubicacion registrada en mapa'',
            @lat,
            @lng,
            @idTipo,
            @Telefono,
            0,
            0,
            0,
            NULL,
            1,
            ''BORRADOR'',
            @Telefono,
            @IdCuenta,
            ''BORRADOR'',
            ''CUENTA'',
            NULL,
            @AhoraUtc
        );

        SET @idInmueble = CONVERT(INT, SCOPE_IDENTITY());

        INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
        VALUES (@idInmueble, 0);

        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble,
            IdCuenta,
            EstadoAnterior,
            EstadoNuevo,
            VisibilidadAnterior,
            VisibilidadNueva,
            IdAsesorResponsable,
            IdAsesorCambio,
            FechaCambioUtc,
            Motivo,
            Origen
        )
        VALUES
        (
            @idInmueble,
            @IdCuenta,
            NULL,
            ''BORRADOR'',
            NULL,
            ''CUENTA'',
            @IdAsesor,
            @IdAsesor,
            @AhoraUtc,
            N''Borrador creado desde el flujo moderno de captura.'',
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor,
        i.lat,
        i.lng,
        i.idTipo,
        i.precio,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaPublicacionUtc,
        i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;
END;';

EXEC sys.sp_executesql @sql;

/* ============================================================
   3. Validar permiso instalado
   ============================================================ */
SELECT
    rp.RolCodigo,
    rp.PermisoCodigo
FROM dbo.RSMAPS_RolPermiso rp
WHERE rp.PermisoCodigo = 'INMUEBLE_CREAR_PROPIO'
ORDER BY rp.RolCodigo;

/* ============================================================
   4. Prueba controlada con ROLLBACK
   ============================================================ */
DECLARE @CorreoPrueba varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdAsesorPrueba int;
DECLARE @IdCuentaPrueba int;
DECLARE @IdTipoPrueba int;
DECLARE @IdNuevo int;
DECLARE @InventarioAntes int;
DECLARE @MarketplaceAntes int;
DECLARE @MarketplaceDurante int;
DECLARE @HistorialEstadoDurante int;
DECLARE @HistorialPrecioDurante int;
DECLARE @ImagenesDurante int;

SELECT @IdAsesorPrueba = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoPrueba;

IF @IdAsesorPrueba IS NULL
    THROW 52740, 'No existe el usuario configurado para la prueba.', 1;

SELECT TOP (1) @IdCuentaPrueba = cu.IdCuenta
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesorPrueba
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

SELECT TOP (1) @IdTipoPrueba = idTipoPropiedad
FROM dbo.RSMAPS_TipoPropiedades
ORDER BY idTipoPropiedad;

IF @IdCuentaPrueba IS NULL OR @IdTipoPrueba IS NULL
    THROW 52741, 'No fue posible resolver cuenta/tipo para la prueba.', 1;

SELECT @InventarioAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE IdCuenta = @IdCuentaPrueba
  AND idAsesor = @IdAsesorPrueba;

SELECT @MarketplaceAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CrearBorradorInmueble
    @correo = @CorreoPrueba,
    @lat = 24.027600,
    @lng = -104.653200,
    @idTipo = @IdTipoPrueba,
    @idInmueble = @IdNuevo OUTPUT;

SELECT @MarketplaceDurante = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT @HistorialEstadoDurante = COUNT(*)
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE IdInmueble = @IdNuevo;

SELECT @HistorialPrecioDurante = COUNT(*)
FROM dbo.RSMAPS_InmueblePrecioHistorial
WHERE IdInmueble = @IdNuevo;

SELECT @ImagenesDurante = ISNULL(MAX(Imagenes), -1)
FROM dbo.RSMAPS_InmuebleImagenes
WHERE idInmueble = @IdNuevo;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.precio,
    i.FechaPublicacionUtc,
    i.lat,
    i.lng,
    @HistorialEstadoDurante AS HistorialEstado,
    @HistorialPrecioDurante AS HistorialPrecio,
    @ImagenesDurante AS Imagenes,
    CASE
        WHEN i.EstadoCodigo = 'BORRADOR'
         AND i.VisibilidadCodigo = 'CUENTA'
         AND i.FechaPublicacionUtc IS NULL
         AND TRY_CONVERT(decimal(18,2), i.precio) = 0
         AND @HistorialEstadoDurante = 1
         AND @HistorialPrecioDurante = 0
         AND @ImagenesDurante = 0
         AND @MarketplaceDurante = @MarketplaceAntes
        THEN 'OK - BORRADOR PRIVADO CREADO CORRECTAMENTE'
        ELSE 'REVISAR'
    END AS EstadoPrueba
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdNuevo;

SELECT
    h.IdInmueble,
    h.EstadoAnterior,
    h.EstadoNuevo,
    h.VisibilidadAnterior,
    h.VisibilidadNueva,
    h.IdAsesorResponsable,
    h.IdAsesorCambio,
    h.Origen,
    CASE
        WHEN h.EstadoAnterior IS NULL
         AND h.EstadoNuevo = 'BORRADOR'
         AND h.VisibilidadNueva = 'CUENTA'
         AND h.IdAsesorResponsable = @IdAsesorPrueba
         AND h.IdAsesorCambio = @IdAsesorPrueba
         AND h.Origen = 'APLICACION'
        THEN 'OK - ALTA AUDITADA'
        ELSE 'REVISAR'
    END AS EstadoAuditoria
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdNuevo;

ROLLBACK TRANSACTION;

SELECT
    @InventarioAntes AS InventarioAntes,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_Inmueble
     WHERE IdCuenta = @IdCuentaPrueba
       AND idAsesor = @IdAsesorPrueba) AS InventarioDespues,
    @MarketplaceAntes AS MarketplaceAntes,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_Inmueble
     WHERE EstadoCodigo = 'PUBLICADO'
       AND VisibilidadCodigo = 'PUBLICO') AS MarketplaceDespues,
    CASE
        WHEN @InventarioAntes =
             (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble
              WHERE IdCuenta = @IdCuentaPrueba AND idAsesor = @IdAsesorPrueba)
         AND @MarketplaceAntes =
             (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble
              WHERE EstadoCodigo = 'PUBLICADO' AND VisibilidadCodigo = 'PUBLICO')
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

SELECT
    OBJECT_NAME(p.object_id) AS Procedimiento,
    p.parameter_id,
    p.name AS Parametro,
    TYPE_NAME(p.user_type_id) AS TipoDato,
    p.max_length AS LongitudBytes,
    p.is_output AS EsOutput
FROM sys.parameters p
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_CrearBorradorInmueble')
ORDER BY p.parameter_id;

PRINT 'Paso 27 RSMaps 2.0 terminado correctamente.';
