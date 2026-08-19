/* ============================================================
   RSMaps 2.0 - Paso 11
   CICLO DE VIDA Y VISIBILIDAD DE INMUEBLES

   Base esperada: mapsMarkers

   Objetivo:
   - Separar el ESTADO comercial del inmueble de su VISIBILIDAD.
   - Dejar de depender a futuro del concepto ambiguo "eliminar = vendido".
   - Preparar inventario privado, colaboración, enlaces no listados y
     marketplace público para Web / Android / iOS.
   - Conservar el comportamiento actual de los inmuebles existentes:
     se clasifican inicialmente como PUBLICADO + PUBLICO.

   IMPORTANTE:
   - NO elimina ni mueve inmuebles.
   - NO modifica RSMAPS_InmuebleVendido.
   - NO cambia todavía los procedimientos de listado/eliminación.
   - Las fechas históricas reales de publicación NO se inventan; los
     inmuebles legacy conservan FechaPublicacionUtc = NULL.
   - El historial creado para los inmuebles existentes queda marcado
     explícitamente como MIGRACION, no como fecha real de publicación.
   - Es idempotente: puede reejecutarse si un intento anterior falló.

   NOTA TECNICA:
   Las operaciones que referencian columnas creadas en este mismo script
   se ejecutan mediante SQL dinámico. SQL Server compila un lote antes de
   ejecutarlo y, sin esta separación, reportaría "Invalid column name"
   aunque el ALTER TABLE aparezca antes en el texto.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 51100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 51101, 'No existe dbo.RSMAPS_Inmueble.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Catálogo de estados del ciclo de vida
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_EstadoInmueble', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_EstadoInmueble
        (
            Codigo      varchar(20) NOT NULL,
            Nombre      nvarchar(100) NOT NULL,
            Descripcion nvarchar(400) NULL,
            EsCierre    bit NOT NULL CONSTRAINT DF_RSMAPS_EstadoInmueble_EsCierre DEFAULT (0),
            Activo      bit NOT NULL CONSTRAINT DF_RSMAPS_EstadoInmueble_Activo DEFAULT (1),
            Orden       int NOT NULL,

            CONSTRAINT PK_RSMAPS_EstadoInmueble PRIMARY KEY (Codigo)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'BORRADOR')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('BORRADOR', N'Borrador', N'Captura en progreso; todavía no debe considerarse oferta publicada.', 0, 10);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'PUBLICADO')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('PUBLICADO', N'Publicado', N'Inmueble activo comercialmente y disponible según su visibilidad.', 0, 20);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'PAUSADO')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('PAUSADO', N'Pausado', N'Oferta temporalmente suspendida sin considerarla retirada o cerrada.', 0, 30);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'RETIRADO')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('RETIRADO', N'Retirado', N'Propiedad retirada del mercado sin afirmar que fue vendida o rentada.', 0, 40);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'VENDIDO')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('VENDIDO', N'Vendido', N'Operación de venta cerrada y confirmada.', 1, 50);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_EstadoInmueble WHERE Codigo = 'RENTADO')
        INSERT dbo.RSMAPS_EstadoInmueble (Codigo, Nombre, Descripcion, EsCierre, Orden)
        VALUES ('RENTADO', N'Rentado', N'Operación de renta cerrada y confirmada.', 1, 60);

    /* ------------------------------------------------------------
       2. Catálogo de visibilidad
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_VisibilidadInmueble', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_VisibilidadInmueble
        (
            Codigo      varchar(20) NOT NULL,
            Nombre      nvarchar(100) NOT NULL,
            Descripcion nvarchar(400) NULL,
            Activo      bit NOT NULL CONSTRAINT DF_RSMAPS_VisibilidadInmueble_Activo DEFAULT (1),
            Orden       int NOT NULL,

            CONSTRAINT PK_RSMAPS_VisibilidadInmueble PRIMARY KEY (Codigo)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble WHERE Codigo = 'CUENTA')
        INSERT dbo.RSMAPS_VisibilidadInmueble (Codigo, Nombre, Descripcion, Orden)
        VALUES ('CUENTA', N'Solo mi cuenta', N'Visible únicamente para usuarios autorizados de la misma cuenta.', 10);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble WHERE Codigo = 'COLABORADORES')
        INSERT dbo.RSMAPS_VisibilidadInmueble (Codigo, Nombre, Descripcion, Orden)
        VALUES ('COLABORADORES', N'Colaboradores', N'Visible en una futura red de colaboración entre asesores autorizados.', 20);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble WHERE Codigo = 'ENLACE')
        INSERT dbo.RSMAPS_VisibilidadInmueble (Codigo, Nombre, Descripcion, Orden)
        VALUES ('ENLACE', N'Solo con enlace', N'No aparece en búsquedas públicas, pero puede consultarse mediante un enlace compartido.', 30);

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_VisibilidadInmueble WHERE Codigo = 'PUBLICO')
        INSERT dbo.RSMAPS_VisibilidadInmueble (Codigo, Nombre, Descripcion, Orden)
        VALUES ('PUBLICO', N'Público', N'Elegible para aparecer en el marketplace público cuando su estado también lo permita.', 40);

    /* ------------------------------------------------------------
       3. Extender RSMAPS_Inmueble
       Cada ALTER se compila de forma independiente.
       ------------------------------------------------------------ */
    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'EstadoCodigo') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble
            ADD EstadoCodigo varchar(20) NOT NULL
                CONSTRAINT DF_RSMAPS_Inmueble_EstadoCodigo
                DEFAULT (''PUBLICADO'') WITH VALUES;';
    END;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'VisibilidadCodigo') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble
            ADD VisibilidadCodigo varchar(20) NOT NULL
                CONSTRAINT DF_RSMAPS_Inmueble_VisibilidadCodigo
                DEFAULT (''PUBLICO'') WITH VALUES;';
    END;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaPublicacionUtc') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble
            ADD FechaPublicacionUtc datetime2(0) NULL;';
    END;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'FechaUltimoCambioEstadoUtc') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble
            ADD FechaUltimoCambioEstadoUtc datetime2(0) NULL;';
    END;

    /* Si las columnas existieran por un intento parcial anterior, completar
       únicamente NULLs sin cambiar valores ya clasificados. */
    EXEC sys.sp_executesql N'
        UPDATE dbo.RSMAPS_Inmueble
        SET EstadoCodigo = ''PUBLICADO''
        WHERE EstadoCodigo IS NULL;

        UPDATE dbo.RSMAPS_Inmueble
        SET VisibilidadCodigo = ''PUBLICO''
        WHERE VisibilidadCodigo IS NULL;';

    /* ------------------------------------------------------------
       4. Foreign Keys de catálogos
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_Estado'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_Estado
                FOREIGN KEY (EstadoCodigo)
                REFERENCES dbo.RSMAPS_EstadoInmueble(Codigo);

            ALTER TABLE dbo.RSMAPS_Inmueble
                CHECK CONSTRAINT FK_RSMAPS_Inmueble_Estado;';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_Visibilidad'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_Visibilidad
                FOREIGN KEY (VisibilidadCodigo)
                REFERENCES dbo.RSMAPS_VisibilidadInmueble(Codigo);

            ALTER TABLE dbo.RSMAPS_Inmueble
                CHECK CONSTRAINT FK_RSMAPS_Inmueble_Visibilidad;';
    END;

    /* ------------------------------------------------------------
       5. Historial de cambios

       Aún no se crea FK hacia RSMAPS_Inmueble porque el procedimiento
       legacy de eliminación borra físicamente la fila activa.
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_InmuebleCambioEstado', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_InmuebleCambioEstado
        (
            IdCambio            bigint IDENTITY(1,1) NOT NULL,
            IdInmueble          int NOT NULL,
            IdCuenta            int NULL,
            EstadoAnterior      varchar(20) NULL,
            EstadoNuevo         varchar(20) NULL,
            VisibilidadAnterior varchar(20) NULL,
            VisibilidadNueva    varchar(20) NULL,
            IdAsesorCambio      int NULL,
            FechaCambioUtc      datetime2(0) NOT NULL
                CONSTRAINT DF_RSMAPS_InmuebleCambioEstado_Fecha DEFAULT (SYSUTCDATETIME()),
            Motivo              nvarchar(500) NULL,
            Origen              varchar(30) NOT NULL
                CONSTRAINT DF_RSMAPS_InmuebleCambioEstado_Origen DEFAULT ('SISTEMA'),

            CONSTRAINT PK_RSMAPS_InmuebleCambioEstado PRIMARY KEY (IdCambio)
        );

        CREATE INDEX IX_RSMAPS_InmuebleCambioEstado_IdInmueble_Fecha
            ON dbo.RSMAPS_InmuebleCambioEstado(IdInmueble, FechaCambioUtc DESC);

        CREATE INDEX IX_RSMAPS_InmuebleCambioEstado_IdCuenta_Fecha
            ON dbo.RSMAPS_InmuebleCambioEstado(IdCuenta, FechaCambioUtc DESC);
    END;

    /* Registrar línea base de migración una sola vez por inmueble. */
    EXEC sys.sp_executesql N'
        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble,
            IdCuenta,
            EstadoAnterior,
            EstadoNuevo,
            VisibilidadAnterior,
            VisibilidadNueva,
            IdAsesorCambio,
            Motivo,
            Origen
        )
        SELECT
            i.idInmueble,
            i.IdCuenta,
            NULL,
            i.EstadoCodigo,
            NULL,
            i.VisibilidadCodigo,
            NULL,
            N''Clasificación inicial inferida durante migración RSMaps 2.0; fecha histórica real de publicación desconocida.'',
            ''MIGRACION''
        FROM dbo.RSMAPS_Inmueble i
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_InmuebleCambioEstado h
            WHERE h.IdInmueble = i.idInmueble
              AND h.Origen = ''MIGRACION''
              AND h.EstadoAnterior IS NULL
        );';

    /* ------------------------------------------------------------
       6. Índices para inventario / marketplace
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IX_RSMAPS_Inmueble_Estado_Visibilidad'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_RSMAPS_Inmueble_Estado_Visibilidad
            ON dbo.RSMAPS_Inmueble(EstadoCodigo, VisibilidadCodigo, IdCuenta);';
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IX_RSMAPS_Inmueble_Cuenta_Estado'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_RSMAPS_Inmueble_Cuenta_Estado
            ON dbo.RSMAPS_Inmueble(IdCuenta, EstadoCodigo, idAsesor);';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   7. VALIDACIONES FINALES
   También dinámicas porque las columnas pueden haber sido creadas arriba.
   ============================================================ */
EXEC sys.sp_executesql N'
    SELECT
        COUNT(*) AS TotalInmuebles,
        SUM(CASE WHEN EstadoCodigo = ''PUBLICADO'' THEN 1 ELSE 0 END) AS Publicados,
        SUM(CASE WHEN VisibilidadCodigo = ''PUBLICO'' THEN 1 ELSE 0 END) AS Publicos,
        SUM(CASE WHEN FechaPublicacionUtc IS NULL THEN 1 ELSE 0 END) AS FechaPublicacionDesconocida
    FROM dbo.RSMAPS_Inmueble;

    SELECT
        EstadoCodigo,
        VisibilidadCodigo,
        COUNT(*) AS Inmuebles
    FROM dbo.RSMAPS_Inmueble
    GROUP BY EstadoCodigo, VisibilidadCodigo
    ORDER BY EstadoCodigo, VisibilidadCodigo;';

SELECT Codigo, Nombre, EsCierre, Activo, Orden
FROM dbo.RSMAPS_EstadoInmueble
ORDER BY Orden;

SELECT Codigo, Nombre, Activo, Orden
FROM dbo.RSMAPS_VisibilidadInmueble
ORDER BY Orden;

SELECT
    COUNT(*) AS RegistrosHistorialMigracion,
    COUNT(DISTINCT IdInmueble) AS InmueblesConLineaBase
FROM dbo.RSMAPS_InmuebleCambioEstado
WHERE Origen = 'MIGRACION';

SELECT
    fk.name AS ForeignKey,
    fk.is_disabled AS Deshabilitada,
    fk.is_not_trusted AS NoConfiable
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
  AND fk.name IN
  (
      N'FK_RSMAPS_Inmueble_Estado',
      N'FK_RSMAPS_Inmueble_Visibilidad'
  )
ORDER BY fk.name;

PRINT 'Paso 11 RSMaps 2.0 terminado correctamente.';
