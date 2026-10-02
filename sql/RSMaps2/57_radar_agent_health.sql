/*
    RSMaps2 - 57_radar_agent_health.sql
    Snapshot sanitario de RADAR Agent. No almacena contenido de chats ni secretos.
    Script idempotente. No ejecutarlo sin autorización explícita del entorno destino.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'mapsMarkers'
    THROW 55700, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
    THROW 55701, 'Primero debe ejecutarse 40_radar_agent_pairing.sql.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentHealth', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarAgentHealth
        (
            IdAgent                 uniqueidentifier NOT NULL,
            InstanceId              uniqueidentifier NOT NULL,
            Sequence                bigint NOT NULL,
            InstanceStartedUtc      datetime2(0) NOT NULL,
            RecibidoUtc             datetime2(0) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarAgentHealth_RecibidoUtc DEFAULT (SYSUTCDATETIME()),
            AgentUtc                datetime2(0) NOT NULL,
            Version                 nvarchar(64) NOT NULL,
            ListenerState           varchar(24) NOT NULL,
            WhatsAppState           varchar(32) NOT NULL,
            WhatsAppStateSinceUtc   datetime2(0) NULL,
            LastSweepStartedUtc     datetime2(0) NULL,
            LastSweepCompletedUtc   datetime2(0) NULL,
            ChatsConfigured         smallint NOT NULL,
            ChatsReviewed           smallint NOT NULL,
            CentralMode             varchar(20) NOT NULL,
            CentralState            varchar(24) NOT NULL,
            LastCentralSuccessUtc   datetime2(0) NULL,
            FallbackEnabled         bit NOT NULL,
            ErrorCode               varchar(64) NULL,
            ActualizadoUtc          datetime2(0) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarAgentHealth_ActualizadoUtc DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_RadarAgentHealth PRIMARY KEY (IdAgent),
            CONSTRAINT FK_RSMAPS_RadarAgentHealth_Device
                FOREIGN KEY (IdAgent) REFERENCES dbo.RSMAPS_RadarAgentDevice(IdAgent),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_Sequence CHECK (Sequence > 0),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_ListenerState
                CHECK (ListenerState IN ('Starting', 'Running', 'Stopping', 'Error')),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_WhatsAppState
                CHECK (WhatsAppState IN ('Starting', 'Ready', 'WaitingForReady', 'LoggedOut', 'Error', 'Unknown')),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_CentralMode
                CHECK (CentralMode IN ('Central', 'Local')),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_CentralState
                CHECK (CentralState IN ('Healthy', 'Degraded', 'NotRecentlyUsed', 'Unknown')),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_ChatCounts
                CHECK (ChatsConfigured BETWEEN 0 AND 2000
                   AND ChatsReviewed BETWEEN 0 AND ChatsConfigured),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_SweepOrder
                CHECK (LastSweepCompletedUtc IS NULL
                    OR LastSweepStartedUtc IS NULL
                    OR LastSweepCompletedUtc >= LastSweepStartedUtc),
            CONSTRAINT CK_RSMAPS_RadarAgentHealth_ErrorCode
                CHECK (ErrorCode IS NULL OR ErrorCode NOT LIKE '%[^A-Z0-9_]%')
        );

        CREATE INDEX IX_RSMAPS_RadarAgentHealth_RecibidoUtc
            ON dbo.RSMAPS_RadarAgentHealth(RecibidoUtc);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    OBJECT_ID(N'dbo.RSMAPS_RadarAgentHealth', N'U') AS RadarAgentHealthObjectId,
    OBJECTPROPERTY(OBJECT_ID(N'dbo.RSMAPS_RadarAgentHealth'), 'TableHasPrimaryKey') AS HasPrimaryKey;
