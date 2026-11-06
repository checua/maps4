/* Optional route; no caller/startup or application/public EXECUTE grant is added.
   One batch: a failed database/schema check cannot be bypassed by continuing after GO. */
SET NOCOUNT ON;
IF DB_NAME() <> N'mapsMarkers' AND
   NOT (DB_NAME() LIKE N'RadarPoisonRegression[_]%' AND COALESCE(TRY_CONVERT(int, SESSION_CONTEXT(N'RadarPoisonTest')), 0) = 1)
    THROW 54800, 'Unexpected database for RADAR administrative route.', 1;
IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'MaxIntentosProcesamiento') <> 4 OR
   COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'MaxIntentosProcesamiento') IS NULL OR
   NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
               AND name=N'VersionFila' AND system_type_id=189 AND is_nullable=0) OR
   OBJECT_ID(N'dbo.RSMAPS_RadarMessageEvent', N'U') IS NULL OR
   OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery', N'U') IS NULL
    THROW 54801, 'Requires compatible schema 57.', 1;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_RadarReactivateProcessing
    @Id bigint,
    @ExpectedVersion binary(8),
    @AdditionalAttempts int,
    @Reason nvarchar(1000),
    @Authorize bit = 0
WITH EXECUTE AS CALLER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @Authorize <> 1 OR @Authorize IS NULL OR
       COALESCE(HAS_PERMS_BY_NAME(DB_NAME(), N''DATABASE'', N''CONTROL''), 0) <> 1
        THROW 54802, ''Explicit administrative authorization required.'', 1;
    SET @Reason=LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(@Reason,CHAR(9),N'' ''),CHAR(10),N'' ''),CHAR(13),N'' ''),NCHAR(160),N'' '')));
    IF @AdditionalAttempts IS NULL OR @AdditionalAttempts NOT BETWEEN 1 AND 100 OR
       NULLIF(@Reason, N'''') IS NULL OR @ExpectedVersion IS NULL
        THROW 54803, ''Reason, version and bounded additional budget required.'', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @attempts int, @previousBudget int;
        SELECT @attempts = IntentosProcesamiento, @previousBudget = MaxIntentosProcesamiento
        FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
        WHERE IdRadarMessageProcessing = @Id AND VersionFila = @ExpectedVersion
          AND Estado = N''FALLIDO_PERMANENTE'' AND DownstreamAckUtc IS NULL
          AND ResultadoCentralJson IS NULL AND LeaseToken IS NULL;
        IF @attempts IS NULL OR @attempts > 2147483547 OR EXISTS
           (SELECT 1 FROM dbo.RSMAPS_RadarMessageDelivery WITH (UPDLOCK, HOLDLOCK) WHERE IdRadarMessageProcessing = @Id)
            THROW 54804, ''Not eligible for administrative reactivation.'', 1;
        UPDATE dbo.RSMAPS_RadarMessageProcessing
        SET Estado = N''FALLIDO_REINTENTABLE'', MaxIntentosProcesamiento = @attempts + @AdditionalAttempts,
            ReintentarDespuesUtc = NULL, TerminadoUtc = NULL, ActualizadoUtc = SYSUTCDATETIME()
        WHERE IdRadarMessageProcessing = @Id AND VersionFila = @ExpectedVersion AND Estado=N''FALLIDO_PERMANENTE'';
        IF @@ROWCOUNT<>1 THROW 54804, ''Reactivation lost its expected version.'', 1;
        INSERT dbo.RSMAPS_RadarMessageEvent
            (IdRadarMessageProcessing, TipoEvento, EstadoAnterior, EstadoNuevo, Detalle, CreadoUtc)
        VALUES (@Id, N''REACTIVACION_ADMINISTRATIVA'', N''FALLIDO_PERMANENTE'', N''FALLIDO_REINTENTABLE'',
            CONCAT(N''SQL principal: '', ORIGINAL_LOGIN(), N''; database user: '', USER_NAME(),
                   N''; additional attempts: '', @AdditionalAttempts, N''; previous budget: '', @previousBudget,
                   N''; new budget: '', @attempts + @AdditionalAttempts, N''; previous attempts: '', @attempts,
                   N''; reason: '', @Reason), SYSUTCDATETIME());
        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH;
END;';
