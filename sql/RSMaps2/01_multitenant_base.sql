/* ============================================================
   RSMaps 2.0 - Paso 01
   BASE MULTI-TENANT: CUENTAS, ROLES Y MEMBRESÍAS

   Base esperada: mapsMarkers

   Objetivo:
   - Crear el concepto de Cuenta para soportar asesores independientes
     e inmobiliarias con múltiples usuarios.
   - Crear catálogo de roles.
   - Crear relación Cuenta <-> Usuario.
   - Migrar DNHoldings Group como primera cuenta tipo INMOBILIARIA.
   - Vincular únicamente los usuarios actuales con idInmobiliaria = 1.

   IMPORTANTE:
   - NO modifica RSMAPS_Inmueble.
   - NO elimina usuarios.
   - Los 4 usuarios sin inmobiliaria quedan sin asignar por ahora.
   - El script está pensado para ejecutarse una sola vez. Incluye
     validaciones para evitar duplicar estructuras/datos.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Validaciones previas
       ------------------------------------------------------------ */
    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmobiliaria WHERE idInmobiliaria = 1)
    BEGIN
        THROW 50101, 'No existe la inmobiliaria legacy idInmobiliaria = 1.', 1;
    END;

    IF EXISTS (
        SELECT idAsesor
        FROM dbo.RSMAPS_Usuario
        GROUP BY idAsesor
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 50102, 'Existen idAsesor duplicados en RSMAPS_Usuario. Revisar antes de continuar.', 1;
    END;

    /* ------------------------------------------------------------
       2. Garantizar unicidad referenciable de RSMAPS_Usuario.idAsesor
       RSMAPS_Usuario actualmente no tiene PK sobre idAsesor.
       Un índice UNIQUE permite utilizarlo como destino de FK sin
       alterar todavía la PK/estructura histórica de la tabla.
       ------------------------------------------------------------ */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Usuario')
          AND name = N'UX_RSMAPS_Usuario_idAsesor'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_Usuario_idAsesor
            ON dbo.RSMAPS_Usuario(idAsesor);
    END;

    /* ------------------------------------------------------------
       3. Crear RSMAPS_Cuenta
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_Cuenta
        (
            IdCuenta              int IDENTITY(1,1) NOT NULL,
            Nombre                nvarchar(200) NOT NULL,
            TipoCuenta            varchar(20) NOT NULL,
            Slug                  varchar(200) NULL,
            IdInmobiliariaLegacy  int NULL,
            Activo                bit NOT NULL CONSTRAINT DF_RSMAPS_Cuenta_Activo DEFAULT (1),
            FechaAlta             datetime2(0) NOT NULL CONSTRAINT DF_RSMAPS_Cuenta_FechaAlta DEFAULT (SYSUTCDATETIME()),
            FechaActualizacion    datetime2(0) NOT NULL CONSTRAINT DF_RSMAPS_Cuenta_FechaActualizacion DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_Cuenta PRIMARY KEY (IdCuenta),
            CONSTRAINT CK_RSMAPS_Cuenta_TipoCuenta
                CHECK (TipoCuenta IN ('INDIVIDUAL', 'INMOBILIARIA'))
        );

        CREATE UNIQUE INDEX UX_RSMAPS_Cuenta_Slug
            ON dbo.RSMAPS_Cuenta(Slug)
            WHERE Slug IS NOT NULL;

        CREATE UNIQUE INDEX UX_RSMAPS_Cuenta_IdInmobiliariaLegacy
            ON dbo.RSMAPS_Cuenta(IdInmobiliariaLegacy)
            WHERE IdInmobiliariaLegacy IS NOT NULL;
    END;

    /* ------------------------------------------------------------
       4. Crear catálogo de roles
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_Rol', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_Rol
        (
            Codigo       varchar(30) NOT NULL,
            Nombre       nvarchar(100) NOT NULL,
            Descripcion  nvarchar(300) NULL,
            Activo       bit NOT NULL CONSTRAINT DF_RSMAPS_Rol_Activo DEFAULT (1),

            CONSTRAINT PK_RSMAPS_Rol PRIMARY KEY (Codigo)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'PROPIETARIO')
        INSERT dbo.RSMAPS_Rol (Codigo, Nombre, Descripcion)
        VALUES ('PROPIETARIO', N'Propietario', N'Dueño de la cuenta con control administrativo principal.');

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'ADMINISTRADOR')
        INSERT dbo.RSMAPS_Rol (Codigo, Nombre, Descripcion)
        VALUES ('ADMINISTRADOR', N'Administrador', N'Administra usuarios, inventario y configuración de la cuenta.');

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'ASESOR')
        INSERT dbo.RSMAPS_Rol (Codigo, Nombre, Descripcion)
        VALUES ('ASESOR', N'Asesor', N'Gestiona y publica las propiedades que le correspondan.');

    IF NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Rol WHERE Codigo = 'CAPTURISTA')
        INSERT dbo.RSMAPS_Rol (Codigo, Nombre, Descripcion)
        VALUES ('CAPTURISTA', N'Capturista', N'Apoya en el alta y mantenimiento de inventario sin control administrativo completo.');

    /* ------------------------------------------------------------
       5. Crear RSMAPS_CuentaUsuario
       Un mismo usuario podrá pertenecer a distintas cuentas en el futuro.
       Por ahora cada membresía tiene un rol principal.
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_CuentaUsuario', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_CuentaUsuario
        (
            IdCuenta       int NOT NULL,
            IdAsesor       int NOT NULL,
            RolCodigo      varchar(30) NOT NULL,
            Activo         bit NOT NULL CONSTRAINT DF_RSMAPS_CuentaUsuario_Activo DEFAULT (1),
            EsPredeterminada bit NOT NULL CONSTRAINT DF_RSMAPS_CuentaUsuario_EsPredeterminada DEFAULT (0),
            FechaAlta      datetime2(0) NOT NULL CONSTRAINT DF_RSMAPS_CuentaUsuario_FechaAlta DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_RSMAPS_CuentaUsuario PRIMARY KEY (IdCuenta, IdAsesor),
            CONSTRAINT FK_RSMAPS_CuentaUsuario_Cuenta
                FOREIGN KEY (IdCuenta) REFERENCES dbo.RSMAPS_Cuenta(IdCuenta),
            CONSTRAINT FK_RSMAPS_CuentaUsuario_Usuario
                FOREIGN KEY (IdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
            CONSTRAINT FK_RSMAPS_CuentaUsuario_Rol
                FOREIGN KEY (RolCodigo) REFERENCES dbo.RSMAPS_Rol(Codigo)
        );

        CREATE INDEX IX_RSMAPS_CuentaUsuario_IdAsesor
            ON dbo.RSMAPS_CuentaUsuario(IdAsesor);
    END;

    /* ------------------------------------------------------------
       6. Migrar DNHoldings Group como primera Cuenta
       ------------------------------------------------------------ */
    DECLARE @IdCuentaDN int;

    SELECT @IdCuentaDN = IdCuenta
    FROM dbo.RSMAPS_Cuenta
    WHERE IdInmobiliariaLegacy = 1;

    IF @IdCuentaDN IS NULL
    BEGIN
        INSERT dbo.RSMAPS_Cuenta
        (
            Nombre,
            TipoCuenta,
            Slug,
            IdInmobiliariaLegacy,
            Activo
        )
        SELECT
            CAST(nombre AS nvarchar(200)),
            'INMOBILIARIA',
            'dnholdings-group',
            idInmobiliaria,
            1
        FROM dbo.RSMAPS_Inmobiliaria
        WHERE idInmobiliaria = 1;

        SET @IdCuentaDN = SCOPE_IDENTITY();
    END;

    /* ------------------------------------------------------------
       7. Vincular los 46 usuarios actuales de DNHoldings
       Todos se migran inicialmente como ASESOR para no asumir
       privilegios administrativos que todavía no hemos definido.
       ------------------------------------------------------------ */
    INSERT dbo.RSMAPS_CuentaUsuario
    (
        IdCuenta,
        IdAsesor,
        RolCodigo,
        Activo,
        EsPredeterminada
    )
    SELECT
        @IdCuentaDN,
        u.idAsesor,
        'ASESOR',
        1,
        1
    FROM dbo.RSMAPS_Usuario u
    WHERE u.idInmobiliaria = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RSMAPS_CuentaUsuario cu
          WHERE cu.IdCuenta = @IdCuentaDN
            AND cu.IdAsesor = u.idAsesor
      );

    COMMIT TRANSACTION;

    /* ------------------------------------------------------------
       8. Validación final
       ------------------------------------------------------------ */
    SELECT
        c.IdCuenta,
        c.Nombre,
        c.TipoCuenta,
        c.Slug,
        c.IdInmobiliariaLegacy,
        c.Activo
    FROM dbo.RSMAPS_Cuenta c
    ORDER BY c.IdCuenta;

    SELECT
        c.IdCuenta,
        c.Nombre AS Cuenta,
        COUNT(cu.IdAsesor) AS UsuariosVinculados
    FROM dbo.RSMAPS_Cuenta c
    LEFT JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdCuenta = c.IdCuenta
    GROUP BY c.IdCuenta, c.Nombre
    ORDER BY c.IdCuenta;

    SELECT
        cu.IdCuenta,
        cu.IdAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        cu.RolCodigo,
        cu.Activo,
        cu.EsPredeterminada
    FROM dbo.RSMAPS_CuentaUsuario cu
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = cu.IdAsesor
    ORDER BY cu.IdCuenta, u.nombres, u.aPaterno;

    SELECT
        u.idAsesor,
        u.nombres,
        u.aPaterno,
        u.correo,
        u.idInmobiliaria,
        CASE WHEN cu.IdAsesor IS NULL THEN 'SIN CUENTA' ELSE 'VINCULADO' END AS EstadoMigracion
    FROM dbo.RSMAPS_Usuario u
    LEFT JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdAsesor = u.idAsesor
    ORDER BY EstadoMigracion, u.idAsesor;

    PRINT 'Paso 01 RSMaps 2.0 terminado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
