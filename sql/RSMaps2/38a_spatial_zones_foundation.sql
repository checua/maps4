/* ============================================================
   RSMaps 2.0 - Paso 38A
   FUNDACION DE ZONAS ESPACIALES PRIVADAS

   Objetivo:
   - Definir zonas comerciales mediante uno o varios poligonos.
   - Permitir que un inmueble pertenezca a varias zonas.
   - Elegir automaticamente una zona principal por prioridad.
   - Conservar asignaciones manuales separadas de las automaticas.
   - Preparar la base para Administrador de Zonas, filtros y RADAR.

   Decisiones de esta fase:
   - Las zonas pertenecen a una cuenta RSMaps (multi-tenant).
   - Los poligonos usan geometry con SRID 4326.
   - X = longitud, Y = latitud.
   - VerticesJson conserva los vertices editables para la UI.
   - Un inmueble puede estar, por ejemplo, en MEZQUITAL y SUR.
   - La zona principal automatica se resuelve por:
       1) Prioridad DESC
       2) area del poligono ASC (zona mas especifica)
       3) IdZona
   - Una zona principal MANUAL tiene precedencia sobre la automatica.

   IMPORTANTE:
   - Este paso NO dibuja aun el administrador visual.
   - No modifica direccion, latitud o longitud de los inmuebles.
   - La prueba final se ejecuta dentro de TRANSACTION y hace ROLLBACK.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53800, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 53801, 'No existe dbo.RSMAPS_Inmueble.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    THROW 53802, 'No existe dbo.RSMAPS_Cuenta. Ejecutar primero la base multi-tenant.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Usuario', N'U') IS NULL
    THROW 53803, 'No existe dbo.RSMAPS_Usuario.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL
    THROW 53804, 'No existe dbo.RSMAPS_Permiso. Ejecutar primero Paso 21.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 53805, 'No existe dbo.RSMAPS_RolPermiso. Ejecutar primero Paso 21.', 1;
GO

/* ------------------------------------------------------------
   1. Permiso administrativo de zonas
   ------------------------------------------------------------ */
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_Permiso
    WHERE Codigo = 'ZONA_ADMINISTRAR'
)
BEGIN
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion, Activo)
    VALUES
    (
        'ZONA_ADMINISTRAR',
        N'Administrar zonas geograficas',
        N'Permite crear, editar y recalcular zonas privadas de la cuenta mediante poligonos.',
        1
    );
END;

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'ADMINISTRADOR')
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.RSMAPS_RolPermiso
       WHERE RolCodigo = 'ADMINISTRADOR'
         AND PermisoCodigo = 'ZONA_ADMINISTRAR'
   )
BEGIN
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'ZONA_ADMINISTRAR');
END;

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'PROPIETARIO')
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.RSMAPS_RolPermiso
       WHERE RolCodigo = 'PROPIETARIO'
         AND PermisoCodigo = 'ZONA_ADMINISTRAR'
   )
BEGIN
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'ZONA_ADMINISTRAR');
END;
GO

/* ------------------------------------------------------------
   2. Catalogo de zonas
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_Zona', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_Zona
    (
        IdZona                  int IDENTITY(1,1) NOT NULL,
        IdCuenta                int NOT NULL,
        Codigo                  varchar(60) NOT NULL,
        Nombre                  nvarchar(120) NOT NULL,
        Descripcion             nvarchar(500) NULL,
        Prioridad               int NOT NULL
            CONSTRAINT DF_RSMAPS_Zona_Prioridad DEFAULT (100),
        ColorHex                char(7) NULL,
        Activa                  bit NOT NULL
            CONSTRAINT DF_RSMAPS_Zona_Activa DEFAULT (1),
        CreadaPorIdAsesor       int NULL,
        FechaCreacionUtc        datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_Zona_FechaCreacionUtc DEFAULT (SYSUTCDATETIME()),
        ModificadaPorIdAsesor   int NULL,
        FechaModificacionUtc    datetime2(0) NULL,
        CONSTRAINT PK_RSMAPS_Zona PRIMARY KEY (IdZona),
        CONSTRAINT FK_RSMAPS_Zona_Cuenta
            FOREIGN KEY (IdCuenta) REFERENCES dbo.RSMAPS_Cuenta(IdCuenta),
        CONSTRAINT FK_RSMAPS_Zona_CreadaPor
            FOREIGN KEY (CreadaPorIdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
        CONSTRAINT FK_RSMAPS_Zona_ModificadaPor
            FOREIGN KEY (ModificadaPorIdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
        CONSTRAINT CK_RSMAPS_Zona_Prioridad CHECK (Prioridad BETWEEN 0 AND 10000),
        CONSTRAINT CK_RSMAPS_Zona_Codigo CHECK (LEN(LTRIM(RTRIM(Codigo))) >= 2),
        CONSTRAINT CK_RSMAPS_Zona_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) >= 2),
        CONSTRAINT CK_RSMAPS_Zona_ColorHex CHECK
        (
            ColorHex IS NULL
            OR (LEN(ColorHex) = 7 AND LEFT(ColorHex, 1) = '#')
        )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Zona')
      AND name = 'UX_RSMAPS_Zona_CuentaCodigo'
)
BEGIN
    CREATE UNIQUE INDEX UX_RSMAPS_Zona_CuentaCodigo
        ON dbo.RSMAPS_Zona(IdCuenta, Codigo);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Zona')
      AND name = 'IX_RSMAPS_Zona_CuentaActivaPrioridad'
)
BEGIN
    CREATE INDEX IX_RSMAPS_Zona_CuentaActivaPrioridad
        ON dbo.RSMAPS_Zona(IdCuenta, Activa, Prioridad DESC)
        INCLUDE (Codigo, Nombre);
END;
GO

/* ------------------------------------------------------------
   3. Alias de zona para lenguaje natural / RADAR
   Ejemplo:
   FENADU -> feria, feria nacional, zona fenadu
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_ZonaAlias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_ZonaAlias
    (
        IdZonaAlias       int IDENTITY(1,1) NOT NULL,
        IdZona            int NOT NULL,
        Alias              nvarchar(150) NOT NULL,
        AliasNormalizado   nvarchar(150) NOT NULL,
        Activo             bit NOT NULL
            CONSTRAINT DF_RSMAPS_ZonaAlias_Activo DEFAULT (1),
        FechaCreacionUtc   datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_ZonaAlias_FechaCreacionUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RSMAPS_ZonaAlias PRIMARY KEY (IdZonaAlias),
        CONSTRAINT FK_RSMAPS_ZonaAlias_Zona
            FOREIGN KEY (IdZona) REFERENCES dbo.RSMAPS_Zona(IdZona),
        CONSTRAINT CK_RSMAPS_ZonaAlias_Alias CHECK (LEN(LTRIM(RTRIM(Alias))) >= 2),
        CONSTRAINT CK_RSMAPS_ZonaAlias_AliasNormalizado CHECK (LEN(LTRIM(RTRIM(AliasNormalizado))) >= 2)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_ZonaAlias')
      AND name = 'UX_RSMAPS_ZonaAlias_ZonaNormalizado'
)
BEGIN
    CREATE UNIQUE INDEX UX_RSMAPS_ZonaAlias_ZonaNormalizado
        ON dbo.RSMAPS_ZonaAlias(IdZona, AliasNormalizado);
END;
GO

/* ------------------------------------------------------------
   4. Uno o varios poligonos por zona

   VerticesJson sera el formato editable para Google Maps.
   Poligono es la representacion espacial optimizada para consultas.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_ZonaPoligono', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_ZonaPoligono
    (
        IdZonaPoligono          int IDENTITY(1,1) NOT NULL,
        IdZona                  int NOT NULL,
        Nombre                  nvarchar(120) NULL,
        Orden                   int NOT NULL
            CONSTRAINT DF_RSMAPS_ZonaPoligono_Orden DEFAULT (10),
        VerticesJson            nvarchar(max) NOT NULL,
        Poligono                geometry NOT NULL,
        Activo                  bit NOT NULL
            CONSTRAINT DF_RSMAPS_ZonaPoligono_Activo DEFAULT (1),
        CreadoPorIdAsesor       int NULL,
        FechaCreacionUtc        datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_ZonaPoligono_FechaCreacionUtc DEFAULT (SYSUTCDATETIME()),
        ModificadoPorIdAsesor   int NULL,
        FechaModificacionUtc    datetime2(0) NULL,
        CONSTRAINT PK_RSMAPS_ZonaPoligono PRIMARY KEY (IdZonaPoligono),
        CONSTRAINT FK_RSMAPS_ZonaPoligono_Zona
            FOREIGN KEY (IdZona) REFERENCES dbo.RSMAPS_Zona(IdZona),
        CONSTRAINT FK_RSMAPS_ZonaPoligono_CreadoPor
            FOREIGN KEY (CreadoPorIdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
        CONSTRAINT FK_RSMAPS_ZonaPoligono_ModificadoPor
            FOREIGN KEY (ModificadoPorIdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
        CONSTRAINT CK_RSMAPS_ZonaPoligono_VerticesJson CHECK (ISJSON(VerticesJson) = 1),
        CONSTRAINT CK_RSMAPS_ZonaPoligono_SRID CHECK (Poligono.STSrid = 4326),
        CONSTRAINT CK_RSMAPS_ZonaPoligono_Valido CHECK (Poligono.STIsValid() = 1),
        CONSTRAINT CK_RSMAPS_ZonaPoligono_Tipo CHECK
        (
            Poligono.STGeometryType() IN ('Polygon', 'MultiPolygon')
        )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_ZonaPoligono')
      AND name = 'IX_RSMAPS_ZonaPoligono_ZonaActivo'
)
BEGIN
    CREATE INDEX IX_RSMAPS_ZonaPoligono_ZonaActivo
        ON dbo.RSMAPS_ZonaPoligono(IdZona, Activo, Orden);
END;
GO

/* ------------------------------------------------------------
   5. Relacion N:N Inmueble -> Zona
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_InmuebleZona', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_InmuebleZona
    (
        IdInmueble          int NOT NULL,
        IdZona              int NOT NULL,
        Origen              varchar(15) NOT NULL,
        EsPrincipal         bit NOT NULL
            CONSTRAINT DF_RSMAPS_InmuebleZona_EsPrincipal DEFAULT (0),
        Confianza           decimal(5,2) NULL,
        IdAsesorCambio      int NULL,
        FechaAsignacionUtc  datetime2(0) NOT NULL
            CONSTRAINT DF_RSMAPS_InmuebleZona_FechaAsignacionUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RSMAPS_InmuebleZona PRIMARY KEY (IdInmueble, IdZona),
        CONSTRAINT FK_RSMAPS_InmuebleZona_Inmueble
            FOREIGN KEY (IdInmueble) REFERENCES dbo.RSMAPS_Inmueble(idInmueble),
        CONSTRAINT FK_RSMAPS_InmuebleZona_Zona
            FOREIGN KEY (IdZona) REFERENCES dbo.RSMAPS_Zona(IdZona),
        CONSTRAINT FK_RSMAPS_InmuebleZona_AsesorCambio
            FOREIGN KEY (IdAsesorCambio) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
        CONSTRAINT CK_RSMAPS_InmuebleZona_Origen
            CHECK (Origen IN ('AUTOMATICA', 'MANUAL')),
        CONSTRAINT CK_RSMAPS_InmuebleZona_Confianza
            CHECK (Confianza IS NULL OR Confianza BETWEEN 0 AND 100)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleZona')
      AND name = 'IX_RSMAPS_InmuebleZona_Zona'
)
BEGIN
    CREATE INDEX IX_RSMAPS_InmuebleZona_Zona
        ON dbo.RSMAPS_InmuebleZona(IdZona, EsPrincipal, Origen)
        INCLUDE (IdInmueble, Confianza);
END;
GO

/* Solo puede existir una zona principal por inmueble. */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleZona')
      AND name = 'UX_RSMAPS_InmuebleZona_Principal'
)
BEGIN
    CREATE UNIQUE INDEX UX_RSMAPS_InmuebleZona_Principal
        ON dbo.RSMAPS_InmuebleZona(IdInmueble)
        WHERE EsPrincipal = 1;
END;
GO

/* ------------------------------------------------------------
   6. Recalcular zonas de UN inmueble por lat/lng
   ------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_RecalcularZonasInmueble
    @idInmueble int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdCuenta int;
    DECLARE @Lat float;
    DECLARE @Lng float;
    DECLARE @Punto geometry;

    SELECT
        @IdCuenta = i.IdCuenta,
        @Lat = TRY_CONVERT(float, i.lat),
        @Lng = TRY_CONVERT(float, i.lng)
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @IdCuenta IS NULL
        THROW 53820, 'El inmueble no existe o no pertenece a una cuenta.', 1;

    BEGIN TRANSACTION;

    DELETE FROM dbo.RSMAPS_InmuebleZona
    WHERE IdInmueble = @idInmueble
      AND Origen = 'AUTOMATICA';

    IF @Lat IS NOT NULL
       AND @Lng IS NOT NULL
       AND @Lat BETWEEN -90 AND 90
       AND @Lng BETWEEN -180 AND 180
    BEGIN
        SET @Punto = geometry::Point(@Lng, @Lat, 4326);

        INSERT dbo.RSMAPS_InmuebleZona
        (
            IdInmueble,
            IdZona,
            Origen,
            EsPrincipal,
            Confianza,
            IdAsesorCambio,
            FechaAsignacionUtc
        )
        SELECT DISTINCT
            @idInmueble,
            z.IdZona,
            'AUTOMATICA',
            0,
            CONVERT(decimal(5,2), 100.00),
            NULL,
            SYSUTCDATETIME()
        FROM dbo.RSMAPS_Zona z
        WHERE z.IdCuenta = @IdCuenta
          AND z.Activa = 1
          AND EXISTS
          (
              SELECT 1
              FROM dbo.RSMAPS_ZonaPoligono zp
              WHERE zp.IdZona = z.IdZona
                AND zp.Activo = 1
                AND zp.Poligono.STIntersects(@Punto) = 1
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.RSMAPS_InmuebleZona iz
              WHERE iz.IdInmueble = @idInmueble
                AND iz.IdZona = z.IdZona
          );

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.RSMAPS_InmuebleZona
            WHERE IdInmueble = @idInmueble
              AND EsPrincipal = 1
              AND Origen = 'MANUAL'
        )
        BEGIN
            DECLARE @IdZonaPrincipal int;

            SELECT TOP (1)
                @IdZonaPrincipal = iz.IdZona
            FROM dbo.RSMAPS_InmuebleZona iz
            INNER JOIN dbo.RSMAPS_Zona z
                ON z.IdZona = iz.IdZona
            OUTER APPLY
            (
                SELECT MIN(zp.Poligono.STArea()) AS AreaMinima
                FROM dbo.RSMAPS_ZonaPoligono zp
                WHERE zp.IdZona = z.IdZona
                  AND zp.Activo = 1
                  AND zp.Poligono.STIntersects(@Punto) = 1
            ) areaZona
            WHERE iz.IdInmueble = @idInmueble
              AND iz.Origen = 'AUTOMATICA'
            ORDER BY
                z.Prioridad DESC,
                ISNULL(areaZona.AreaMinima, 1.0E20) ASC,
                z.IdZona ASC;

            IF @IdZonaPrincipal IS NOT NULL
            BEGIN
                UPDATE dbo.RSMAPS_InmuebleZona
                SET EsPrincipal = CASE WHEN IdZona = @IdZonaPrincipal THEN 1 ELSE 0 END
                WHERE IdInmueble = @idInmueble
                  AND Origen = 'AUTOMATICA';
            END;
        END;
    END;

    COMMIT;

    SELECT
        iz.IdInmueble,
        z.IdZona,
        z.Codigo,
        z.Nombre,
        z.Prioridad,
        iz.Origen,
        iz.EsPrincipal,
        iz.Confianza,
        iz.FechaAsignacionUtc
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN dbo.RSMAPS_Zona z
        ON z.IdZona = iz.IdZona
    WHERE iz.IdInmueble = @idInmueble
    ORDER BY iz.EsPrincipal DESC, z.Prioridad DESC, z.Nombre;
END;
GO

/* ------------------------------------------------------------
   7. Recalculo masivo de la cuenta autenticada
   Solo ADMINISTRADOR / PROPIETARIO con ZONA_ADMINISTRAR.
   ------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_RecalcularZonasCuenta
    @correo varchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor int;
    DECLARE @IdCuenta int;
    DECLARE @RolCodigo varchar(30);
    DECLARE @MembresiasActivas int;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53830, 'No existe un usuario RSMaps con el correo autenticado.', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c
            ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @IdAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @IdCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c
                ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @IdAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 53831, 'El usuario no pertenece a ninguna cuenta activa.', 1;
        ELSE
            THROW 53832, 'El usuario pertenece a varias cuentas y no tiene una predeterminada.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p
            ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'ZONA_ADMINISTRAR'
          AND p.Activo = 1
    )
        THROW 53833, 'El rol actual no puede administrar zonas.', 1;

    BEGIN TRANSACTION;

    DELETE iz
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = iz.IdInmueble
    WHERE i.IdCuenta = @IdCuenta
      AND iz.Origen = 'AUTOMATICA';

    INSERT dbo.RSMAPS_InmuebleZona
    (
        IdInmueble,
        IdZona,
        Origen,
        EsPrincipal,
        Confianza,
        IdAsesorCambio,
        FechaAsignacionUtc
    )
    SELECT DISTINCT
        i.idInmueble,
        z.IdZona,
        'AUTOMATICA',
        0,
        CONVERT(decimal(5,2), 100.00),
        NULL,
        SYSUTCDATETIME()
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Zona z
        ON z.IdCuenta = i.IdCuenta
       AND z.Activa = 1
    WHERE i.IdCuenta = @IdCuenta
      AND TRY_CONVERT(float, i.lat) BETWEEN -90 AND 90
      AND TRY_CONVERT(float, i.lng) BETWEEN -180 AND 180
      AND EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_ZonaPoligono zp
          WHERE zp.IdZona = z.IdZona
            AND zp.Activo = 1
            AND zp.Poligono.STIntersects
                (
                    geometry::Point
                    (
                        TRY_CONVERT(float, i.lng),
                        TRY_CONVERT(float, i.lat),
                        4326
                    )
                ) = 1
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_InmuebleZona manual
          WHERE manual.IdInmueble = i.idInmueble
            AND manual.IdZona = z.IdZona
            AND manual.Origen = 'MANUAL'
      );

    ;WITH Candidatas AS
    (
        SELECT
            iz.IdInmueble,
            iz.IdZona,
            ROW_NUMBER() OVER
            (
                PARTITION BY iz.IdInmueble
                ORDER BY
                    z.Prioridad DESC,
                    ISNULL(areaZona.AreaMinima, 1.0E20) ASC,
                    z.IdZona ASC
            ) AS rn
        FROM dbo.RSMAPS_InmuebleZona iz
        INNER JOIN dbo.RSMAPS_Zona z
            ON z.IdZona = iz.IdZona
        INNER JOIN dbo.RSMAPS_Inmueble i
            ON i.idInmueble = iz.IdInmueble
        OUTER APPLY
        (
            SELECT MIN(zp.Poligono.STArea()) AS AreaMinima
            FROM dbo.RSMAPS_ZonaPoligono zp
            WHERE zp.IdZona = z.IdZona
              AND zp.Activo = 1
              AND zp.Poligono.STIntersects
                  (
                      geometry::Point
                      (
                          TRY_CONVERT(float, i.lng),
                          TRY_CONVERT(float, i.lat),
                          4326
                      )
                  ) = 1
        ) areaZona
        WHERE i.IdCuenta = @IdCuenta
          AND iz.Origen = 'AUTOMATICA'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.RSMAPS_InmuebleZona mp
              WHERE mp.IdInmueble = iz.IdInmueble
                AND mp.EsPrincipal = 1
                AND mp.Origen = 'MANUAL'
          )
    )
    UPDATE iz
    SET EsPrincipal = CASE WHEN c.rn = 1 THEN 1 ELSE 0 END
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN Candidatas c
        ON c.IdInmueble = iz.IdInmueble
       AND c.IdZona = iz.IdZona;

    COMMIT;

    SELECT
        @IdCuenta AS IdCuenta,
        COUNT(DISTINCT iz.IdInmueble) AS InmueblesClasificados,
        COUNT(*) AS AsignacionesAutomaticas,
        SUM(CASE WHEN iz.EsPrincipal = 1 THEN 1 ELSE 0 END) AS PrincipalesAutomaticas,
        'OK - ZONAS RECALCULADAS' AS Estado
    FROM dbo.RSMAPS_InmuebleZona iz
    INNER JOIN dbo.RSMAPS_Inmueble i
        ON i.idInmueble = iz.IdInmueble
    WHERE i.IdCuenta = @IdCuenta
      AND iz.Origen = 'AUTOMATICA';
END;
GO

/* ------------------------------------------------------------
   8. Lectura administrativa de zonas de la cuenta
   ------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListarZonasCuenta
    @correo varchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor int;
    DECLARE @IdCuenta int;
    DECLARE @RolCodigo varchar(30);

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdAsesor IS NULL OR @IdCuenta IS NULL
        THROW 53840, 'Sesion de trabajo invalida.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p
            ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'ZONA_ADMINISTRAR'
          AND p.Activo = 1
    )
        THROW 53841, 'El rol actual no puede administrar zonas.', 1;

    SELECT
        z.IdZona,
        z.IdCuenta,
        z.Codigo,
        z.Nombre,
        z.Descripcion,
        z.Prioridad,
        z.ColorHex,
        z.Activa,
        z.CreadaPorIdAsesor,
        z.FechaCreacionUtc,
        z.ModificadaPorIdAsesor,
        z.FechaModificacionUtc,
        ISNULL(p.Poligonos, 0) AS Poligonos,
        ISNULL(a.Alias, 0) AS Alias,
        ISNULL(iz.Inmuebles, 0) AS Inmuebles,
        ISNULL(iz.Principales, 0) AS Principales
    FROM dbo.RSMAPS_Zona z
    OUTER APPLY
    (
        SELECT COUNT(*) AS Poligonos
        FROM dbo.RSMAPS_ZonaPoligono zp
        WHERE zp.IdZona = z.IdZona
          AND zp.Activo = 1
    ) p
    OUTER APPLY
    (
        SELECT COUNT(*) AS Alias
        FROM dbo.RSMAPS_ZonaAlias za
        WHERE za.IdZona = z.IdZona
          AND za.Activo = 1
    ) a
    OUTER APPLY
    (
        SELECT
            COUNT(*) AS Inmuebles,
            SUM(CASE WHEN x.EsPrincipal = 1 THEN 1 ELSE 0 END) AS Principales
        FROM dbo.RSMAPS_InmuebleZona x
        WHERE x.IdZona = z.IdZona
    ) iz
    WHERE z.IdCuenta = @IdCuenta
    ORDER BY z.Activa DESC, z.Prioridad DESC, z.Nombre;
END;
GO

/* ------------------------------------------------------------
   9. PRUEBA CONTROLADA CON ROLLBACK
   - Usa un inmueble real solo como punto de referencia.
   - Crea una zona temporal alrededor de su pin.
   - Recalcula.
   - Comprueba que el inmueble cae dentro.
   - ROLLBACK elimina zona, poligono y asignacion de prueba.
   ------------------------------------------------------------ */
DECLARE @IdInmueblePrueba int;
DECLARE @IdCuentaPrueba int;
DECLARE @IdAsesorPrueba int;
DECLARE @LatPrueba float;
DECLARE @LngPrueba float;
DECLARE @IdZonaPrueba int;
DECLARE @PuntoPrueba geometry;
DECLARE @PoligonoPrueba geometry;

SELECT TOP (1)
    @IdInmueblePrueba = i.idInmueble,
    @IdCuentaPrueba = i.IdCuenta,
    @IdAsesorPrueba = i.idAsesor,
    @LatPrueba = TRY_CONVERT(float, i.lat),
    @LngPrueba = TRY_CONVERT(float, i.lng)
FROM dbo.RSMAPS_Inmueble i
WHERE i.IdCuenta IS NOT NULL
  AND TRY_CONVERT(float, i.lat) BETWEEN -90 AND 90
  AND TRY_CONVERT(float, i.lng) BETWEEN -180 AND 180
ORDER BY i.idInmueble;

IF @IdInmueblePrueba IS NULL
BEGIN
    SELECT
        'OMITIDA - NO HAY INMUEBLE CON COORDENADAS VALIDAS PARA PRUEBA' AS EstadoPrueba;
END
ELSE
BEGIN
    SET @PuntoPrueba = geometry::Point(@LngPrueba, @LatPrueba, 4326);
    SET @PoligonoPrueba = @PuntoPrueba.STBuffer(0.002);

    BEGIN TRANSACTION;

    INSERT dbo.RSMAPS_Zona
    (
        IdCuenta,
        Codigo,
        Nombre,
        Descripcion,
        Prioridad,
        Activa,
        CreadaPorIdAsesor
    )
    VALUES
    (
        @IdCuentaPrueba,
        'PRUEBA_38A',
        N'PRUEBA 38A - ROLLBACK',
        N'Zona temporal para validar punto dentro de poligono.',
        9999,
        1,
        @IdAsesorPrueba
    );

    SET @IdZonaPrueba = SCOPE_IDENTITY();

    INSERT dbo.RSMAPS_ZonaPoligono
    (
        IdZona,
        Nombre,
        Orden,
        VerticesJson,
        Poligono,
        Activo,
        CreadoPorIdAsesor
    )
    VALUES
    (
        @IdZonaPrueba,
        N'Poligono temporal',
        10,
        N'{"tipo":"prueba-buffer","radioGrados":0.002}',
        @PoligonoPrueba,
        1,
        @IdAsesorPrueba
    );

    EXEC dbo.RSMAPS_sp_RecalcularZonasInmueble
        @idInmueble = @IdInmueblePrueba;

    SELECT
        @IdInmueblePrueba AS IdInmueblePrueba,
        @IdZonaPrueba AS IdZonaPrueba,
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.RSMAPS_InmuebleZona iz
                WHERE iz.IdInmueble = @IdInmueblePrueba
                  AND iz.IdZona = @IdZonaPrueba
                  AND iz.Origen = 'AUTOMATICA'
            )
            THEN 'OK - PUNTO CLASIFICADO DENTRO DE ZONA'
            ELSE 'ERROR - NO SE GENERO LA ASIGNACION'
        END AS EstadoPrueba;

    ROLLBACK;

    SELECT
        (SELECT COUNT(*) FROM dbo.RSMAPS_Zona WHERE Codigo = 'PRUEBA_38A') AS ZonasPruebaRestantes,
        (SELECT COUNT(*) FROM dbo.RSMAPS_ZonaPoligono WHERE IdZona = @IdZonaPrueba) AS PoligonosPruebaRestantes,
        (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleZona WHERE IdZona = @IdZonaPrueba) AS AsignacionesPruebaRestantes,
        'OK - ROLLBACK COMPLETO' AS EstadoRollback;
END;
GO

/* Resumen de objetos instalados */
SELECT
    OBJECT_ID(N'dbo.RSMAPS_Zona', N'U') AS Zona,
    OBJECT_ID(N'dbo.RSMAPS_ZonaAlias', N'U') AS ZonaAlias,
    OBJECT_ID(N'dbo.RSMAPS_ZonaPoligono', N'U') AS ZonaPoligono,
    OBJECT_ID(N'dbo.RSMAPS_InmuebleZona', N'U') AS InmuebleZona,
    OBJECT_ID(N'dbo.RSMAPS_sp_RecalcularZonasInmueble', N'P') AS SpRecalcularInmueble,
    OBJECT_ID(N'dbo.RSMAPS_sp_RecalcularZonasCuenta', N'P') AS SpRecalcularCuenta,
    OBJECT_ID(N'dbo.RSMAPS_sp_ListarZonasCuenta', N'P') AS SpListarZonas,
    'OK - PASO 38A INSTALADO' AS Estado;
