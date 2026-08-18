/* ============================================================
   RSMaps 2.0 - Paso 00
   RESPALDO LÓGICO DE OBJETOS RSMAPS

   Base esperada: mapsMarkers
   Origen actual: dbo.RSMAPS_*
   Destino: schema RSMAPS_BKP_20260818

   IMPORTANTE:
   - Este script NO elimina ni modifica datos de las tablas actuales.
   - Crea copias lógicas de todas las tablas dbo.RSMAPS_*.
   - Guarda además la definición SQL de procedimientos RSMAPS_*.
   - SELECT INTO copia columnas y datos, pero no recrea PK/FK/índices.
     El objetivo es tener una copia de seguridad lógica antes de migrar.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50001, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

DECLARE @BackupSchema sysname = N'RSMAPS_BKP_20260818';
DECLARE @sql nvarchar(max);

/* ------------------------------------------------------------
   1. Crear schema de respaldo si no existe
   ------------------------------------------------------------ */
IF NOT EXISTS (
    SELECT 1
    FROM sys.schemas
    WHERE name = @BackupSchema
)
BEGIN
    SET @sql = N'CREATE SCHEMA ' + QUOTENAME(@BackupSchema) + N';';
    EXEC sys.sp_executesql @sql;
END;

/* ------------------------------------------------------------
   2. Evitar sobrescribir un respaldo existente
   ------------------------------------------------------------ */
IF EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = @BackupSchema
)
BEGIN
    THROW 50002, 'El schema de respaldo ya contiene tablas. No se sobrescribirá.', 1;
END;

/* ------------------------------------------------------------
   3. Copiar todas las tablas dbo.RSMAPS_*
   ------------------------------------------------------------ */
DECLARE @TableName sysname;

DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'dbo'
  AND t.name LIKE N'RSMAPS[_]%'
ORDER BY t.name;

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql =
        N'SELECT * INTO ' + QUOTENAME(@BackupSchema) + N'.' + QUOTENAME(@TableName) +
        N' FROM dbo.' + QUOTENAME(@TableName) + N';';

    PRINT N'Copiando dbo.' + @TableName + N' ...';
    EXEC sys.sp_executesql @sql;

    FETCH NEXT FROM table_cursor INTO @TableName;
END;

CLOSE table_cursor;
DEALLOCATE table_cursor;

/* ------------------------------------------------------------
   4. Guardar definiciones de objetos SQL RSMAPS_*
   ------------------------------------------------------------ */
SET @sql = N'
CREATE TABLE ' + QUOTENAME(@BackupSchema) + N'.ObjetosSQL
(
    Id int IDENTITY(1,1) NOT NULL,
    SchemaName sysname NOT NULL,
    ObjectName sysname NOT NULL,
    ObjectType nvarchar(60) NOT NULL,
    Definition nvarchar(max) NULL,
    FechaRespaldo datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);';
EXEC sys.sp_executesql @sql;

SET @sql = N'
INSERT INTO ' + QUOTENAME(@BackupSchema) + N'.ObjetosSQL
(
    SchemaName,
    ObjectName,
    ObjectType,
    Definition
)
SELECT
    s.name,
    o.name,
    o.type_desc,
    OBJECT_DEFINITION(o.object_id)
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name = N''dbo''
  AND o.name LIKE N''RSMAPS[_]%''
  AND o.type IN (N''P'', N''V'', N''FN'', N''IF'', N''TF'', N''TR'');';
EXEC sys.sp_executesql @sql;

/* ------------------------------------------------------------
   5. Validación de conteos origen vs respaldo
   ------------------------------------------------------------ */
CREATE TABLE #Conteos
(
    Tabla sysname NOT NULL,
    RegistrosOrigen bigint NOT NULL,
    RegistrosBackup bigint NOT NULL
);

DECLARE @Origen bigint;
DECLARE @Backup bigint;

DECLARE validate_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'dbo'
  AND t.name LIKE N'RSMAPS[_]%'
ORDER BY t.name;

OPEN validate_cursor;
FETCH NEXT FROM validate_cursor INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @CountOut = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@TableName) + N';';
    EXEC sys.sp_executesql @sql, N'@CountOut bigint OUTPUT', @CountOut = @Origen OUTPUT;

    SET @sql = N'SELECT @CountOut = COUNT_BIG(*) FROM ' + QUOTENAME(@BackupSchema) + N'.' + QUOTENAME(@TableName) + N';';
    EXEC sys.sp_executesql @sql, N'@CountOut bigint OUTPUT', @CountOut = @Backup OUTPUT;

    INSERT INTO #Conteos (Tabla, RegistrosOrigen, RegistrosBackup)
    VALUES (@TableName, @Origen, @Backup);

    FETCH NEXT FROM validate_cursor INTO @TableName;
END;

CLOSE validate_cursor;
DEALLOCATE validate_cursor;

SELECT
    Tabla,
    RegistrosOrigen,
    RegistrosBackup,
    CASE
        WHEN RegistrosOrigen = RegistrosBackup THEN 'OK'
        ELSE 'REVISAR'
    END AS Estado
FROM #Conteos
ORDER BY Tabla;

/* ------------------------------------------------------------
   6. Resumen
   ------------------------------------------------------------ */
SELECT
    @BackupSchema AS SchemaBackup,
    COUNT(*) AS TablasRespaldadas
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @BackupSchema
  AND t.name <> N'ObjetosSQL';

SET @sql = N'SELECT COUNT(*) AS ObjetosSQLRespaldados FROM ' + QUOTENAME(@BackupSchema) + N'.ObjetosSQL;';
EXEC sys.sp_executesql @sql;

PRINT 'Respaldo lógico RSMaps terminado.';
