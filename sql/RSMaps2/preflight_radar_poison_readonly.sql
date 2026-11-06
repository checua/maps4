/* Hito 3. Execute only with an already authorized connection to mapsMarkers.
   SELECT only. No payload, message text, credentials or error details returned.
   Metadata visibility may be restricted. An empty result does not prove absence.
   Read committed can block and counts can change while workers run.
   Run sections separately if an expected dependency/column is missing.
   356 is interpreted as the numeric processing primary key. */
SELECT DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS ObservedUtc,
       ORIGINAL_LOGIN() AS OriginalLogin, USER_NAME() AS DatabasePrincipal,
       IS_MEMBER(N'db_owner') AS IsDbOwner,
       HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL') AS HasControl,
       HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DEFINITION') AS ViewDefinition,
       HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE PROCEDURE') AS CreateProcedure,
       HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER') AS AlterDbo,
       HAS_PERMS_BY_NAME(N'dbo.RSMAPS_RadarMessageProcessing',N'OBJECT',N'ALTER') AS AlterProcessing,
       HAS_PERMS_BY_NAME(N'dbo.RSMAPS_RadarReactivateProcessing',N'OBJECT',N'ALTER') AS AlterAdminProcedure,
       HAS_PERMS_BY_NAME(N'dbo.RSMAPS_RadarReactivateProcessing',N'OBJECT',N'EXECUTE') AS ExecuteAdminProcedure;

SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE database_id=DB_ID();
SELECT name, type_desc FROM sys.objects
WHERE name IN(N'RSMAPS_RadarMessageProcessing',N'RSMAPS_RadarMessageEvent',
 N'RSMAPS_RadarMessageDelivery',N'RSMAPS_RadarAgentDevice',N'RSMAPS_RadarReactivateProcessing');
SELECT c.name, TYPE_NAME(c.system_type_id) AS SqlType, c.max_length,
 c.is_nullable, c.is_identity, d.definition AS DefaultDefinition
FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
WHERE c.object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing') ORDER BY c.column_id;
SELECT i.name, i.is_unique, i.is_disabled, i.has_filter, i.filter_definition,
 ic.key_ordinal, ic.is_included_column, c.name AS ColumnName
FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing') ORDER BY i.index_id,ic.index_column_id;
SELECT name, definition, is_disabled,is_not_trusted FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing');
SELECT name,is_disabled,is_not_trusted,OBJECT_NAME(referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing');

SELECT Estado,COUNT_BIG(*) AS Records,MIN(IntentosProcesamiento) AS MinAttempts,
 MAX(IntentosProcesamiento) AS MaxAttempts FROM dbo.RSMAPS_RadarMessageProcessing GROUP BY Estado;
SELECT IdRadarMessageProcessing,Estado,IntentosProcesamiento,DetectadoUtc,
 LeaseHastaUtc,ReintentarDespuesUtc,ActualizadoUtc,
 CASE WHEN Estado=N'PROCESANDO' AND (LeaseHastaUtc IS NULL OR LeaseHastaUtc<=SYSUTCDATETIME()) THEN 1 ELSE 0 END AS ExpiredLease,
 CASE WHEN ResultadoCentralJson IS NULL AND (Estado=N'DETECTADO' OR
 (Estado=N'FALLIDO_REINTENTABLE' AND (ReintentarDespuesUtc IS NULL OR ReintentarDespuesUtc<=SYSUTCDATETIME())) OR
 (Estado=N'PROCESANDO' AND (LeaseHastaUtc IS NULL OR LeaseHastaUtc<=SYSUTCDATETIME()))) THEN 1 ELSE 0 END AS RecoveryStateEligible,
 CASE WHEN IntentosProcesamiento>=5 THEN 1 ELSE 0 END AS AtOrAboveInitialMigrationBudget
FROM dbo.RSMAPS_RadarMessageProcessing
WHERE Estado NOT IN(N'COMPLETADO',N'FALLIDO_PERMANENTE');

/* Structural screening only. The C# reader is authoritative for full identity/list validation.
   CASE sanitizes invalid JSON before JSON functions, regardless of optimizer ordering. */
SELECT IdRadarMessageProcessing,Estado,DownstreamAckUtc,
 ISJSON(ResultadoCentralJson) AS ValidJson,
 CASE WHEN NULLIF(JSON_VALUE(CASE WHEN ISJSON(ResultadoCentralJson)=1 THEN ResultadoCentralJson ELSE N'{}' END,N'$.motor'),N'') IS NULL
 AND NULLIF(JSON_VALUE(CASE WHEN ISJSON(ResultadoCentralJson)=1 THEN ResultadoCentralJson ELSE N'{}' END,N'$.Motor'),N'') IS NULL THEN 1 ELSE 0 END AS MotorRequiresReview,
 CASE WHEN LEFT(LTRIM(COALESCE(
 JSON_QUERY(CASE WHEN ISJSON(ResultadoCentralJson)=1 THEN ResultadoCentralJson ELSE N'{}' END,N'$.solicitudes'),
 JSON_QUERY(CASE WHEN ISJSON(ResultadoCentralJson)=1 THEN ResultadoCentralJson ELSE N'{}' END,N'$.Solicitudes'),N'')),1)<>N'[' THEN 1 ELSE 0 END AS RequestsRequireReview
FROM dbo.RSMAPS_RadarMessageProcessing WHERE Estado=N'COMPLETADO';

SELECT Estado,COUNT_BIG(*) AS DownstreamCount FROM dbo.RSMAPS_RadarMessageProcessing
WHERE Estado=N'COMPLETADO' AND DownstreamAckUtc IS NULL GROUP BY Estado;
SELECT Estado,COUNT_BIG(*) AS DeliveryCount FROM dbo.RSMAPS_RadarMessageDelivery GROUP BY Estado;

SELECT IdRadarMessageProcessing,Estado,IntentosProcesamiento,LeaseHastaUtc,
 ReintentarDespuesUtc,TerminadoUtc,DownstreamAckUtc,ActualizadoUtc,VersionFila,
 CASE WHEN ResultadoCentralJson IS NULL THEN 0 ELSE 1 END AS HasCentralResult,
 CASE WHEN UltimoError IS NULL THEN 0 ELSE 1 END AS HasError
FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=356;
SELECT TipoEvento,EstadoAnterior,EstadoNuevo,CreadoUtc
FROM dbo.RSMAPS_RadarMessageEvent WHERE IdRadarMessageProcessing=356 ORDER BY CreadoUtc;
SELECT Estado,COUNT_BIG(*) AS RelatedDeliveries FROM dbo.RSMAPS_RadarMessageDelivery
WHERE IdRadarMessageProcessing=356 GROUP BY Estado;

/* Inspect role membership without executing as another principal. Effective rights above
   describe this connection only. Repeat through an authorized app-principal session later. */
SELECT member.name AS PrincipalName,role.name AS RoleName
FROM sys.database_role_members m JOIN sys.database_principals member ON member.principal_id=m.member_principal_id
JOIN sys.database_principals role ON role.principal_id=m.role_principal_id;

/* Hito 4E: execute this section ONLY in a previously authorized application
   connection. An administrator's session does not identify the application's user.
   No EXECUTE AS, credentials, access tokens or connection strings are involved. */
SELECT DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS ObservedUtc,
 ORIGINAL_LOGIN() AS OriginalLogin, SUSER_SNAME() AS ExecutionLogin,
 USER_NAME() AS DatabaseUser, SESSION_USER AS SessionUser,
 IS_MEMBER(N'db_owner') AS DbOwner,
 HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL') AS DatabaseControl,
 HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'ALTER ANY USER') AS AlterAnyUser,
 HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'ALTER ANY ROLE') AS AlterAnyRole;
SELECT permission_name FROM sys.fn_my_permissions(NULL,N'DATABASE');
SELECT permission_name FROM sys.fn_my_permissions(N'dbo',N'SCHEMA');
SELECT permission_name FROM sys.fn_my_permissions(N'dbo.RSMAPS_RadarMessageProcessing',N'OBJECT');
SELECT permission_name FROM sys.fn_my_permissions(N'dbo.RSMAPS_RadarMessageEvent',N'OBJECT');
SELECT permission_name FROM sys.fn_my_permissions(N'dbo.RSMAPS_RadarMessageDelivery',N'OBJECT');
SELECT name,authentication_type_desc,type_desc FROM sys.database_principals
WHERE principal_id=USER_ID();
