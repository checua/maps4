/* ============================================================
   RSMaps 2.0 - Paso 38D
   ALIAS ESTRUCTURADOS PARA ZONAS

   Objetivo:
   - Administrar nombres comunes de una zona (Centro, zona centro,
     centro de la ciudad, etc.).
   - Mantener los alias como datos estructurados, no en Descripcion.
   - Guardar zona + poligono + alias en una sola transaccion.
   - Exponer los alias al editor privado para modificarlos.

   Requiere Pasos 38A y 38B.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53880, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_ZonaAlias', N'U') IS NULL
    THROW 53881, 'No existe RSMAPS_ZonaAlias. Ejecutar primero Paso 38A.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_sp_ObtenerZonaEdicion', N'P') IS NULL
    THROW 53882, 'No existe RSMAPS_sp_ObtenerZonaEdicion. Ejecutar primero Paso 38B.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ObtenerZonaEdicion
    @correo varchar(200),
    @idZona int
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
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdAsesor IS NULL OR @IdCuenta IS NULL
        THROW 53853, 'Sesion de trabajo invalida.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'ZONA_ADMINISTRAR'
          AND p.Activo = 1
    )
        THROW 53854, 'El rol actual no puede administrar zonas.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Zona z
        WHERE z.IdZona = @idZona
          AND z.IdCuenta = @IdCuenta
    )
        THROW 53855, 'La zona no existe dentro de la cuenta actual.', 1;

    SELECT TOP (1)
        z.IdZona,
        z.Codigo,
        z.Nombre,
        z.Descripcion,
        z.Prioridad,
        z.ColorHex,
        z.Activa,
        zp.VerticesJson,
        ISNULL
        (
            (
                SELECT za.Alias
                FROM dbo.RSMAPS_ZonaAlias za
                WHERE za.IdZona = z.IdZona
                  AND za.Activo = 1
                ORDER BY za.Alias
                FOR JSON PATH
            ),
            N'[]'
        ) AS AliasesJson
    FROM dbo.RSMAPS_Zona z
    LEFT JOIN dbo.RSMAPS_ZonaPoligono zp
        ON zp.IdZona = z.IdZona
       AND zp.Activo = 1
    WHERE z.IdZona = @idZona
      AND z.IdCuenta = @IdCuenta
    ORDER BY zp.Orden, zp.IdZonaPoligono;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GuardarZona
    @correo varchar(200),
    @idZona int = NULL,
    @codigo varchar(60),
    @nombre nvarchar(120),
    @descripcion nvarchar(500) = NULL,
    @prioridad int = 100,
    @colorHex char(7) = NULL,
    @verticesJson nvarchar(max),
    @poligonoWkt nvarchar(max),
    @aliasesJson nvarchar(max) = N'[]'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor int;
    DECLARE @IdCuenta int;
    DECLARE @RolCodigo varchar(30);
    DECLARE @Poligono geometry;

    SET @codigo = UPPER(LTRIM(RTRIM(@codigo)));
    SET @nombre = LTRIM(RTRIM(@nombre));
    SET @descripcion = NULLIF(LTRIM(RTRIM(@descripcion)), N'');
    SET @colorHex = NULLIF(LTRIM(RTRIM(@colorHex)), '');
    SET @aliasesJson = COALESCE(NULLIF(LTRIM(RTRIM(@aliasesJson)), N''), N'[]');

    IF LEN(ISNULL(@codigo, '')) < 2
        THROW 53856, 'El codigo de zona no es valido.', 1;

    IF LEN(ISNULL(@nombre, N'')) < 2
        THROW 53857, 'El nombre de zona no es valido.', 1;

    IF @prioridad NOT BETWEEN 0 AND 10000
        THROW 53858, 'La prioridad de zona no es valida.', 1;

    IF ISJSON(@verticesJson) <> 1
        THROW 53859, 'Los vertices de la zona no son JSON valido.', 1;

    IF (SELECT COUNT(*) FROM OPENJSON(@verticesJson)) < 3
        THROW 53860, 'La zona necesita al menos tres vertices.', 1;

    IF ISJSON(@aliasesJson) <> 1
        THROW 53883, 'Los alias de la zona no son JSON valido.', 1;

    BEGIN TRY
        SET @Poligono = geometry::STGeomFromText(@poligonoWkt, 4326);
    END TRY
    BEGIN CATCH
        THROW 53861, 'El poligono recibido no es valido.', 1;
    END CATCH;

    IF @Poligono IS NULL
       OR @Poligono.STSrid <> 4326
       OR @Poligono.STGeometryType() NOT IN ('Polygon', 'MultiPolygon')
       OR @Poligono.STIsValid() <> 1
       OR @Poligono.STIsEmpty() = 1
        THROW 53862, 'La geometria de zona no es valida.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM OPENJSON(@aliasesJson)
        WITH
        (
            Alias nvarchar(150) '$.alias',
            AliasNormalizado nvarchar(150) '$.aliasNormalizado'
        ) a
        WHERE LEN(LTRIM(RTRIM(ISNULL(a.Alias, N'')))) < 2
           OR LEN(LTRIM(RTRIM(ISNULL(a.AliasNormalizado, N'')))) < 2
    )
        THROW 53884, 'Hay un alias de zona no valido.', 1;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

    IF @IdAsesor IS NULL OR @IdCuenta IS NULL
        THROW 53863, 'Sesion de trabajo invalida.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'ZONA_ADMINISTRAR'
          AND p.Activo = 1
    )
        THROW 53864, 'El rol actual no puede administrar zonas.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Zona z
        WHERE z.IdCuenta = @IdCuenta
          AND z.Codigo = @codigo
          AND (@idZona IS NULL OR z.IdZona <> @idZona)
    )
        THROW 53865, 'Ya existe otra zona con ese codigo.', 1;

    IF @idZona IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.RSMAPS_Zona z
           WHERE z.IdZona = @idZona
             AND z.IdCuenta = @IdCuenta
       )
        THROW 53866, 'La zona a editar no pertenece a la cuenta actual.', 1;

    BEGIN TRANSACTION;

    IF @idZona IS NULL
    BEGIN
        INSERT dbo.RSMAPS_Zona
        (
            IdCuenta, Codigo, Nombre, Descripcion, Prioridad, ColorHex,
            Activa, CreadaPorIdAsesor, FechaCreacionUtc
        )
        VALUES
        (
            @IdCuenta, @codigo, @nombre, @descripcion, @prioridad, @colorHex,
            1, @IdAsesor, SYSUTCDATETIME()
        );

        SET @idZona = CONVERT(int, SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.RSMAPS_Zona
        SET Codigo = @codigo,
            Nombre = @nombre,
            Descripcion = @descripcion,
            Prioridad = @prioridad,
            ColorHex = @colorHex,
            ModificadaPorIdAsesor = @IdAsesor,
            FechaModificacionUtc = SYSUTCDATETIME()
        WHERE IdZona = @idZona
          AND IdCuenta = @IdCuenta;
    END;

    /* MVP: un poligono principal editable por zona. */
    DELETE FROM dbo.RSMAPS_ZonaPoligono
    WHERE IdZona = @idZona;

    INSERT dbo.RSMAPS_ZonaPoligono
    (
        IdZona, Nombre, Orden, VerticesJson, Poligono, Activo,
        CreadoPorIdAsesor, FechaCreacionUtc
    )
    VALUES
    (
        @idZona, N'Poligono principal', 10, @verticesJson, @Poligono, 1,
        @IdAsesor, SYSUTCDATETIME()
    );

    /* Los alias recibidos representan el estado completo actual. */
    DELETE FROM dbo.RSMAPS_ZonaAlias
    WHERE IdZona = @idZona;

    ;WITH AliasEntrada AS
    (
        SELECT
            LTRIM(RTRIM(a.Alias)) AS Alias,
            LOWER(LTRIM(RTRIM(a.AliasNormalizado))) AS AliasNormalizado,
            ROW_NUMBER() OVER
            (
                PARTITION BY LOWER(LTRIM(RTRIM(a.AliasNormalizado)))
                ORDER BY LTRIM(RTRIM(a.Alias))
            ) AS rn
        FROM OPENJSON(@aliasesJson)
        WITH
        (
            Alias nvarchar(150) '$.alias',
            AliasNormalizado nvarchar(150) '$.aliasNormalizado'
        ) a
    )
    INSERT dbo.RSMAPS_ZonaAlias
    (
        IdZona, Alias, AliasNormalizado, Activo, FechaCreacionUtc
    )
    SELECT
        @idZona, Alias, AliasNormalizado, 1, SYSUTCDATETIME()
    FROM AliasEntrada
    WHERE rn = 1;

    COMMIT;

    EXEC dbo.RSMAPS_sp_RecalcularZonasCuenta @correo = @correo;

    SELECT @idZona AS IdZona;
END;
GO

/* Prueba controlada de alias sobre CENTRO si existe. No deja cambios. */
DECLARE @IdZonaPrueba int;
DECLARE @CorreoPrueba varchar(200);

SELECT TOP (1) @IdZonaPrueba = z.IdZona
FROM dbo.RSMAPS_Zona z
WHERE z.Codigo = 'CENTRO'
ORDER BY z.IdZona;

SELECT TOP (1) @CorreoPrueba = u.correo
FROM dbo.RSMAPS_Zona z
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdCuenta = z.IdCuenta
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = cu.IdAsesor
WHERE z.IdZona = @IdZonaPrueba
  AND cu.RolCodigo IN ('PROPIETARIO','ADMINISTRADOR')
ORDER BY cu.EsPredeterminada DESC, u.idAsesor;

IF @IdZonaPrueba IS NULL OR @CorreoPrueba IS NULL
BEGIN
    SELECT 'OMITIDA - NO HAY ZONA/ADMIN PARA PRUEBA DE ALIAS' AS EstadoPrueba;
END
ELSE
BEGIN
    DECLARE @AliasesAntes int = (SELECT COUNT(*) FROM dbo.RSMAPS_ZonaAlias WHERE IdZona = @IdZonaPrueba);

    BEGIN TRANSACTION;

    INSERT dbo.RSMAPS_ZonaAlias (IdZona, Alias, AliasNormalizado, Activo)
    SELECT @IdZonaPrueba, N'centro de la ciudad prueba 38D', N'centro de la ciudad prueba 38d', 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.RSMAPS_ZonaAlias
        WHERE IdZona = @IdZonaPrueba
          AND AliasNormalizado = N'centro de la ciudad prueba 38d'
    );

    SELECT
        @IdZonaPrueba AS IdZonaPrueba,
        Alias,
        AliasNormalizado,
        Activo,
        'OK - ALIAS ESTRUCTURADO' AS EstadoPrueba
    FROM dbo.RSMAPS_ZonaAlias
    WHERE IdZona = @IdZonaPrueba
      AND AliasNormalizado = N'centro de la ciudad prueba 38d';

    ROLLBACK;

    SELECT
        @AliasesAntes AS AliasAntes,
        (SELECT COUNT(*) FROM dbo.RSMAPS_ZonaAlias WHERE IdZona = @IdZonaPrueba) AS AliasDespues,
        CASE
            WHEN @AliasesAntes = (SELECT COUNT(*) FROM dbo.RSMAPS_ZonaAlias WHERE IdZona = @IdZonaPrueba)
            THEN 'OK - ROLLBACK COMPLETO'
            ELSE 'REVISAR'
        END AS EstadoRollback;
END;
GO

SELECT
    OBJECT_ID(N'dbo.RSMAPS_sp_ObtenerZonaEdicion', N'P') AS SpObtenerZonaEdicion,
    OBJECT_ID(N'dbo.RSMAPS_sp_GuardarZona', N'P') AS SpGuardarZona,
    'OK - PASO 38D SQL INSTALADO' AS Estado;
