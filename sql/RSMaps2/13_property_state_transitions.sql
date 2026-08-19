/* ============================================================
   RSMaps 2.0 - Paso 13
   TRANSICIONES AUTORIZADAS DE ESTADO Y VISIBILIDAD

   Base esperada: mapsMarkers

   Objetivo:
   - Cambiar Estado/Visibilidad sin borrar fisicamente el inmueble.
   - Registrar cada cambio en RSMAPS_InmuebleCambioEstado.
   - Resolver identidad y Cuenta desde el usuario autenticado.
   - Mantener, por ahora, la politica conservadora: solo el asesor
     propietario del inmueble puede cambiarlo.
   - Reservar VENDIDO/RENTADO para un flujo posterior de cierre que
     exija precio y fecha de cierre, evitando historico incompleto.

   Transiciones permitidas de ESTADO:
   BORRADOR  -> PUBLICADO | RETIRADO
   PUBLICADO -> PAUSADO   | RETIRADO
   PAUSADO   -> PUBLICADO | RETIRADO
   RETIRADO  -> PUBLICADO

   VENDIDO y RENTADO son estados terminales reservados al futuro
   procedimiento de cierre de operacion.

   La VISIBILIDAD puede cambiar de forma independiente para estados
   no terminales: CUENTA | COLABORADORES | ENLACE | PUBLICO.

   IMPORTANTE:
   - NO elimina inmuebles.
   - NO modifica RSMAPS_InmuebleVendido.
   - NO permite marcar VENDIDO/RENTADO todavia.
   - Las pruebas usan ROLLBACK y no dejan cambios persistentes.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 51300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51301, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado', N'U') IS NULL
    THROW 51302, 'No existe dbo.RSMAPS_InmuebleCambioEstado. Ejecutar primero Paso 11.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_EstadoInmueble', N'U') IS NULL
    THROW 51303, 'No existe dbo.RSMAPS_EstadoInmueble. Ejecutar primero Paso 11.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_VisibilidadInmueble', N'U') IS NULL
    THROW 51304, 'No existe dbo.RSMAPS_VisibilidadInmueble. Ejecutar primero Paso 11.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'EstadoCodigo') IS NULL
    THROW 51305, 'Falta EstadoCodigo. Ejecutar primero Paso 11.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'VisibilidadCodigo') IS NULL
    THROW 51306, 'Falta VisibilidadCodigo. Ejecutar primero Paso 11.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @sql nvarchar(max) = N'
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

    /* 1. Resolver identidad. */
    SELECT @idAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @idAsesor IS NULL
        THROW 51320, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    /* 2. Resolver Cuenta activa/predeterminada. */
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
              AND c.Activo = 1;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 51321, ''El usuario autenticado no pertenece a ninguna Cuenta activa.'', 1;
        ELSE
            THROW 51322, ''El usuario autenticado pertenece a varias Cuentas y no tiene una predeterminada.'', 1;
    END;

    /* 3. Obtener inmueble. */
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

    IF @idAsesorInmueble <> @idAsesor
        THROW 51325, ''El asesor autenticado no es propietario de este inmueble.'', 1;

    SET @EstadoObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@EstadoNuevo)), ''''), @EstadoAnterior);
    SET @VisibilidadObjetivo = COALESCE(NULLIF(LTRIM(RTRIM(@VisibilidadNueva)), ''''), @VisibilidadAnterior);

    /* 4. Validar catálogos. */
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_EstadoInmueble
        WHERE Codigo = @EstadoObjetivo
          AND Activo = 1
    )
        THROW 51326, ''El estado solicitado no existe o no está activo.'', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_VisibilidadInmueble
        WHERE Codigo = @VisibilidadObjetivo
          AND Activo = 1
    )
        THROW 51327, ''La visibilidad solicitada no existe o no está activa.'', 1;

    /* 5. Los cierres requieren un flujo transaccional posterior. */
    IF @EstadoObjetivo IN (''VENDIDO'', ''RENTADO'')
        THROW 51328, ''VENDIDO/RENTADO requieren registrar una operación de cierre con precio y fecha.'', 1;

    IF @EstadoAnterior IN (''VENDIDO'', ''RENTADO'')
        THROW 51329, ''Un inmueble cerrado no puede reabrirse desde este procedimiento.'', 1;

    /* 6. Validar matriz de transiciones si cambia el estado. */
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

        /* FechaPublicacionUtc representa el inicio del ciclo de publicación
           conocido por RSMaps 2.0. Pausar conserva la fecha. Retirar y volver
           a publicar inicia un nuevo ciclo. */
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
            @idAsesor,
            @AhoraUtc,
            NULLIF(LTRIM(RTRIM(@Motivo)), N''''),
            ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
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
END;
';

    EXEC sys.sp_executesql @sql;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   PRUEBAS CONTROLADAS
   ============================================================ */
DECLARE @IdInmueblePrueba INT;
DECLARE @IdCuentaPrueba INT;
DECLARE @IdAsesorPropietario INT;
DECLARE @CorreoPropietario VARCHAR(200);
DECLARE @CorreoOtro VARCHAR(200);
DECLARE @EstadoOriginal VARCHAR(20);
DECLARE @VisibilidadOriginal VARCHAR(20);
DECLARE @HistorialAntes INT;
DECLARE @HistorialDurante INT;
DECLARE @MarketplaceAntes INT;
DECLARE @MarketplaceDurante INT;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdCuentaPrueba = i.IdCuenta,
    @IdAsesorPropietario = i.idAsesor,
    @EstadoOriginal = i.EstadoCodigo,
    @VisibilidadOriginal = i.VisibilidadCodigo
FROM dbo.RSMAPS_Inmueble i
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO'
  AND EXISTS
  (
      SELECT 1
      FROM dbo.RSMAPS_CuentaUsuario cu
      WHERE cu.IdCuenta = i.IdCuenta
        AND cu.IdAsesor <> i.idAsesor
        AND cu.Activo = 1
  )
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
    THROW 51340, 'No se encontró inmueble PUBLICADO/PUBLICO adecuado para la prueba.', 1;

SELECT @CorreoPropietario = correo
FROM dbo.RSMAPS_Usuario
WHERE idAsesor = @IdAsesorPropietario;

SELECT TOP (1) @CorreoOtro = u.correo
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = cu.IdAsesor
WHERE cu.IdCuenta = @IdCuentaPrueba
  AND cu.IdAsesor <> @IdAsesorPropietario
  AND cu.Activo = 1
  AND u.correo IS NOT NULL
ORDER BY u.idAsesor;

IF @CorreoPropietario IS NULL OR @CorreoOtro IS NULL
    THROW 51341, 'No fue posible resolver usuarios para la prueba.', 1;

SELECT @HistorialAntes = COUNT(*)
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE IdInmueble = @IdInmueblePrueba;

SELECT @MarketplaceAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

/* A. Propietario pausa: sale del marketplace y crea historial. */
BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPropietario,
    @EstadoNuevo = 'PAUSADO',
    @Motivo = N'PRUEBA PASO 13 - DEBE HACER ROLLBACK';

SELECT @HistorialDurante = COUNT(*)
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE IdInmueble = @IdInmueblePrueba;

SELECT @MarketplaceDurante = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'PUBLICADO'
  AND VisibilidadCodigo = 'PUBLICO';

SELECT
    @IdInmueblePrueba AS idInmueble,
    @EstadoOriginal AS EstadoAntes,
    (SELECT EstadoCodigo FROM dbo.RSMAPS_Inmueble WHERE idInmueble = @IdInmueblePrueba) AS EstadoDurante,
    @MarketplaceAntes AS MarketplaceAntes,
    @MarketplaceDurante AS MarketplaceDurante,
    @HistorialAntes AS HistorialAntes,
    @HistorialDurante AS HistorialDurante,
    CASE
        WHEN @MarketplaceDurante = @MarketplaceAntes - 1
         AND @HistorialDurante = @HistorialAntes + 1
         AND EXISTS
         (
             SELECT 1 FROM dbo.RSMAPS_Inmueble
             WHERE idInmueble = @IdInmueblePrueba
               AND EstadoCodigo = 'PAUSADO'
         )
        THEN 'OK - PAUSA CON HISTORIAL'
        ELSE 'REVISAR'
    END AS EstadoPruebaPausa;

ROLLBACK TRANSACTION;

/* B. Otro asesor de la misma Cuenta no puede cambiar estado. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoOtro,
        @EstadoNuevo = 'PAUSADO',
        @Motivo = N'NO DEBE GUARDARSE';

    SELECT 'ERROR - CAMBIO AJENO NO FUE BLOQUEADO' AS EstadoAutorizacion;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51325
        SELECT
            'OK - CAMBIO AJENO BLOQUEADO' AS EstadoAutorizacion,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

/* C. No permitir VENDIDO sin cierre formal. */
BEGIN TRY
    EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
        @idInmueble = @IdInmueblePrueba,
        @correo = @CorreoPropietario,
        @EstadoNuevo = 'VENDIDO',
        @Motivo = N'NO DEBE GUARDARSE';

    SELECT 'ERROR - VENTA INCOMPLETA NO FUE BLOQUEADA' AS EstadoCierre;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51328
        SELECT
            'OK - VENTA INCOMPLETA BLOQUEADA' AS EstadoCierre,
            ERROR_NUMBER() AS NumeroError,
            ERROR_MESSAGE() AS MensajeError;
    ELSE
        THROW;
END CATCH;

/* D. Cambio solo de visibilidad, con rollback. */
BEGIN TRANSACTION;

EXEC dbo.RSMAPS_sp_CambiarEstadoInmueble
    @idInmueble = @IdInmueblePrueba,
    @correo = @CorreoPropietario,
    @VisibilidadNueva = 'ENLACE',
    @Motivo = N'PRUEBA VISIBILIDAD PASO 13';

SELECT
    @IdInmueblePrueba AS idInmueble,
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_Inmueble
            WHERE idInmueble = @IdInmueblePrueba
              AND EstadoCodigo = @EstadoOriginal
              AND VisibilidadCodigo = 'ENLACE'
        )
        THEN 'OK - VISIBILIDAD INDEPENDIENTE'
        ELSE 'REVISAR'
    END AS EstadoVisibilidad;

ROLLBACK TRANSACTION;

/* E. Verificar que todo quedó como estaba. */
SELECT
    i.idInmueble,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmuebleCambioEstado h
     WHERE h.IdInmueble = i.idInmueble) AS HistorialActual,
    CASE
        WHEN i.EstadoCodigo = @EstadoOriginal
         AND i.VisibilidadCodigo = @VisibilidadOriginal
         AND (SELECT COUNT(*)
              FROM dbo.RSMAPS_InmuebleCambioEstado h
              WHERE h.IdInmueble = i.idInmueble) = @HistorialAntes
        THEN 'OK - DATOS E HISTORIAL ORIGINALES INTACTOS'
        ELSE 'REVISAR'
    END AS EstadoFinal
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueblePrueba;

SELECT
    p.name AS Procedimiento,
    prm.parameter_id,
    prm.name AS Parametro,
    TYPE_NAME(prm.user_type_id) AS TipoDato,
    prm.max_length AS LongitudBytes
FROM sys.procedures p
INNER JOIN sys.parameters prm
    ON prm.object_id = p.object_id
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_CambiarEstadoInmueble')
ORDER BY prm.parameter_id;

PRINT 'Paso 13 RSMaps 2.0 terminado correctamente.';
