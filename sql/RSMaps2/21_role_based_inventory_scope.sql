/* ============================================================
   RSMaps 2.0 - Paso 21
   ALCANCE DE INVENTARIO BASADO EN ROL (RBAC - FASE 1)

   Objetivo:
   - Crear catalogo explicito de permisos.
   - Relacionar Rol -> Permiso.
   - Resolver desde SQL que inventario puede LEER un usuario autenticado.
   - Mantener minimo privilegio para ASESOR.
   - Permitir vista de equipo a CAPTURISTA / ADMINISTRADOR / PROPIETARIO.
   - No abrir todavia edicion/cierre de propiedades ajenas.

   Regla inicial de lectura:
   - ASESOR        -> INVENTARIO_VER_PROPIO
   - CAPTURISTA    -> INVENTARIO_VER_CUENTA (solo lectura por ahora)
   - ADMINISTRADOR -> INVENTARIO_VER_CUENTA
   - PROPIETARIO   -> INVENTARIO_VER_CUENTA

   IMPORTANTE:
   - PROPIETARIO es el codigo tecnico historico del rol; en UI se muestra
     como "Titular de cuenta" para evitar confusion con el propietario
     fisico de un inmueble.
   - Las pruebas cambian temporalmente un rol dentro de TRANSACTION y
     hacen ROLLBACK. No dejan privilegios adicionales persistentes.
   - El script es idempotente y puede volver a ejecutarse.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Rol', N'U') IS NULL
    THROW 52101, 'No existe dbo.RSMAPS_Rol. Ejecutar primero Paso 01.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    THROW 52102, 'No existe dbo.RSMAPS_CuentaUsuario. Ejecutar primero Paso 01.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
    THROW 52103, 'No existe dbo.RSMAPS_Inmueble.', 1;

/* ------------------------------------------------------------
   1. Catalogo de permisos
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_Permiso', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_Permiso
    (
        Codigo       varchar(50) NOT NULL,
        Nombre       nvarchar(120) NOT NULL,
        Descripcion  nvarchar(500) NULL,
        Activo       bit NOT NULL
            CONSTRAINT DF_RSMAPS_Permiso_Activo DEFAULT (1),
        CONSTRAINT PK_RSMAPS_Permiso PRIMARY KEY (Codigo)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INVENTARIO_VER_PROPIO')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('INVENTARIO_VER_PROPIO', N'Ver inventario propio',
            N'Permite consultar unicamente inmuebles cuyo asesor responsable es el usuario autenticado.');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INVENTARIO_VER_CUENTA')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('INVENTARIO_VER_CUENTA', N'Ver inventario de la cuenta',
            N'Permite consultar el inventario completo de la cuenta activa. No implica permiso para modificar inmuebles ajenos.');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_EDITAR_CUENTA')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('INMUEBLE_EDITAR_CUENTA', N'Editar inventario de la cuenta',
            N'Reservado para una fase posterior, cuando la auditoria distinga asesor responsable y usuario actor.');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_CAMBIAR_ESTADO_CUENTA')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('INMUEBLE_CAMBIAR_ESTADO_CUENTA', N'Cambiar estado de inventario de la cuenta',
            N'Reservado para una fase posterior con auditoria completa del usuario actor.');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'OPERACION_CERRAR_CUENTA')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('OPERACION_CERRAR_CUENTA', N'Cerrar operaciones de la cuenta',
            N'Reservado para una fase posterior; requiere conservar por separado asesor responsable y usuario que registra el cierre.');

IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Permiso WHERE Codigo = 'INMUEBLE_CAPTURAR_PARA_OTRO')
    INSERT dbo.RSMAPS_Permiso (Codigo, Nombre, Descripcion)
    VALUES ('INMUEBLE_CAPTURAR_PARA_OTRO', N'Capturar para otro asesor',
            N'Reservado para el futuro modelo de capturista con asesor responsable distinto del usuario creador.');

/* ------------------------------------------------------------
   2. Rol -> Permiso
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RSMAPS_RolPermiso', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RSMAPS_RolPermiso
    (
        RolCodigo      varchar(30) NOT NULL,
        PermisoCodigo  varchar(50) NOT NULL,
        CONSTRAINT PK_RSMAPS_RolPermiso PRIMARY KEY (RolCodigo, PermisoCodigo),
        CONSTRAINT FK_RSMAPS_RolPermiso_Rol
            FOREIGN KEY (RolCodigo) REFERENCES dbo.RSMAPS_Rol(Codigo),
        CONSTRAINT FK_RSMAPS_RolPermiso_Permiso
            FOREIGN KEY (PermisoCodigo) REFERENCES dbo.RSMAPS_Permiso(Codigo)
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ASESOR' AND PermisoCodigo = 'INVENTARIO_VER_PROPIO'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ASESOR', 'INVENTARIO_VER_PROPIO');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'CAPTURISTA' AND PermisoCodigo = 'INVENTARIO_VER_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('CAPTURISTA', 'INVENTARIO_VER_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'ADMINISTRADOR' AND PermisoCodigo = 'INVENTARIO_VER_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('ADMINISTRADOR', 'INVENTARIO_VER_CUENTA');

IF NOT EXISTS
(
    SELECT 1 FROM dbo.RSMAPS_RolPermiso
    WHERE RolCodigo = 'PROPIETARIO' AND PermisoCodigo = 'INVENTARIO_VER_CUENTA'
)
    INSERT dbo.RSMAPS_RolPermiso (RolCodigo, PermisoCodigo)
    VALUES ('PROPIETARIO', 'INVENTARIO_VER_CUENTA');

UPDATE dbo.RSMAPS_Rol
SET Nombre = N'Titular de cuenta',
    Descripcion = N'Titular de la cuenta con control administrativo principal.'
WHERE Codigo = 'PROPIETARIO';

/* ------------------------------------------------------------
   3. Lectura autorizada por identidad autenticada
   ------------------------------------------------------------ */
DECLARE @sql nvarchar(max) = N'
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
        THROW 52120, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

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
            THROW 52121, ''El usuario autenticado no pertenece a ninguna cuenta activa.'', 1;
        ELSE
            THROW 52122, ''El usuario pertenece a varias cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT @VerCuenta = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INVENTARIO_VER_CUENTA''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    SELECT @VerPropio = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INVENTARIO_VER_PROPIO''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @VerCuenta = 0 AND @VerPropio = 0
        THROW 52123, ''El rol actual no tiene permiso para consultar inventario.'', 1;

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
        i.FechaUltimoCambioEstadoUtc
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
    WHERE i.IdCuenta = @IdCuenta
      AND (@VerCuenta = 1 OR i.idAsesor = @IdAsesor)
    ORDER BY
        CASE i.EstadoCodigo
            WHEN ''PUBLICADO'' THEN 10
            WHEN ''BORRADOR'' THEN 20
            WHEN ''PAUSADO'' THEN 30
            WHEN ''RETIRADO'' THEN 40
            WHEN ''VENDIDO'' THEN 50
            WHEN ''RENTADO'' THEN 60
            ELSE 99
        END,
        i.idInmueble DESC;
END;
';

EXEC sys.sp_executesql @sql;

/* ============================================================
   4. Matriz resultante
   ============================================================ */
SELECT
    r.Codigo AS RolCodigo,
    r.Nombre AS Rol,
    p.Codigo AS PermisoCodigo,
    p.Nombre AS Permiso
FROM dbo.RSMAPS_Rol r
LEFT JOIN dbo.RSMAPS_RolPermiso rp ON rp.RolCodigo = r.Codigo
LEFT JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
ORDER BY r.Codigo, p.Codigo;

/* ============================================================
   5. Prueba controlada de alcance
   ============================================================ */
DECLARE @IdCuentaPrueba INT;
DECLARE @IdAsesorPrueba INT;
DECLARE @CorreoPrueba VARCHAR(200);
DECLARE @RolOriginal VARCHAR(30);
DECLARE @Propios INT;
DECLARE @CuentaTotal INT;
DECLARE @FilasAsesor INT;
DECLARE @FilasAdmin INT;

;WITH PropiosPorAsesor AS
(
    SELECT
        i.IdCuenta,
        i.idAsesor,
        COUNT(*) AS Propios
    FROM dbo.RSMAPS_Inmueble i
    GROUP BY i.IdCuenta, i.idAsesor
),
TotalPorCuenta AS
(
    SELECT
        i.IdCuenta,
        COUNT(*) AS CuentaTotal
    FROM dbo.RSMAPS_Inmueble i
    GROUP BY i.IdCuenta
)
SELECT TOP (1)
    @IdCuentaPrueba = cu.IdCuenta,
    @IdAsesorPrueba = cu.IdAsesor,
    @CorreoPrueba = u.correo,
    @RolOriginal = cu.RolCodigo,
    @Propios = pa.Propios,
    @CuentaTotal = tc.CuentaTotal
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = cu.IdAsesor
INNER JOIN PropiosPorAsesor pa
    ON pa.IdCuenta = cu.IdCuenta
   AND pa.idAsesor = cu.IdAsesor
INNER JOIN TotalPorCuenta tc
    ON tc.IdCuenta = cu.IdCuenta
WHERE cu.Activo = 1
  AND cu.RolCodigo = 'ASESOR'
  AND u.correo IS NOT NULL
  AND pa.Propios > 0
  AND tc.CuentaTotal > pa.Propios
ORDER BY pa.Propios DESC, cu.IdAsesor;

IF @IdAsesorPrueba IS NULL
    THROW 52140, 'No se encontro un asesor adecuado para probar alcance por rol.', 1;

SELECT TOP (0)
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
    i.FechaUltimoCambioEstadoUtc
INTO #InventarioAutorizado
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
LEFT JOIN dbo.RSMAPS_Inmobiliaria inm ON inm.idInmobiliaria = i.idInmobiliaria
LEFT JOIN dbo.RSMAPS_TipoPropiedades tp ON tp.idTipoPropiedad = i.idTipo
OUTER APPLY
(
    SELECT MAX(ii.Imagenes) AS Imagenes
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = i.idInmueble
) img;

INSERT #InventarioAutorizado
EXEC dbo.RSMAPS_sp_ListaInmueblesAutorizados @correo = @CorreoPrueba;

SELECT @FilasAsesor = COUNT(*) FROM #InventarioAutorizado;

SELECT
    @IdAsesorPrueba AS IdAsesor,
    @CorreoPrueba AS Correo,
    @RolOriginal AS RolActual,
    @Propios AS InmueblesPropios,
    @CuentaTotal AS InmueblesCuenta,
    @FilasAsesor AS FilasAutorizadas,
    CASE WHEN @FilasAsesor = @Propios
         THEN 'OK - ASESOR VE SOLO INVENTARIO PROPIO'
         ELSE 'REVISAR'
    END AS EstadoAsesor;

TRUNCATE TABLE #InventarioAutorizado;

BEGIN TRANSACTION;

UPDATE dbo.RSMAPS_CuentaUsuario
SET RolCodigo = 'ADMINISTRADOR'
WHERE IdCuenta = @IdCuentaPrueba
  AND IdAsesor = @IdAsesorPrueba;

INSERT #InventarioAutorizado
EXEC dbo.RSMAPS_sp_ListaInmueblesAutorizados @correo = @CorreoPrueba;

SELECT @FilasAdmin = COUNT(*) FROM #InventarioAutorizado;

SELECT
    @IdAsesorPrueba AS IdAsesor,
    @CorreoPrueba AS Correo,
    'ADMINISTRADOR (TEMPORAL)' AS RolDurantePrueba,
    @CuentaTotal AS InmueblesCuenta,
    @FilasAdmin AS FilasAutorizadas,
    CASE WHEN @FilasAdmin = @CuentaTotal
         THEN 'OK - ADMINISTRADOR VE INVENTARIO DE CUENTA'
         ELSE 'REVISAR'
    END AS EstadoAdministrador;

ROLLBACK TRANSACTION;

SELECT
    cu.IdCuenta,
    cu.IdAsesor,
    cu.RolCodigo AS RolDespuesRollback,
    CASE WHEN cu.RolCodigo = @RolOriginal
         THEN 'OK - ROL ORIGINAL INTACTO'
         ELSE 'REVISAR'
    END AS EstadoRollback
FROM dbo.RSMAPS_CuentaUsuario cu
WHERE cu.IdCuenta = @IdCuentaPrueba
  AND cu.IdAsesor = @IdAsesorPrueba;

DROP TABLE #InventarioAutorizado;

PRINT 'Paso 21 RSMaps 2.0 terminado correctamente.';