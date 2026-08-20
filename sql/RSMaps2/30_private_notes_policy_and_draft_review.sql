/* ============================================================
   RSMaps 2.0 - Paso 30
   NOTAS PRIVADAS + REVISION DE BORRADORES REALES

   Objetivo:
   - Formalizar que las notas privadas son informacion interna.
   - Migrar de forma NO destructiva el contenido legacy de contacto_a
     hacia NotasPrivadas cuando esta ultima este vacia.
   - Permitir lectura de notas privadas al asesor responsable o a un
     ADMINISTRADOR / PROPIETARIO de la misma cuenta mediante permiso RBAC.
   - No exponer notas privadas a usuarios publicos, otros asesores o
     CAPTURISTA por defecto.
   - Listar todos los BORRADORES actuales para decidir cuales conservar
     y cuales limpiar, SIN borrar nada.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53000, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 53001, 'No existe dbo.RSMAPS_Inmueble.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 53002, 'No existe dbo.RSMAPS_CuentaUsuario.', 1;
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL OR OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
    THROW 53003, 'Falta la base RBAC.', 1;
IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'NotasPrivadas') IS NULL
    THROW 53004, 'Falta NotasPrivadas. Ejecutar primero Paso 29.', 1;

/* ============================================================
   1. Permiso explicito para consultar notas privadas de la cuenta
   ============================================================ */
IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA')
BEGIN
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion, Activo)
    VALUES
    (
        'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA',
        N'Ver notas privadas de la cuenta',
        N'Permite consultar notas privadas de inmuebles pertenecientes a la misma cuenta. No autoriza publicarlas ni compartirlas externamente.',
        1
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ADMINISTRADOR'
      AND PermisoCodigo = 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'PROPIETARIO'
      AND PermisoCodigo = 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA');

/* ============================================================
   2. Migracion NO destructiva de notas legacy
   contacto_a se conserva por compatibilidad; solo copiamos cuando
   NotasPrivadas aun no tiene contenido.
   ============================================================ */
DECLARE @LegacyConContenido int;
DECLARE @Migradas int = 0;

SELECT @LegacyConContenido = COUNT(*)
FROM dbo.RSMAPS_Inmueble
WHERE NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), contacto_a))), N'') IS NOT NULL;

UPDATE dbo.RSMAPS_Inmueble
SET NotasPrivadas = CONVERT(nvarchar(max), contacto_a)
WHERE NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), NotasPrivadas))), N'') IS NULL
  AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), contacto_a))), N'') IS NOT NULL;

SET @Migradas = @@ROWCOUNT;

SELECT
    @LegacyConContenido AS InmueblesConNotasLegacy,
    @Migradas AS NotasCopiadasANotasPrivadas,
    (SELECT COUNT(*)
     FROM dbo.RSMAPS_Inmueble
     WHERE NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), NotasPrivadas))), N'') IS NOT NULL) AS InmueblesConNotasPrivadasActuales;

/* ============================================================
   3. Lectura segura de notas privadas
   ============================================================ */
DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_GetNotasPrivadasInmueble
    @idInmueble INT,
    @correo VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;
    DECLARE @IdCuentaInmueble INT;
    DECLARE @IdAsesorResponsable INT;
    DECLARE @PuedeVerCuenta BIT = 0;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 53020, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
    WHERE cu.IdAsesor = @IdAsesor
      AND cu.Activo = 1
      AND c.Activo = 1
      AND cu.EsPredeterminada = 1
    ORDER BY cu.IdCuenta;

    IF @IdCuenta IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM dbo.RSMAPS_CuentaUsuario cu
        INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
        WHERE cu.IdAsesor = @IdAsesor
          AND cu.Activo = 1
          AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @IdCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @IdAsesor
              AND cu.Activo = 1
              AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 53021, ''El usuario no pertenece a ninguna cuenta activa.'', 1;
        ELSE
            THROW 53022, ''El usuario pertenece a varias cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT
        @IdCuentaInmueble = i.IdCuenta,
        @IdAsesorResponsable = i.idAsesor
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;

    IF @IdCuentaInmueble IS NULL
        THROW 53023, ''El inmueble no existe.'', 1;

    IF @IdCuentaInmueble <> @IdCuenta
        THROW 53024, ''El inmueble pertenece a otra cuenta.'', 1;

    SELECT @PuedeVerCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @IdAsesorResponsable <> @IdAsesor AND @PuedeVerCuenta = 0
        THROW 53025, ''No tienes permiso para consultar las notas privadas de este inmueble.'', 1;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor AS IdAsesorResponsable,
        i.NotasPrivadas
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;
END;';

EXEC sys.sp_executesql @sql;

/* ============================================================
   4. Matriz de privacidad instalada
   ============================================================ */
SELECT
    rp.RolCodigo,
    rp.PermisoCodigo
FROM dbo.RSMAPS_RolPermiso rp
WHERE rp.PermisoCodigo = 'INMUEBLE_VER_NOTAS_PRIVADAS_CUENTA'
ORDER BY rp.RolCodigo;

/* ============================================================
   5. Inventario de BORRADORES REALES actuales - NO BORRA NADA
   ============================================================ */
SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    CONCAT(u.nombres, ' ', u.aPaterno) AS AsesorResponsable,
    u.correo,
    i.idTipo,
    tp.nombre AS TipoPropiedad,
    i.precio,
    i.terreno,
    i.construccion,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.FechaPublicacionUtc,
    i.FechaUltimaEdicionUtc,
    i.lat,
    i.lng,
    i.direccion,
    CASE WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), i.observaciones))), N'') IS NULL THEN 0 ELSE 1 END AS TieneDescripcion,
    CASE WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), i.NotasPrivadas))), N'') IS NULL THEN 0 ELSE 1 END AS TieneNotasPrivadas,
    ISNULL(img.Imagenes, 0) AS Imagenes
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad = i.idTipo
OUTER APPLY
(
    SELECT MAX(ISNULL(ii.Imagenes, 0)) AS Imagenes
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = i.idInmueble
) img
WHERE i.EstadoCodigo = 'BORRADOR'
ORDER BY i.idInmueble;

SELECT
    COUNT(*) AS BorradoresActuales,
    'OK - REVISION DE BORRADORES SIN ELIMINAR DATOS' AS EstadoPaso30
FROM dbo.RSMAPS_Inmueble
WHERE EstadoCodigo = 'BORRADOR';
