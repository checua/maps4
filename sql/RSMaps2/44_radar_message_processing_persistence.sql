/* ============================================================
   RSMaps 2.0 - Paso 44
   RADAR: PERSISTENCIA DURABLE E IDEMPOTENCIA DE PROCESAMIENTO

   Objetivo:
   - No perder demandas pendientes si el RADAR Agent se reinicia.
   - Identificar de forma idempotente cada mensaje por Agent + chat + MessageId.
   - Conservar el resultado central para poder reanudar sin repetir Intelligence.
   - Registrar entregas de alertas por separado para evitar duplicados.
   - Conservar una bitacora de transiciones para auditoria y diagnostico.

   Nota:
   - Este paso crea solamente la base durable.
   - El comportamiento del Agent se conectara a estas tablas en pasos posteriores.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54400, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
BEGIN
    THROW 54401, 'Primero debe ejecutarse 40_radar_agent_pairing.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Estado durable por mensaje de WhatsApp.

       Clave idempotente:
           IdAgent + ChatOrigen + MessageId

       ResultadoCentralJson queda disponible para reanudar un mensaje
       ya interpretado/matcheado sin volver a consumir Intelligence.
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarMessageProcessing
        (
            IdRadarMessageProcessing bigint IDENTITY(1,1) NOT NULL,
            IdAgent                  uniqueidentifier NOT NULL,
            ChatOrigen               nvarchar(300) NOT NULL,
            MessageId                nvarchar(200) NOT NULL,
            Autor                    nvarchar(250) NULL,
            Telefono                 nvarchar(80) NULL,
            MensajeOriginal          nvarchar(max) NULL,

            Estado                   nvarchar(40) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageProcessing_Estado DEFAULT (N'DETECTADO'),
            IntentosProcesamiento    int NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageProcessing_Intentos DEFAULT (0),
            ReintentarDespuesUtc     datetime2(3) NULL,
            UltimoIntentoUtc         datetime2(3) NULL,
            UltimoError              nvarchar(2000) NULL,

            MotorInteligencia        nvarchar(200) NULL,
            ResultadoCentralJson     nvarchar(max) NULL,

            DetectadoUtc             datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageProcessing_DetectadoUtc DEFAULT (SYSUTCDATETIME()),
            InterpretadoUtc          datetime2(3) NULL,
            MatchingCompletadoUtc    datetime2(3) NULL,
            TerminadoUtc             datetime2(3) NULL,
            CreadoUtc                datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageProcessing_CreadoUtc DEFAULT (SYSUTCDATETIME()),
            ActualizadoUtc           datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageProcessing_ActualizadoUtc DEFAULT (SYSUTCDATETIME()),

            LeaseToken               uniqueidentifier NULL,
            LeaseHastaUtc            datetime2(3) NULL,
            VersionFila              rowversion NOT NULL,

            CONSTRAINT PK_RSMAPS_RadarMessageProcessing
                PRIMARY KEY (IdRadarMessageProcessing),
            CONSTRAINT FK_RSMAPS_RadarMessageProcessing_Agent
                FOREIGN KEY (IdAgent) REFERENCES dbo.RSMAPS_RadarAgentDevice(IdAgent),
            CONSTRAINT CK_RSMAPS_RadarMessageProcessing_Intentos
                CHECK (IntentosProcesamiento >= 0)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
          AND name = N'UX_RSMAPS_RadarMessageProcessing_Idempotencia'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_RadarMessageProcessing_Idempotencia
            ON dbo.RSMAPS_RadarMessageProcessing(IdAgent, ChatOrigen, MessageId);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
          AND name = N'IX_RSMAPS_RadarMessageProcessing_Pendientes'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_RadarMessageProcessing_Pendientes
            ON dbo.RSMAPS_RadarMessageProcessing(IdAgent, Estado, ReintentarDespuesUtc, ActualizadoUtc)
            INCLUDE (ChatOrigen, MessageId, LeaseHastaUtc);
    END;

    /* ------------------------------------------------------------
       2. Entregas idempotentes.

       Una sola demanda puede producir 0, 1 o varias solicitudes.
       La entrega se identifica por una ClaveEntrega determinista para
       que un retry no vuelva a enviar una alerta ya confirmada.
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarMessageDelivery
        (
            IdRadarMessageDelivery   bigint IDENTITY(1,1) NOT NULL,
            IdRadarMessageProcessing bigint NOT NULL,
            SolicitudIndice          int NOT NULL,
            ClaveEntrega             nvarchar(300) NOT NULL,
            IdInmueble               int NULL,
            Puntuacion               int NULL,
            Estado                   nvarchar(40) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageDelivery_Estado DEFAULT (N'PENDIENTE'),
            PayloadAlerta            nvarchar(max) NULL,
            IntentosEntrega          int NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageDelivery_Intentos DEFAULT (0),
            UltimoIntentoUtc         datetime2(3) NULL,
            EnviadoUtc               datetime2(3) NULL,
            UltimoError              nvarchar(2000) NULL,
            CreadoUtc                datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageDelivery_CreadoUtc DEFAULT (SYSUTCDATETIME()),
            ActualizadoUtc           datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageDelivery_ActualizadoUtc DEFAULT (SYSUTCDATETIME()),
            VersionFila              rowversion NOT NULL,

            CONSTRAINT PK_RSMAPS_RadarMessageDelivery
                PRIMARY KEY (IdRadarMessageDelivery),
            CONSTRAINT FK_RSMAPS_RadarMessageDelivery_Processing
                FOREIGN KEY (IdRadarMessageProcessing)
                REFERENCES dbo.RSMAPS_RadarMessageProcessing(IdRadarMessageProcessing),
            CONSTRAINT CK_RSMAPS_RadarMessageDelivery_SolicitudIndice
                CHECK (SolicitudIndice >= 0),
            CONSTRAINT CK_RSMAPS_RadarMessageDelivery_Intentos
                CHECK (IntentosEntrega >= 0),
            CONSTRAINT CK_RSMAPS_RadarMessageDelivery_Puntuacion
                CHECK (Puntuacion IS NULL OR (Puntuacion >= 0 AND Puntuacion <= 100))
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery')
          AND name = N'UX_RSMAPS_RadarMessageDelivery_ClaveEntrega'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_RadarMessageDelivery_ClaveEntrega
            ON dbo.RSMAPS_RadarMessageDelivery(IdRadarMessageProcessing, ClaveEntrega);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery')
          AND name = N'IX_RSMAPS_RadarMessageDelivery_Pendientes'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_RadarMessageDelivery_Pendientes
            ON dbo.RSMAPS_RadarMessageDelivery(Estado, ActualizadoUtc)
            INCLUDE (IdRadarMessageProcessing, SolicitudIndice, ClaveEntrega, IdInmueble);
    END;

    /* ------------------------------------------------------------
       3. Bitacora append-only de eventos/transiciones.
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageEvent', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarMessageEvent
        (
            IdRadarMessageEvent      bigint IDENTITY(1,1) NOT NULL,
            IdRadarMessageProcessing bigint NOT NULL,
            TipoEvento               nvarchar(60) NOT NULL,
            EstadoAnterior           nvarchar(40) NULL,
            EstadoNuevo              nvarchar(40) NULL,
            Detalle                  nvarchar(2000) NULL,
            DatosJson                nvarchar(max) NULL,
            CreadoUtc                datetime2(3) NOT NULL
                CONSTRAINT DF_RSMAPS_RadarMessageEvent_CreadoUtc DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_RadarMessageEvent
                PRIMARY KEY (IdRadarMessageEvent),
            CONSTRAINT FK_RSMAPS_RadarMessageEvent_Processing
                FOREIGN KEY (IdRadarMessageProcessing)
                REFERENCES dbo.RSMAPS_RadarMessageProcessing(IdRadarMessageProcessing)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageEvent')
          AND name = N'IX_RSMAPS_RadarMessageEvent_Timeline'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_RadarMessageEvent_Timeline
            ON dbo.RSMAPS_RadarMessageEvent(IdRadarMessageProcessing, CreadoUtc, IdRadarMessageEvent);
    END;

    COMMIT TRANSACTION;

    SELECT
        OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing', N'U') AS RadarMessageProcessingObjectId,
        OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery', N'U') AS RadarMessageDeliveryObjectId,
        OBJECT_ID(N'dbo.RSMAPS_RadarMessageEvent', N'U') AS RadarMessageEventObjectId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
