/* ============================================================
   RSMaps 2.0 - Paso 45
   RADAR: ACK TERMINAL DURABLE DEL WORKFLOW

   Objetivo:
   - Separar "Intelligence + matching completados" de "workflow terminado".
   - Permitir recuperar mensajes cuyo resultado central ya fue persistido,
     pero cuyo downstream no alcanzo a terminar antes de reiniciar el Agent.
   - Evitar inferir el estado terminal por la existencia o ausencia de Delivery.

   Nota tecnica:
   - Las referencias a columnas agregadas en este mismo script se ejecutan
     mediante SQL dinamico. SQL Server compila cada batch antes de ejecutar
     los ALTER TABLE; sin esto puede producir Msg 207 "Invalid column name".
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing', N'U') IS NULL
BEGIN
    THROW 54501, 'Primero debe ejecutarse 44_radar_message_processing_persistence.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DownstreamAckUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarMessageProcessing
            ADD DownstreamAckUtc datetime2(3) NULL;
    END;

    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DisposicionTerminal') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarMessageProcessing
            ADD DisposicionTerminal nvarchar(60) NULL;
    END;

    /*
       Los registros previos a este paso ya usaban TerminadoUtc como si fuera
       finalizacion end-to-end. Se marcan como legado para no reactivar pruebas
       historicas al habilitar la nueva recuperacion downstream.
    */
    EXEC sys.sp_executesql N'
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET DownstreamAckUtc = COALESCE(DownstreamAckUtc, TerminadoUtc, ActualizadoUtc, SYSUTCDATETIME()),
    DisposicionTerminal = COALESCE(DisposicionTerminal, N''LEGACY_PRE_45'')
WHERE Estado = N''COMPLETADO''
  AND ResultadoCentralJson IS NOT NULL
  AND DownstreamAckUtc IS NULL
  AND TerminadoUtc IS NOT NULL;';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
          AND name = N'IX_RSMAPS_RadarMessageProcessing_DownstreamPendiente'
    )
    BEGIN
        EXEC sys.sp_executesql N'
CREATE INDEX IX_RSMAPS_RadarMessageProcessing_DownstreamPendiente
    ON dbo.RSMAPS_RadarMessageProcessing(IdAgent, Estado, DownstreamAckUtc, ActualizadoUtc)
    INCLUDE (ChatOrigen, MessageId, MatchingCompletadoUtc);';
    END;

    COMMIT TRANSACTION;

    SELECT
        COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DownstreamAckUtc') AS DownstreamAckUtcBytes,
        COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DisposicionTerminal') AS DisposicionTerminalBytes;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;