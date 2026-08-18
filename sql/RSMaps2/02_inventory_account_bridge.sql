/* ============================================================
   RSMaps 2.0 - Paso 02
   PUENTE INVENTARIO -> CUENTA

   Base esperada: mapsMarkers

   Objetivo:
   - Agregar IdCuenta a RSMAPS_Inmueble.
   - Asociar el inventario activo actual con RSMAPS_Cuenta usando
     la relación legacy idInmobiliaria -> IdInmobiliariaLegacy.
   - Crear FK e índice para empezar a formalizar el modelo multi-tenant.

   IMPORTANTE:
   - NO elimina ni renombra idInmobiliaria.
   - NO modifica procedimientos almacenados todavía.
   - NO modifica el código .NET.
   - IdCuenta permanece NULLABLE temporalmente porque el procedimiento
     legacy de alta todavía no envía/escribe IdCuenta. Esto se corregirá
     en el siguiente paso antes de hacer la columna obligatoria.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Validar prerrequisitos del Paso 01
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
        THROW 50201, 'No existe dbo.RSMAPS_Cuenta. Ejecutar primero el Paso 01.', 1;

    IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
        THROW 50202, 'No existe dbo.RSMAPS_Inmueble.', 1;

    /* ------------------------------------------------------------
       2. Agregar IdCuenta de forma compatible
       ------------------------------------------------------------ */
    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'IdCuenta') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble
            ADD IdCuenta int NULL;
    END;

    /* ------------------------------------------------------------
       3. Migrar las propiedades existentes
       ------------------------------------------------------------ */
    UPDATE i
       SET i.IdCuenta = c.IdCuenta
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdInmobiliariaLegacy = i.idInmobiliaria
    WHERE i.IdCuenta IS NULL;

    /* ------------------------------------------------------------
       4. No continuar si alguna propiedad actual no pudo mapearse
       ------------------------------------------------------------ */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        WHERE i.IdCuenta IS NULL
    )
    BEGIN
        THROW 50203, 'Existen inmuebles activos sin una Cuenta mapeada. Revisar antes de continuar.', 1;
    END;

    /* ------------------------------------------------------------
       5. Crear relación formal Cuenta -> Inmueble
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_Cuenta'
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_Cuenta
            FOREIGN KEY (IdCuenta)
            REFERENCES dbo.RSMAPS_Cuenta(IdCuenta);

        ALTER TABLE dbo.RSMAPS_Inmueble
            CHECK CONSTRAINT FK_RSMAPS_Inmueble_Cuenta;
    END;

    /* ------------------------------------------------------------
       6. Índice para filtros por cuenta
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IX_RSMAPS_Inmueble_IdCuenta'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_Inmueble_IdCuenta
            ON dbo.RSMAPS_Inmueble(IdCuenta);
    END;

    COMMIT TRANSACTION;

    /* ------------------------------------------------------------
       7. Validación final
       ------------------------------------------------------------ */
    SELECT
        COUNT(*) AS TotalInmuebles,
        SUM(CASE WHEN IdCuenta IS NOT NULL THEN 1 ELSE 0 END) AS ConCuenta,
        SUM(CASE WHEN IdCuenta IS NULL THEN 1 ELSE 0 END) AS SinCuenta
    FROM dbo.RSMAPS_Inmueble;

    SELECT
        c.IdCuenta,
        c.Nombre AS Cuenta,
        c.TipoCuenta,
        COUNT(i.idInmueble) AS PropiedadesActivas
    FROM dbo.RSMAPS_Cuenta c
    LEFT JOIN dbo.RSMAPS_Inmueble i
        ON i.IdCuenta = c.IdCuenta
    GROUP BY c.IdCuenta, c.Nombre, c.TipoCuenta
    ORDER BY c.IdCuenta;

    SELECT
        i.idInmueble,
        i.IdCuenta,
        c.Nombre AS Cuenta,
        i.idInmobiliaria AS IdInmobiliariaLegacy,
        i.idAsesor,
        i.precio
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = i.IdCuenta
    ORDER BY i.idInmueble;

    PRINT 'Paso 02 RSMaps 2.0 terminado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
