/* ============================================================
   RSMaps 2.0 - Paso 24
   AUTORIZACION DE ACCIONES DE EQUIPO CON AUDITORIA

   Objetivo:
   - Permitir a ADMINISTRADOR y PROPIETARIO/Titular de cuenta cambiar
     estado/visibilidad y cerrar operaciones de inmuebles de la misma cuenta.
   - Mantener ASESOR limitado a sus propios inmuebles.
   - Mantener CAPTURISTA sin permisos comerciales de cambio/cierre por ahora.
   - Conservar separado:
       * asesor responsable del inmueble
       * usuario/asesor actor de la accion
   - Validar el escenario cruzado con ROLLBACK.

   Requiere:
   - Paso 21: RSMAPS_Permiso / RSMAPS_RolPermiso
   - Paso 23: IdAsesorResponsable / IdAsesorActor
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52400, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 52401, 'Falta RSMAPS_RolPermiso. Ejecutar primero Paso 21.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL
    THROW 52402, 'Falta RSMAPS_Permiso. Ejecutar primero Paso 21.', 1;
IF COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') IS NULL
    THROW 52403, 'Falta IdAsesorResponsable. Ejecutar primero Paso 23.', 1;
IF COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') IS NULL
    THROW 52404, 'Falta IdAsesorActor. Ejecutar primero Paso 23.', 1;

/* ============================================================
   1. Activar permisos comerciales de cuenta
   ============================================================ */
IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ADMINISTRADOR'
      AND PermisoCodigo = 'INMUEBLE_CAMBIAR_ESTADO_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'INMUEBLE_CAMBIAR_ESTADO_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'PROPIETARIO'
      AND PermisoCodigo = 'INMUEBLE_CAMBIAR_ESTADO_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'INMUEBLE_CAMBIAR_ESTADO_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ADMINISTRADOR'
      AND PermisoCodigo = 'OPERACION_CERRAR_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'OPERACION_CERRAR_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'PROPIETARIO'
      AND PermisoCodigo = 'OPERACION_CERRAR_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'OPERACION_CERRAR_CUENTA');

/* ============================================================
   2. Estado / visibilidad: propietario O permiso de cuenta
   ============================================================ */
DECLARE @sqlEstado nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_CambiarEstadoInmueble
    @idInmueble INT,
    @correo VARCHAR(200),
    @EstadoNuevo VARCHAR(20) = NULL,
    @VisibilidadNueva VARCHAR(20) = NULL,
    @Motivo NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;
    DECLARE @EstadoAnterior VARCHAR(20);
    DECLARE @VisibilidadAnterior VARCHAR(20);
    DECLARE @EstadoObjetivo VARCHAR(20);
    DECLARE @VisibilidadObjetivo VARCHAR(20);
    DECLARE @PuedeCambiarCuenta BIT = 0;
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51320, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @idCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @idAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @idCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @idAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @idCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @idAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 51321, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51322, ''El usuario autenticado pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @idAsesorInmueble = i.idAsesor,
        @idCuentaInmueble = i.IdCuenta,
        @EstadoAnterior = i.EstadoCodigo,
        @VisibilidadAnterior = i.VisibilidadCodigo
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51323, ''El inmueble no existe.'', 1;

    IF @idCuentaInmueble <> @idCuenta
        THROW 51324, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

    SELECT @PuedeCambiarCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INMUEBLE_CAMBIAR_ESTADO_CUENTA''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @idAsesorInmueble <> @idAsesor AND @PuedeCambiarCuenta = 0
        THROW 51325, ''El usuario autenticado no tiene permiso para modificar este inmueble.'', 1;

    SET @EstadoObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@EstadoNuevo)), ''''), @EstadoAnterior);
    SET @VisibilidadObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@VisibilidadNueva)), ''''), @VisibilidadAnterior);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = @EstadoObjetivo AND Activo = 1)
        THROW 51326, ''El estado solicitado no existe o no está activo.'', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble WHERE Codigo = @VisibilidadObjetivo AND Activo = 1)
        THROW 51327, ''La visibilidad solicitada no existe o no está activa.'', 1;

    IF @EstadoObjetivo IN (''VENDIDO'', ''RENTADO'')
        THROW 51328, ''VENDIDO/RENTADO requieren registrar una operación de cierre con precio y fecha.'', 1;

    IF @EstadoAnterior IN (''VENDIDO'', ''RENTADO'')
        THROW 51329, ''Un inmueble cerrado no puede reabrirse desde este procedimiento.'', 1;

    IF @EstadoObjetivo <> @EstadoAnterior
    BEGIN
        IF NOT
        (
               (@EstadoAnterior = ''BORRADOR''  AND @EstadoObjetivo IN (''PUBLICADO'', ''RETIRADO''))
            OR (@EstadoAnterior = ''PUBLICADO'' AND @EstadoObjetivo IN (''PAUSADO'', ''RETIRADO''))
            OR (@EstadoAnterior = ''PAUSADO''   AND @EstadoObjetivo IN (''PUBLICADO'', ''RETIRADO''))
            OR (@EstadoAnterior = ''RETIRADO''  AND @EstadoObjetivo = ''PUBLICADO'')
        )
            THROW 51330, ''La transición de estado solicitada no está permitida.'', 1;
    END;

    IF @EstadoObjetivo = @EstadoAnterior AND @VisibilidadObjetivo = @VisibilidadAnterior
        THROW 51331, ''No hay cambios de estado ni visibilidad que guardar.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.RSMAPS_Inmueble
        SET EstadoCodigo = @EstadoObjetivo,
            VisibilidadCodigo = @VisibilidadObjetivo,
            FechaPublicacionUtc = CASE
                WHEN @EstadoObjetivo = ''PUBLICADO'' AND @EstadoAnterior IN (''BORRADOR'', ''RETIRADO'')
                    THEN @AhoraUtc
                ELSE FechaPublicacionUtc
            END,
            FechaUltimoCambioEstadoUtc = @AhoraUtc
        WHERE idInmueble = @idInmueble;

        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble, IdCuenta,
            EstadoAnterior, EstadoNuevo,
            VisibilidadAnterior, VisibilidadNueva,
            IdAsesorResponsable, IdAsesorCambio,
            FechaCambioUtc, Motivo, Origen
        )
        VALUES
        (
            @idInmueble, @idCuenta,
            @EstadoAnterior, @EstadoObjetivo,
            @VisibilidadAnterior, @VisibilidadObjetivo,
            @idAsesorInmueble, @idAsesor,
            @AhoraUtc,
            NULLIF(LTRIM(RTRIM(@Motivo)), N''''),
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT i.idInmueble, i.IdCuenta, i.idAsesor,
           i.EstadoCodigo, i.VisibilidadCodigo,
           i.FechaPublicacionUtc, i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;
END;';

EXEC sys.sp_executesql @sqlEstado;

/* ============================================================
   3. Cierre: propietario O permiso de cierre de cuenta
   ============================================================ */
DECLARE @sqlCierre nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_CerrarOperacionInmueble
    @idInmueble INT,
    @correo VARCHAR(200),
    @TipoOperacion VARCHAR(10),
    @PrecioCierre DECIMAL(18,2),
    @FechaCierreUtc DATETIME2(0) = NULL,
    @NotasCierre NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @idAsesor INT;
    DECLARE @idCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;
    DECLARE @EstadoAnterior VARCHAR(20);
    DECLARE @VisibilidadAnterior VARCHAR(20);
    DECLARE @EstadoCierre VARCHAR(20);
    DECLARE @FechaPublicacionUtc DATETIME2(0);
    DECLARE @FechaCierre DATETIME2(0);
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @PrecioPublicado DECIMAL(18,2);
    DECLARE @IdTipo INT;
    DECLARE @Lat DECIMAL(10,6);
    DECLARE @Lng DECIMAL(10,6);
    DECLARE @Terreno FLOAT;
    DECLARE @Construccion FLOAT;
    DECLARE @Direccion NVARCHAR(500);
    DECLARE @IdOperacion BIGINT;
    DECLARE @PuedeCerrarCuenta BIT = 0;

    SET @TipoOperacion = UPPER(LTRIM(RTRIM(@TipoOperacion)));
    SET @FechaCierre = COALESCE(@FechaCierreUtc, @AhoraUtc);

    IF @TipoOperacion NOT IN (''VENTA'', ''RENTA'')
        THROW 51420, ''TipoOperacion debe ser VENTA o RENTA.'', 1;
    IF @PrecioCierre IS NULL OR @PrecioCierre <= 0
        THROW 51421, ''El precio de cierre debe ser mayor que cero.'', 1;
    IF @FechaCierre > DATEADD(MINUTE, 10, @AhoraUtc)
        THROW 51422, ''La fecha de cierre no puede estar en el futuro.'', 1;

    SELECT @idAsesor = u.idAsesor FROM dbo.RSMAPS_Usuario u WHERE u.correo = @correo;
    IF @idAsesor IS NULL
        THROW 51423, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @idCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @idAsesor
      AND cu.Activo = 1 AND c.Activo = 1 AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @idCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @idAsesor AND cu.Activo = 1 AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @idCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @idAsesor AND cu.Activo = 1 AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 51424, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51425, ''El usuario pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @idAsesorInmueble = i.idAsesor,
        @idCuentaInmueble = i.IdCuenta,
        @EstadoAnterior = i.EstadoCodigo,
        @VisibilidadAnterior = i.VisibilidadCodigo,
        @FechaPublicacionUtc = i.FechaPublicacionUtc,
        @PrecioPublicado = TRY_CONVERT(DECIMAL(18,2), i.precio),
        @IdTipo = i.idTipo,
        @Lat = i.lat, @Lng = i.lng,
        @Terreno = i.terreno, @Construccion = i.construccion,
        @Direccion = LEFT(CONVERT(NVARCHAR(MAX), i.direccion), 500)
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51426, ''El inmueble no existe.'', 1;
    IF @idCuentaInmueble <> @idCuenta
        THROW 51427, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

    SELECT @PuedeCerrarCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''OPERACION_CERRAR_CUENTA''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @idAsesorInmueble <> @idAsesor AND @PuedeCerrarCuenta = 0
        THROW 51428, ''El usuario autenticado no tiene permiso para cerrar este inmueble.'', 1;

    IF @EstadoAnterior IN (''VENDIDO'', ''RENTADO'')
        THROW 51429, ''El inmueble ya tiene un estado de cierre.'', 1;
    IF @EstadoAnterior NOT IN (''PUBLICADO'', ''PAUSADO'', ''RETIRADO'')
        THROW 51430, ''El estado actual del inmueble no permite registrar un cierre.'', 1;
    IF @FechaPublicacionUtc IS NOT NULL AND @FechaCierre < @FechaPublicacionUtc
        THROW 51431, ''La fecha de cierre no puede ser anterior a la fecha de publicacion conocida.'', 1;

    SET @EstadoCierre = CASE @TipoOperacion WHEN ''VENTA'' THEN ''VENDIDO'' WHEN ''RENTA'' THEN ''RENTADO'' END;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.RSMAPS_OperacionInmueble
        (
            IdInmueble, IdCuenta, IdAsesor, IdAsesorActor,
            TipoOperacion, PrecioPublicado, PrecioCierre, Moneda,
            FechaPublicacionUtc, FechaCierreUtc, DiasEnMercado,
            EstadoAnterior, VisibilidadAlCierre, IdTipo,
            Lat, Lng, Terreno, Construccion, Direccion,
            NotasCierre, Origen, EsDatoConfiable
        )
        VALUES
        (
            @idInmueble, @idCuenta, @idAsesorInmueble, @idAsesor,
            @TipoOperacion, @PrecioPublicado, @PrecioCierre, ''MXN'',
            @FechaPublicacionUtc, @FechaCierre,
            CASE WHEN @FechaPublicacionUtc IS NULL THEN NULL ELSE DATEDIFF(DAY, @FechaPublicacionUtc, @FechaCierre) END,
            @EstadoAnterior, @VisibilidadAnterior, @IdTipo,
            @Lat, @Lng, @Terreno, @Construccion, @Direccion,
            NULLIF(LTRIM(RTRIM(@NotasCierre)), N''''),
            ''APLICACION'', 1
        );

        SET @IdOperacion = CONVERT(BIGINT, SCOPE_IDENTITY());

        UPDATE dbo.RSMAPS_Inmueble
        SET EstadoCodigo = @EstadoCierre,
            FechaUltimoCambioEstadoUtc = @AhoraUtc
        WHERE idInmueble = @idInmueble;

        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble, IdCuenta,
            EstadoAnterior, EstadoNuevo,
            VisibilidadAnterior, VisibilidadNueva,
            IdAsesorResponsable, IdAsesorCambio,
            FechaCambioUtc, Motivo, Origen
        )
        VALUES
        (
            @idInmueble, @idCuenta,
            @EstadoAnterior, @EstadoCierre,
            @VisibilidadAnterior, @VisibilidadAnterior,
            @idAsesorInmueble, @idAsesor,
            @AhoraUtc,
            CONCAT(N''Cierre '', @TipoOperacion, N''. Precio de cierre: '', CONVERT(NVARCHAR(50), @PrecioCierre),
                   CASE WHEN NULLIF(LTRIM(RTRIM(@NotasCierre)), N'''') IS NULL THEN N'''' ELSE CONCAT(N''. '', @NotasCierre) END),
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT o.IdOperacion, o.IdInmueble, o.IdCuenta,
           o.IdAsesor, o.IdAsesorActor, o.TipoOperacion,
           o.PrecioPublicado, o.PrecioCierre, o.Moneda,
           o.FechaPublicacionUtc, o.FechaCierreUtc,
           o.DiasEnMercado, i.EstadoCodigo AS EstadoFinal
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = o.IdInmueble
    WHERE o.IdOperacion = @IdOperacion;
END;';

EXEC sys.sp_executesql @sqlCierre;

/* ============================================================
   4. Validar matriz persistente de permisos
   ============================================================ */
SELECT rp.RolCodigo, rp.PermisoCodigo
FROM dbo.RSMAPS_RolPermiso rp
WHERE rp.PermisoCodigo IN ('INMUEBLE_CAMBIAR_ESTADO_CUENTA', 'OPERACION_CERRAR_CUENTA')
ORDER BY rp.RolCodigo, rp.PermisoCodigo;

/* ============================================================
   5. Seleccionar actor y propiedad AJENA de la misma cuenta
   ============================================================ */
DECLARE @IdCuentaPrueba INT;
DECLARE @IdActor INT;
DECLARE @CorreoActor VARCHAR(200);
DECLARE @RolOriginal VARCHAR(30);
DECLARE @IdInmueblePrueba INT;
DECLARE @IdResponsable INT;
DECLARE @HistorialAntes INT;
DECLARE @OperacionesAntes INT;

SELECT TOP (1)
    @IdCuentaPrueba = cu.IdCuenta,
    @IdActor = cu.IdAsesor,
    @CorreoActor = u.correo,
    @RolOriginal = cu.RolCodigo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = cu.IdAsesor
WHERE cu.Activo = 1
  AND u.correo IS NOT NULL
  AND EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_Inmueble i
      WHERE i.IdCuenta = cu.IdCuenta
        AND i.idAsesor <> cu.IdAsesor
        AND i.EstadoCodigo = 'PUBLICADO'
  )
ORDER BY CASE WHEN cu.RolCodigo = 'ASESOR' THEN 0 ELSE 1 END, cu.IdAsesor;

IF @IdActor IS NULL
    THROW 52420, 'No se encontro usuario apto para prueba cruzada.', 1;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdResponsable = i.idAsesor
FROM dbo.RSMAPS_Inmueble i
WHERE i.IdCuenta = @IdCuentaPrueba
  AND i.idAsesor <> @IdActor
  AND i.EstadoCodigo = 'PUBLICADO'
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 52421, 'No se encontro inmueble ajeno publicado para la prueba.', 1;

SELECT @HistorialAntes = COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba;
SELECT @OperacionesAntes = COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba;

/* ============================================================
   6. Prueba cruzada de CAMBIO DE ESTADO con ROLLBACK
   ============================================================ */
BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_CuentaUsuario
SET RolCodigo = 'ADMINISTRADOR'
WHERE IdCuenta = @IdCuentaPrueba
  AND IdAsesor = @IdActor;

EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoActor,
    @EstadoNuevo = 'PAUSADO',
    @VisibilidadNueva = NULL,
    @Motivo = N'PRUEBA PASO 24 - ADMINISTRADOR SOBRE INMUEBLE AJENO';

SELECT TOP (1)
    h.IdInmueble,
    h.IdAsesorResponsable,
    h.IdAsesorCambio AS IdAsesorActor,
    @IdResponsable AS ResponsableEsperado,
    @IdActor AS ActorEsperado,
    h.EstadoAnterior,
    h.EstadoNuevo,
    CASE WHEN h.IdAsesorResponsable = @IdResponsable
              AND h.IdAsesorCambio = @IdActor
              AND @IdResponsable <> @IdActor
         THEN 'OK - ADMINISTRADOR CAMBIA ESTADO AJENO CON AUDITORIA CORRECTA'
         ELSE 'REVISAR'
    END AS EstadoPrueba
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdInmueblePrueba
  AND h.Motivo = N'PRUEBA PASO 24 - ADMINISTRADOR SOBRE INMUEBLE AJENO'
ORDER BY h.IdCambio DESC;

ROLLBACK TRANSACTION;

/* ============================================================
   7. Prueba cruzada de CIERRE con ROLLBACK
   ============================================================ */
BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_CuentaUsuario
SET RolCodigo = 'ADMINISTRADOR'
WHERE IdCuenta = @IdCuentaPrueba
  AND IdAsesor = @IdActor;

EXEC dbo.RSMAPS_sp_CerrarOperacionInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoActor,
    @TipoOperacion = 'VENTA',
    @PrecioCierre = 1234567.00,
    @FechaCierreUtc = NULL,
    @NotasCierre = N'PRUEBA PASO 24 - ADMINISTRADOR CIERRA INMUEBLE AJENO';

SELECT TOP (1)
    o.IdOperacion,
    o.IdInmueble,
    o.IdAsesor AS IdAsesorResponsable,
    o.IdAsesorActor,
    @IdResponsable AS ResponsableEsperado,
    @IdActor AS ActorEsperado,
    o.TipoOperacion,
    CASE WHEN o.IdAsesor = @IdResponsable
              AND o.IdAsesorActor = @IdActor
              AND @IdResponsable <> @IdActor
         THEN 'OK - ADMINISTRADOR CIERRA AJENO CON AUDITORIA CORRECTA'
         ELSE 'REVISAR'
    END AS EstadoPrueba
FROM dbo.RSMAPS_OperacionInmueble o
WHERE o.IdInmueble = @IdInmueblePrueba
  AND o.NotasCierre = N'PRUEBA PASO 24 - ADMINISTRADOR CIERRA INMUEBLE AJENO'
ORDER BY o.IdOperacion DESC;

SELECT TOP (1)
    h.IdInmueble,
    h.IdAsesorResponsable,
    h.IdAsesorCambio AS IdAsesorActor,
    h.EstadoAnterior,
    h.EstadoNuevo,
    CASE WHEN h.IdAsesorResponsable = @IdResponsable
              AND h.IdAsesorCambio = @IdActor
         THEN 'OK - HISTORIAL DE CIERRE CONSERVA RESPONSABLE Y ACTOR'
         ELSE 'REVISAR'
    END AS EstadoAuditoriaHistorial
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdInmueblePrueba
  AND h.Motivo LIKE N'Cierre VENTA.%PRUEBA PASO 24%'
ORDER BY h.IdCambio DESC;

ROLLBACK TRANSACTION;

/* ============================================================
   8. Verificar que todo dato de prueba y rol temporal regresaron
   ============================================================ */
SELECT
    @IdCuentaPrueba AS IdCuenta,
    @IdActor AS IdActor,
    @RolOriginal AS RolOriginal,
    cu.RolCodigo AS RolDespues,
    @IdInmueblePrueba AS IdInmueblePrueba,
    @IdResponsable AS IdResponsable,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) AS HistorialDespues,
    @HistorialAntes AS HistorialAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) AS OperacionesDespues,
    @OperacionesAntes AS OperacionesAntes,
    CASE
        WHEN cu.RolCodigo = @RolOriginal
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) = @HistorialAntes
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) = @OperacionesAntes
        THEN 'OK - ROL Y DATOS ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_CuentaUsuario cu
WHERE cu.IdCuenta = @IdCuentaPrueba
  AND cu.IdAsesor = @IdActor;

PRINT 'Paso 24 RSMaps 2.0 terminado correctamente.';
