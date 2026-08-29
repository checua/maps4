/* ============================================================
   RSMaps 2.0 - Paso 42
   RADAR AGENT: DESCUBRIMIENTO DE CHATS DE WHATSAPP

   Objetivo:
   - Registrar únicamente los nombres de chats detectados por cada Agent.
   - No almacenar mensajes, participantes ni contenido de conversaciones.
   - Permitir seleccionar desde RSMaps qué chats debe monitorear el Agent.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
BEGIN
    THROW 54201, 'Primero debe ejecutarse 40_radar_agent_pairing.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentChatDiscovery', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarAgentChatDiscovery
        (
            IdAgent         uniqueidentifier NOT NULL,
            NombreChat      nvarchar(300) NOT NULL,
            UltimoVistoUtc  datetime2(0) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarAgentChatDiscovery_UltimoVistoUtc
                DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_RadarAgentChatDiscovery
                PRIMARY KEY (IdAgent, NombreChat),
            CONSTRAINT FK_RSMAPS_RadarAgentChatDiscovery_Device
                FOREIGN KEY (IdAgent)
                REFERENCES dbo.RSMAPS_RadarAgentDevice(IdAgent)
        );

        CREATE INDEX IX_RSMAPS_RadarAgentChatDiscovery_UltimoVisto
            ON dbo.RSMAPS_RadarAgentChatDiscovery(IdAgent, UltimoVistoUtc DESC);
    END;

    COMMIT TRANSACTION;

    SELECT OBJECT_ID(N'dbo.RSMAPS_RadarAgentChatDiscovery', N'U') AS RadarAgentChatDiscoveryObjectId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
