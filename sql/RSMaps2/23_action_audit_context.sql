/* ============================================================
   RSMaps 2.0 - Paso 23
   AUDITORIA: ASESOR RESPONSABLE VS USUARIO ACTOR

   Objetivo:
   - Preparar acciones de equipo sin perder atribucion comercial.
   - Separar responsable comercial y usuario actor.
   - No abrir todavia permisos de modificacion ajena.
   - Mantener compatibilidad con los procedimientos actuales.

   Semantica:
   RSMAPS_InmuebleCambioEstado
     IdAsesorResponsable -> responsable comercial del inmueble.
     IdAsesorCambio      -> actor que ejecuto el cambio.

   RSMAPS_OperacionInmueble
     IdAsesor      -> responsable comercial del inmueble.
     IdAsesorActor -> actor que registro el cierre.

   IMPORTANTE:
   - El primer lote SOLO agrega columnas.
   - GO fuerza una nueva compilacion despues del ALTER TABLE.
   - Los triggers solo completan valores NULL; un procedimiento futuro
     podra enviar un actor distinto sin que sea sobrescrito.
   - Las pruebas usan ROLLBACK.
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
   1. Solo DDL en este lote.
   SQL Server compila el lote antes de ejecutarlo; por eso las nuevas
   columnas no se referencian hasta despues de GO.
   ============================================================ */
IF COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.RSMAPS_InmuebleCambioEstado
        ADD IdAsesorResponsable int NULL;';
END;

IF COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.RSMAPS_OperacionInmueble
        ADD IdAsesorActor int NULL;';
END;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

/* ============================================================
   2. Backfill de informacion historica conocida.
   ============================================================ */
UPDATE h
SET IdAsesorResponsable = i.idAsesor
FROM dbo.RSMAPS_InmuebleCambioEstado h
INNER JOIN dbo.RSMAPS_Inmueble i
    ON i.idInmueble = h.IdInmueble
WHERE h.IdAsesorResponsable IS NULL;

/* Hasta este paso todos los cierres reales permitian exclusivamente al
   asesor responsable, por lo que actor y responsable historicamente
   coinciden en las operaciones existentes. */
UPDATE dbo.RSMAPS_OperacionInmueble
SET IdAsesorActor = IdAsesor
WHERE IdAsesorActor IS NULL;

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
   3. Compatibilidad con procedimientos actuales.

   Los triggers SOLO rellenan NULL. Cuando un futuro procedimiento RBAC
   envie explicitamente IdAsesorResponsable / IdAsesorActor distintos,
   esos valores se conservaran.
   ============================================================ */
EXEC sys.sp_executesql N'
CREATE OR ALTER TRIGGER dbo.RSMAPS_tr_InmuebleCambioEstado_AuditDefaults
ON dbo.RSMAPS_InmuebleCambioEstado
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE h
    SET h.IdAsesorResponsable = inm.idAsesor
    FROM dbo.RSMAPS_InmuebleCambioEstado h
    INNER JOIN inserted ins
        ON ins.IdCambio = h.IdCambio
    INNER JOIN dbo.RSMAPS_Inmueble inm
        ON inm.idInmueble = h.IdInmueble
    WHERE h.IdAsesorResponsable IS NULL;
END;';

EXEC sys.sp_executesql N'
CREATE OR ALTER TRIGGER dbo.RSMAPS_tr_OperacionInmueble_AuditDefaults
ON dbo.RSMAPS_OperacionInmueble
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE o
    SET o.IdAsesorActor = o.IdAsesor
    FROM dbo.RSMAPS_OperacionInmueble o
    INNER JOIN inserted ins
        ON ins.IdOperacion = o.IdOperacion
    WHERE o.IdAsesorActor IS NULL;
END;';

/* ============================================================
   4. Validacion estructural.
   ============================================================ */
SELECT
    COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') AS ColResponsableHistorial,
    COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') AS ColActorOperacion,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_OperacionInmueble
     WHERE IdAsesorActor IS NULL) AS OperacionesSinActor,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmuebleCambioEstado h
     INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = h.IdInmueble
     WHERE h.IdAsesorResponsable IS NULL) AS HistorialActivoSinResponsable;

/* ============================================================
   5. Elegir inmueble para pruebas controladas.
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
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = i.idAsesor
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

SELECT @OperacionesAntes = COUNT(*)
FROM dbo.RSMAPS_OperacionInmueble
WHERE IdInmueble = @IdInmueblePrueba;

/* ============================================================
   6. Prueba de cambio de estado. ROLLBACK.
   El procedimiento actual inserta IdAsesorCambio (actor) y el trigger
   completa IdAsesorResponsable.
   ============================================================ */
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
    CASE
        WHEN h.IdAsesorResponsable = @IdAsesorResponsable
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
   7. Prueba de cierre. ROLLBACK.
   El procedimiento actual conserva IdAsesor como responsable; el trigger
   completa IdAsesorActor con el actor actual.
   ============================================================ */
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
    CASE
        WHEN o.IdAsesor = @IdAsesorResponsable
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
    CASE
        WHEN h.IdAsesorResponsable = @IdAsesorResponsable
         AND h.IdAsesorCambio = @IdAsesorResponsable
        THEN 'OK - CIERRE HISTORIAL CONSERVA RESPONSABLE Y ACTOR'
        ELSE 'REVISAR'
    END AS EstadoAuditoriaHistorial
FROM dbo.RSMAPS_InmuebleCambioEstado h
WHERE h.IdInmueble = @IdInmueblePrueba
  AND h.Motivo LIKE N'Cierre VENTA.%PRUEBA PASO 23%'
ORDER BY h.IdCambio DESC;

ROLLBACK TRANSACTION;

/* ============================================================
   8. Verificar que las pruebas no dejaron datos.
   ============================================================ */
SELECT
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_InmuebleCambioEstado
     WHERE IdInmueble = @IdInmueblePrueba) AS HistorialDespues,
    @HistorialAntes AS HistorialAntes,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_OperacionInmueble
     WHERE IdInmueble = @IdInmueblePrueba) AS OperacionesDespues,
    @OperacionesAntes AS OperacionesAntes,
    CASE
        WHEN (SELECT COUNT(*)
              FROM dbo.RSMAPS_InmuebleCambioEstado
              WHERE IdInmueble = @IdInmueblePrueba) = @HistorialAntes
         AND (SELECT COUNT(*)
              FROM dbo.RSMAPS_OperacionInmueble
              WHERE IdInmueble = @IdInmueblePrueba) = @OperacionesAntes
        THEN 'OK - ROLLBACK COMPLETO'
        ELSE 'REVISAR'
    END AS EstadoRollback;

PRINT 'Paso 23 RSMaps 2.0 terminado correctamente.';
