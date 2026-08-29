/* ============================================================
   RSMaps 2.0 - Paso 43
   RADAR AGENT: EXPLORACION DE CHATS BAJO DEMANDA

   Objetivo:
   - El monitoreo normal no recorre todo el catalogo de WhatsApp.
   - RSMaps puede solicitar explicitamente una exploracion completa.
   - La solicitud persiste aunque el Agent este desconectado o se reinicie.
   - Una exploracion terminada se marca con la misma marca UTC solicitada,
     evitando perder una solicitud mas nueva que llegue durante el barrido.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54300, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
BEGIN
    THROW 54301, 'Primero debe ejecutarse 40_radar_agent_pairing.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.RSMAPS_RadarAgentDevice', 'ExploracionChatsSolicitadaUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarAgentDevice
            ADD ExploracionChatsSolicitadaUtc datetime2(3) NULL;
    END;

    IF COL_LENGTH('dbo.RSMAPS_RadarAgentDevice', 'ExploracionChatsCompletadaUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarAgentDevice
            ADD ExploracionChatsCompletadaUtc datetime2(3) NULL;
    END;

    COMMIT TRANSACTION;

    SELECT
        COL_LENGTH('dbo.RSMAPS_RadarAgentDevice', 'ExploracionChatsSolicitadaUtc') AS ExploracionSolicitadaBytes,
        COL_LENGTH('dbo.RSMAPS_RadarAgentDevice', 'ExploracionChatsCompletadaUtc') AS ExploracionCompletadaBytes;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
