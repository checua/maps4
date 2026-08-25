/* ============================================================
   RSMaps 2.0 - Paso 39A
   INTELIGENCIA DE ZONAS EN EL INVENTARIO PRIVADO

   Objetivo:
   - Exponer en la lectura autorizada del inventario la zona principal.
   - Exponer tambien todas las zonas activas a las que pertenece un inmueble.
   - Mantener intacto el alcance RBAC existente.
   - No cambiar todavia la UI: las columnas nuevas son aditivas.

   Requiere Pasos 38A-38D.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53910, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_InmuebleZona', N'U') IS NULL
    THROW 53911, 'No existe RSMAPS_InmuebleZona. Ejecutar primero Paso 38A.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Zona', N'U') IS NULL
    THROW 53912, 'No existe RSMAPS_Zona. Ejecutar primero Paso 38A.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_sp_ListaInmueblesAutorizados', N'P') IS NULL
    THROW 53913, 'No existe RSMAPS_sp_ListaInmueblesAutorizados. Ejecutar primero Paso 21.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_ListaInmueblesAutorizados
    @correo VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @MembresiasActivas INT;
    DECLARE @VerCuenta BIT = 0;
    DECLARE @VerPropio BIT = 0;

    SELECT @IdAsesor = u.idAsesor
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 52120, 'No existe un usuario RSMaps con el correo autenticado.', 1;

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
            THROW 52121, 'El usuario autenticado no pertenece a ninguna cuenta activa.', 1;
        ELSE
            THROW 52122, 'El usuario pertenece a varias cuentas y no tiene una predeterminada.', 1;
    END;

    SELECT @VerCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'INVENTARIO_VER_CUENTA'
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    SELECT @VerPropio = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = 'INVENTARIO_VER_PROPIO'
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @VerCuenta = 0 AND @VerPropio = 0
        THROW 52123, 'El rol actual no tiene permiso para consultar inventario.', 1;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idInmobiliaria,
        inm.nombre,
        i.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        i.direccion,
        i.lat,
        i.lng,
        i.idTipo,
        tp.nombre AS TipoNombre,
        i.telefono,
        i.terreno,
        i.construccion,
        i.precio,
        i.observaciones,
        i.exclusiva,
        i.link,
        i.contacto_a,
        ISNULL(img.Imagenes, 0) AS imagenes,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.FechaPublicacionUtc,
        i.FechaUltimoCambioEstadoUtc,
        zp.Codigo AS ZonaPrincipalCodigo,
        zp.Nombre AS ZonaPrincipalNombre,
        zonas.ZonasCsv
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
        ON inm.idInmobiliaria = i.idInmobiliaria
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp
        ON tp.idTipoPropiedad = i.idTipo
    OUTER APPLY
    (
        SELECT MAX(ii.Imagenes) AS Imagenes
        FROM dbo.RSMAPS_InmuebleImagenes ii
        WHERE ii.idInmueble = i.idInmueble
    ) img
    OUTER APPLY
    (
        SELECT TOP (1)
            z.Codigo,
            z.Nombre
        FROM dbo.RSMAPS_InmuebleZona iz
        INNER JOIN dbo.RSMAPS_Zona z
            ON z.IdZona = iz.IdZona
           AND z.Activa = 1
        WHERE iz.IdInmueble = i.idInmueble
          AND iz.EsPrincipal = 1
        ORDER BY z.Prioridad DESC, z.Nombre, z.IdZona
    ) zp
    OUTER APPLY
    (
        SELECT STRING_AGG(CONVERT(nvarchar(max), q.Nombre), N' · ') AS ZonasCsv
        FROM
        (
            SELECT DISTINCT z.Nombre, z.Prioridad, z.IdZona
            FROM dbo.RSMAPS_InmuebleZona iz
            INNER JOIN dbo.RSMAPS_Zona z
                ON z.IdZona = iz.IdZona
               AND z.Activa = 1
            WHERE iz.IdInmueble = i.idInmueble
        ) q
    ) zonas
    WHERE i.IdCuenta = @IdCuenta
      AND (@VerCuenta = 1 OR i.idAsesor = @IdAsesor)
    ORDER BY
        CASE i.EstadoCodigo
            WHEN 'PUBLICADO' THEN 10
            WHEN 'BORRADOR' THEN 20
            WHEN 'PAUSADO' THEN 30
            WHEN 'RETIRADO' THEN 40
            WHEN 'VENDIDO' THEN 50
            WHEN 'RENTADO' THEN 60
            ELSE 99
        END,
        i.idInmueble DESC;
END;
GO

/* ============================================================
   Prueba de lectura con el usuario principal de desarrollo.
   Solo lectura: no modifica datos.
   ============================================================ */
DECLARE @CorreoPrueba varchar(200) = 'profesor76@hotmail.com';

IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Usuario WHERE correo = @CorreoPrueba)
BEGIN
    CREATE TABLE #Inventario39A
    (
        idInmueble int,
        IdCuenta int,
        idInmobiliaria int NULL,
        nombre nvarchar(4000) NULL,
        idAsesor int,
        nombres nvarchar(4000) NULL,
        aPaterno nvarchar(4000) NULL,
        correo nvarchar(4000) NULL,
        direccion nvarchar(max) NULL,
        lat decimal(18,8) NULL,
        lng decimal(18,8) NULL,
        idTipo int NULL,
        TipoNombre nvarchar(4000) NULL,
        telefono nvarchar(max) NULL,
        terreno float NULL,
        construccion float NULL,
        precio float NULL,
        observaciones nvarchar(max) NULL,
        exclusiva int NULL,
        link nvarchar(max) NULL,
        contacto_a nvarchar(max) NULL,
        imagenes int,
        EstadoCodigo varchar(20) NULL,
        VisibilidadCodigo varchar(20) NULL,
        FechaPublicacionUtc datetime2 NULL,
        FechaUltimoCambioEstadoUtc datetime2 NULL,
        ZonaPrincipalCodigo varchar(60) NULL,
        ZonaPrincipalNombre nvarchar(120) NULL,
        ZonasCsv nvarchar(max) NULL
    );

    INSERT #Inventario39A
    EXEC dbo.RSMAPS_sp_ListaInmueblesAutorizados @correo = @CorreoPrueba;

    SELECT TOP (20)
        idInmueble,
        direccion,
        ZonaPrincipalCodigo,
        ZonaPrincipalNombre,
        ZonasCsv
    FROM #Inventario39A
    WHERE ZonaPrincipalCodigo IS NOT NULL OR ZonasCsv IS NOT NULL
    ORDER BY idInmueble;

    SELECT
        COUNT(*) AS InmueblesAutorizados,
        SUM(CASE WHEN ZonaPrincipalCodigo IS NOT NULL THEN 1 ELSE 0 END) AS ConZonaPrincipal,
        SUM(CASE WHEN ZonasCsv IS NOT NULL THEN 1 ELSE 0 END) AS ConAlgunaZona,
        'OK - PASO 39A INSTALADO' AS Estado
    FROM #Inventario39A;
END
ELSE
BEGIN
    SELECT 'OMITIDA - USUARIO DE PRUEBA NO DISPONIBLE' AS Estado;
END;
GO
