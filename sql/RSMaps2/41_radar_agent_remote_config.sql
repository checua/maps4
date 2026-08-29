/* ============================================================
   RSMaps 2.0 - Paso 41
   RADAR AGENT: CONFIGURACIÓN CENTRALIZADA POR DISPOSITIVO

   Objetivo:
   - Mover la configuración operativa del Agent a RSMaps.
   - Mantener una configuración independiente por IdAgent.
   - Permitir que el Agent sincronice chats, destino e intervalo
     usando únicamente su credencial de dispositivo.
   - Conservar el JSON local como fallback durante la transición.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
BEGIN
    THROW 54101, 'Primero debe ejecutarse 40_radar_agent_pairing.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentConfig', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarAgentConfig
        (
            IdAgent                uniqueidentifier NOT NULL,
            ChatsMonitoreadosJson  nvarchar(max) NOT NULL,
            DestinoAlertas         nvarchar(200) NULL,
            IntervaloRevisionMs    int NOT NULL,
            TerminosBusquedaJson   nvarchar(max) NOT NULL,
            ActualizadoUtc         datetime2(0) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarAgentConfig_ActualizadoUtc DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_RadarAgentConfig PRIMARY KEY (IdAgent),
            CONSTRAINT FK_RSMAPS_RadarAgentConfig_Device
                FOREIGN KEY (IdAgent) REFERENCES dbo.RSMAPS_RadarAgentDevice(IdAgent),
            CONSTRAINT CK_RSMAPS_RadarAgentConfig_Intervalo
                CHECK (IntervaloRevisionMs BETWEEN 10000 AND 1200000),
            CONSTRAINT CK_RSMAPS_RadarAgentConfig_ChatsJson
                CHECK (ISJSON(ChatsMonitoreadosJson) = 1),
            CONSTRAINT CK_RSMAPS_RadarAgentConfig_TerminosJson
                CHECK (ISJSON(TerminosBusquedaJson) = 1)
        );
    END;

    COMMIT TRANSACTION;

    SELECT OBJECT_ID(N'dbo.RSMAPS_RadarAgentConfig', N'U') AS RadarAgentConfigObjectId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
