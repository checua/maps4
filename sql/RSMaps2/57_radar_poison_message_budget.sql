/* Additive migration after 44 + 45. Review and apply separately; never runs at startup.
   Existing states/payloads stay unchanged; the new budget defaults to five. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'mapsMarkers' AND
   NOT (DB_NAME() LIKE N'RadarPoisonRegression[_]%' AND COALESCE(TRY_CONVERT(int, SESSION_CONTEXT(N'RadarPoisonTest')), 0) = 1)
    THROW 54700, 'Unexpected database for RADAR migration.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing', N'U') IS NULL OR
   OBJECT_ID(N'dbo.RSMAPS_RadarMessageEvent', N'U') IS NULL OR
   COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DownstreamAckUtc') IS NULL OR
   COALESCE(COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'Estado'), -1) <> 80 OR
   NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
               AND i.name = N'UX_RSMAPS_RadarMessageProcessing_Idempotencia' AND i.is_unique = 1
               AND i.is_disabled = 0 AND i.has_filter = 0 AND i.is_hypothetical = 0
               AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 3
               AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                           WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'IdAgent')
               AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                           WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'ChatOrigen')
               AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                           WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'MessageId'))
    THROW 54701, 'Requires compatible schema 44 and 45.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'MaxIntentosProcesamiento') IS NULL
        ALTER TABLE dbo.RSMAPS_RadarMessageProcessing ADD MaxIntentosProcesamiento int NOT NULL
            CONSTRAINT DF_RadarProcessing_MaxIntentos DEFAULT (5) WITH VALUES;
    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'MaxIntentosProcesamiento') <> 4
        THROW 54702, 'Incompatible attempt budget column.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
                   AND name = N'MaxIntentosProcesamiento' AND system_type_id = 56 AND is_nullable = 0)
        THROW 54702, 'Attempt budget must be a non-null int.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
                   AND name = N'CK_RadarProcessing_MaxIntentos')
        EXEC(N'ALTER TABLE dbo.RSMAPS_RadarMessageProcessing WITH CHECK ADD CONSTRAINT CK_RadarProcessing_MaxIntentos
               CHECK (MaxIntentosProcesamiento > 0);');
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
                   AND name = N'CK_RadarProcessing_PermanentFailure')
        EXEC(N'ALTER TABLE dbo.RSMAPS_RadarMessageProcessing WITH CHECK ADD CONSTRAINT CK_RadarProcessing_PermanentFailure
               CHECK (Estado <> N''FALLIDO_PERMANENTE'' OR
                (TerminadoUtc IS NOT NULL AND UltimoError IS NOT NULL AND LeaseToken IS NULL
                 AND LeaseHastaUtc IS NULL AND ReintentarDespuesUtc IS NULL AND DownstreamAckUtc IS NULL));');
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
                AND name IN (N'CK_RadarProcessing_MaxIntentos', N'CK_RadarProcessing_PermanentFailure')
                AND (is_disabled = 1 OR is_not_trusted = 1))
        THROW 54703, 'RADAR checks must be enabled and trusted.', 1;
    -- Same-name constraints are not proof of the expected invariant. Fail closed on drift.
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints c
        CROSS APPLY (SELECT LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.definition,
            N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''),CHAR(13),N''),CHAR(10),N''),CHAR(9),N'')) AS normalized) d
        WHERE c.parent_object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing') AND
        ((c.name=N'CK_RadarProcessing_MaxIntentos' AND d.normalized<>N'maxintentosprocesamiento>0') OR
         (c.name=N'CK_RadarProcessing_PermanentFailure' AND d.normalized<>
          N'estado<>n''fallido_permanente''orterminadoutcisnotnullandultimoerrorisnotnullandleasetokenisnullandleasehastautcisnullandreintentardespuesutcisnullanddownstreamackutcisnull')))
        THROW 54704, 'Unexpected RADAR constraint definition; manual review required.', 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
