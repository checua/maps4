/* ============================================================
   RSMaps 2.0 - Paso 23
   AUDITORIA: ASESOR RESPONSABLE VS USUARIO ACTOR

   Objetivo:
   - Preparar acciones de equipo sin perder atribucion comercial.
   - Conservar por separado al asesor responsable del inmueble y al
     usuario/asesor autenticado que ejecuta una accion.
   - Mantener por ahora la autorizacion conservadora: solo el asesor
     responsable puede cambiar estado/visibilidad o cerrar operacion.
   - Dejar listas las estructuras para abrir permisos de equipo en un
     paso posterior mediante RBAC.

   Semantica resultante:
   RSMAPS_InmuebleCambioEstado
     IdAsesorResponsable -> responsable comercial en ese momento.
     IdAsesorCambio      -> actor que ejecuto el cambio.

   RSMAPS_OperacionInmueble
     IdAsesor             -> responsable comercial del inmueble.
     IdAsesorActor        -> actor que registro el cierre.

   El script es idempotente. Las pruebas usan ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 52301, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado', N'U') IS NULL
    THROW 52302, 'No existe dbo.RSMAPS_InmuebleCambioEstado. Ejecutar primero Paso 11.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_OperacionInmueble', N'U') IS NULL
    THROW 52303, 'No existe dbo.RSMAPS_OperacionInmueble. Ejecutar primero Paso 14.', 1;

/* ============================================================
   1. Extender auditoria historica
   ============================================================ */
IF COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') IS NULL
BEGIN
    ALTER TABLE dbo.RSMAPS_InmuebleCambioEstado
    ADD IdAsesorResponsable int NULL;
END;

/* Los historiales creados hasta ahora pertenecen a inmuebles que siguen
   conservados en RSMAPS_Inmueble. Completar cuando sea posible sin
   inventar datos si no hubiera fila activa. */
UPDATE h
SET IdAsesorResponsable = i.idAsesor
FROM dbo.RSMAPS_InmuebleCambioEstado h
INNER JOIN dbo.RSMAPS_Inmueble i
    ON i.idInmueble = h.IdInmueble
WHERE h.IdAsesorResponsable IS NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado')
      AND name = N'IX_RSMAPS_InmuebleCambioEstado_Responsable_Fecha'
)
BEGIN
    CREATE INDEX IX_RSMAPS_InmuebleCambioEstado_Responsable_Fecha
        ON dbo.RSMAPS_InmuebleCambioEstado(IdAsesorResponsable, FechaCambioUtc DESC);
END;

IF COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') IS NULL
BEGIN
    ALTER TABLE dbo.RSMAPS_OperacionInmueble
    ADD IdAsesorActor int NULL;
END;

/* Hasta este paso todos los cierres permitian exclusivamente al asesor
   responsable, por lo que el actor historico conocido coincide con IdAsesor. */
UPDATE dbo.RSMAPS_OperacionInmueble
SET IdAsesorActor = IdAsesor
WHERE IdAsesorActor IS NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_OperacionInmueble')
      AND name = N'IX_RSMAPS_OperacionInmueble_Actor_Fecha'
)
BEGIN
    CREATE INDEX IX_RSMAPS_OperacionInmueble_Actor_Fecha
        ON dbo.RSMAPS_OperacionInmueble(IdAsesorActor, FechaCierreUtc DESC);
END;

/* ============================================================
   2. Cambios de estado: guardar responsable + actor
   Mantiene los mismos numeros de error usados por la aplicacion.
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
    DECLARE @MembresiasActivas INT;
    DECLARE @idAsesorInmueble INT;
    DECLARE @idCuentaInmueble INT;
    DECLARE @EstadoAnterior VARCHAR(20);
    DECLARE @VisibilidadAnterior VARCHAR(20);
    DECLARE @EstadoObjetivo VARCHAR(20);
    DECLARE @VisibilidadObjetivo VARCHAR(20);
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51320, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @idCuenta = cu.IdCuenta
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @idAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @idCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c
            ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @idAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1) @idCuenta = cu.IdCuenta
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c
                ON c.IdCuenta = cu.IdCuenta
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

    /* Paso 23 todavia conserva minimo privilegio. */
    IF @idAsesorInmueble <> @idAsesor
        THROW 51325, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    SET @EstadoObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@EstadoNuevo)), ''''), @EstadoAnterior);
    SET @VisibilidadObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@VisibilidadNueva)), ''''), @VisibilidadAnterior);

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.RSMAPS_EstadoInmueble
        WHERE Codigo = @EstadoObjetivo AND Activo = 1
    )
        THROW 51326, ''El estado solicitado no existe o no está activo.'', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble
        WHERE Codigo = @VisibilidadObjetivo AND Activo = 1
    )
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

    IF @EstadoObjetivo = @EstadoAnterior
       AND @VisibilidadObjetivo = @VisibilidadAnterior
        THROW 51331, ''No hay cambios de estado ni visibilidad que guardar.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.RSMAPS_Inmueble
        SET
            EstadoCodigo = @EstadoObjetivo,
            VisibilidadCodigo = @VisibilidadObjetivo,
            FechaPublicacionUtc = CASE
                WHEN @EstadoObjetivo = ''PUBLICADO''
                     AND @EstadoAnterior IN (''BORRADOR'', ''RETIRADO'')
                    THEN @AhoraUtc
                ELSE FechaPublicacionUtc
            END,
            FechaUltimoCambioEstadoUtc = @AhoraUtc
        WHERE idInmueble = @idInmueble;

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
            @idCuenta,
            @EstadoAnterior,
            @EstadoObjetivo,
            @VisibilidadAnterior,
            @VisibilidadObjetivo,
            @idAsesorInmueble,
            @idAsesor,
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

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaPublicacionUtc,
        i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;
END;';

EXEC sys.sp_executesql @sqlEstado;

/* ============================================================
   3. Cierre: IdAsesor = responsable, IdAsesorActor = actor
   Mantiene los mismos numeros de error usados por la aplicacion.
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

    SET @TipoOperacion = UPPER(LTRIM(RTRIM(@TipoOperacion)));
    SET @FechaCierre = COALESCE(@FechaCierreUtc, @AhoraUtc);

    IF @TipoOperacion NOT IN (''VENTA'', ''RENTA'')
        THROW 51420, ''TipoOperacion debe ser VENTA o RENTA.'', 1;

    IF @PrecioCierre IS NULL OR @PrecioCierre <= 0
        THROW 51421, ''El precio de cierre debe ser mayor que cero.'', 1;

    IF @FechaCierre > DATEADD(MINUTE, 10, @AhoraUtc)
        THROW 51422, ''La fecha de cierre no puede estar en el futuro.'', 1;

    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51423, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1) @idCuenta = cu.IdCuenta
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
            SELECT TOP (1) @idCuenta = cu.IdCuenta
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @idAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
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
        @Lat = i.lat,
        @Lng = i.lng,
        @Terreno = i.terreno,
        @Construccion = i.construccion,
        @Direccion = LEFT(CONVERT(NVARCHAR(MAX), i.direccion), 500)
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @idAsesorInmueble IS NULL
        THROW 51426, ''El inmueble no existe.'', 1;

    IF @idCuentaInmueble <> @idCuenta
        THROW 51427, ''El inmueble pertenece a una Cuenta diferente a la del usuario autenticado.'', 1;

    /* Paso 23 todavia conserva minimo privilegio. */
    IF @idAsesorInmueble <> @idAsesor
        THROW 51428, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    IF @EstadoAnterior IN (''VENDIDO'', ''RENTADO'')
        THROW 51429, ''El inmueble ya tiene un estado de cierre.'', 1;

    IF @EstadoAnterior NOT IN (''PUBLICADO'', ''PAUSADO'', ''RETIRADO'')
        THROW 51430, ''El estado actual del inmueble no permite registrar un cierre.'', 1;

    IF @FechaPublicacionUtc IS NOT NULL AND @FechaCierre < @FechaPublicacionUtc
        THROW 51431, ''La fecha de cierre no puede ser anterior a la fecha de publicacion conocida.'', 1;

    SET @EstadoCierre = CASE @TipoOperacion
        WHEN ''VENTA'' THEN ''VENDIDO''
        WHEN ''RENTA'' THEN ''RENTADO''
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.RSMAPS_OperacionInmueble
        (
            IdInmueble,
            IdCuenta,
            IdAsesor,
            IdAsesorActor,
            TipoOperacion,
            PrecioPublicado,
            PrecioCierre,
            Moneda,
            FechaPublicacionUtc,
            FechaCierreUtc,
            DiasEnMercado,
            EstadoAnterior,
            VisibilidadAlCierre,
            IdTipo,
            Lat,
            Lng,
            Terreno,
            Construccion,
            Direccion,
            NotasCierre,
            Origen,
            EsDatoConfiable
        )
        VALUES
        (
            @idInmueble,
            @idCuenta,
            @idAsesorInmueble,
            @idAsesor,
            @TipoOperacion,
            @PrecioPublicado,
            @PrecioCierre,
            ''MXN'',
            @FechaPublicacionUtc,
            @FechaCierre,
            CASE WHEN @FechaPublicacionUtc IS NULL THEN NULL
                 ELSE DATEDIFF(DAY, @FechaPublicacionUtc, @FechaCierre) END,
            @EstadoAnterior,
            @VisibilidadAnterior,
            @IdTipo,
            @Lat,
            @Lng,
            @Terreno,
            @Construccion,
            @Direccion,
            NULLIF(LTRIM(RTRIM(@NotasCierre)), N''''),
            ''APLICACION'',
            1
        );

        SET @IdOperacion = CONVERT(BIGINT, SCOPE_IDENTITY());

        UPDATE dbo.RSMAPS_Inmueble
        SET EstadoCodigo = @EstadoCierre,
            FechaUltimoCambioEstadoUtc = @AhoraUtc
        WHERE idInmueble = @idInmueble;

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
            @idCuenta,
            @EstadoAnterior,
            @EstadoCierre,
            @VisibilidadAnterior,
            @VisibilidadAnterior,
            @idAsesorInmueble,
            @idAsesor,
            @AhoraUtc,
            CONCAT(N''Cierre '', @TipoOperacion, N''. Precio de cierre: '', CONVERT(NVARCHAR(50), @PrecioCierre),
                   CASE WHEN NULLIF(LTRIM(RTRIM(@NotasCierre)), N'''') IS NULL THEN N''''
                        ELSE CONCAT(N''. '', @NotasCierre) END),
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT
        o.IdOperacion,
        o.IdInmueble,
        o.IdCuenta,
        o.IdAsesor,
        o.IdAsesorActor,
        o.TipoOperacion,
        o.PrecioPublicado,
        o.PrecioCierre,
        o.Moneda,
        o.FechaPublicacionUtc,
        o.FechaCierreUtc,
        o.DiasEnMercado,
        i.EstadoCodigo AS EstadoFinal
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = o.IdInmueble
    WHERE o.IdOperacion = @IdOperacion;
END;';

EXEC sys.sp_executesql @sqlCierre;

/* ============================================================
   4. Validaciones estructurales
   ============================================================ */
SELECT
    COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') AS ColResponsableHistorial,
    COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') AS ColActorOperacion,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdAsesorActor IS NULL) AS OperacionesSinActor,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmuebleCambioEstado h
     INNER JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = h.IdInmueble
     WHERE h.IdAsesorResponsable IS NULL) AS HistorialActivoSinResponsable;

/* ============================================================
   5. Prueba controlada: estado
   ============================================================ */
DECLARE @IdInmueblePrueba int;
DECLARE @IdAsesorResponsable int;
DECLARE @CorreoResponsable varchar(200);
DECLARE @HistorialAntes int;
DECLARE @OperacionesAntes int;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdAsesorResponsable = i.idAsesor,
    @CorreoResponsable = u.correo
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdCuenta = i.IdCuenta
   AND cu.IdAsesor = i.idAsesor
   AND cu.Activo = 1
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND u.correo IS NOT NULL
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 52320, 'No se encontro inmueble publicado para la prueba de auditoria.', 1;

SELECT @HistorialAntes = COUNT(*)
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE IdInmueble = @IdInmueblePrueba;

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoResponsable,
    @EstadoNuevo = 'PAUSADO',
    @VisibilidadNueva = NULL,
    @Motivo = N'PRUEBA PASO 23 - AUDITORIA RESPONSABLE/ACTOR';

SELECT TOP (1)
    h.IdInmueble,
    h.IdAsesorResponsable,
    h.IdAsesorCambio AS IdAsesorActor,
    h.EstadoAnterior,
    h.EstadoNuevo,
    CASE WHEN h.IdAsesorResponsable = @IdAsesorResponsable
              AND h.IdAsesorCambio = @IdAsesorResponsable
         THEN 'OK - HISTORIAL DISTINGUE RESPONSABLE Y ACTOR'
         ELSE 'REVISAR'
    END AS EstadoAuditoria
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdInmueblePrueba
  AND h.Motivo = N'PRUEBA PASO 23 - AUDITORIA RESPONSABLE/ACTOR'
ORDER BY h.IdCambio DESC;

ROLLBACK TRANSACTION;

/* ============================================================
   6. Prueba controlada: cierre
   ============================================================ */
SELECT @OperacionesAntes = COUNT(*)
FROM dbo.RSMAPS_OperacionInmueble
WHERE IdInmueble = @IdInmueblePrueba;

BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CerrarOperacionInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoResponsable,
    @TipoOperacion = 'VENTA',
    @PrecioCierre = 1234567.00,
    @FechaCierreUtc = NULL,
    @NotasCierre = N'PRUEBA PASO 23 - AUDITORIA RESPONSABLE/ACTOR';

SELECT TOP (1)
    o.IdOperacion,
    o.IdInmueble,
    o.IdAsesor AS IdAsesorResponsable,
    o.IdAsesorActor,
    o.TipoOperacion,
    CASE WHEN o.IdAsesor = @IdAsesorResponsable
              AND o.IdAsesorActor = @IdAsesorResponsable
         THEN 'OK - OPERACION DISTINGUE RESPONSABLE Y ACTOR'
         ELSE 'REVISAR'
    END AS EstadoAuditoria
FROM dbo.RSMAPS_OperacionInmueble o
WHERE o.IdInmueble = @IdInmueblePrueba
  AND o.NotasCierre = N'PRUEBA PASO 23 - AUDITORIA RESPONSABLE/ACTOR'
ORDER BY o.IdOperacion DESC;

SELECT TOP (1)
    h.IdInmueble,
    h.IdAsesorResponsable,
    h.IdAsesorCambio AS IdAsesorActor,
    h.EstadoAnterior,
    h.EstadoNuevo,
    CASE WHEN h.IdAsesorResponsable = @IdAsesorResponsable
              AND h.IdAsesorCambio = @IdAsesorResponsable
         THEN 'OK - CIERRE HISTORIAL CONSERVA RESPONSABLE Y ACTOR'
         ELSE 'REVISAR'
    END AS EstadoAuditoriaHistorial
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdInmueblePrueba
  AND h.Motivo LIKE N'Cierre VENTA.%PRUEBA PASO 23%'
ORDER BY h.IdCambio DESC;

ROLLBACK TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) AS HistorialDespues,
    @HistorialAntes AS HistorialAntes,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) AS OperacionesDespues,
    @OperacionesAntes AS OperacionesAntes,
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueblePrueba) = @HistorialAntes
         AND (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueblePrueba) = @OperacionesAntes
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

PRINT 'Paso 23 RSMaps 2.0 terminado correctamente.';