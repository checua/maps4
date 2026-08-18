/* ============================================================
   RSMaps 2.0 - Paso 06
   ENDURECER INTEGRIDAD DEL INVENTARIO ACTIVO

   Base esperada: mapsMarkers

   Objetivo:
   - Hacer RSMAPS_Inmueble.IdCuenta obligatorio ahora que el flujo
     de escritura ya lo resuelve desde RSMAPS_CuentaUsuario.
   - Formalizar relaciones del inventario activo con Cuenta, Usuario,
     TipoPropiedad e Inmobiliaria legacy.
   - Crear indices utiles para filtros privados por cuenta/asesor/tipo.

   IMPORTANTE:
   - NO modifica datos funcionales.
   - NO toca tablas historicas (_0 / InmuebleVendido).
   - NO toca RSMAPS_InmuebleImagenes porque existen registros huerfanos
     que deben clasificarse antes de agregar su FK.
   - idInmobiliaria permanece nullable porque una Cuenta INDIVIDUAL
     futura puede no tener IdInmobiliariaLegacy.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50600, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------
       1. Prerrequisitos
       ------------------------------------------------------------ */
    IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL
        THROW 50601, 'No existe dbo.RSMAPS_Inmueble.', 1;

    IF OBJECT_ID(N'dbo.RSMAPS_Cuenta', N'U') IS NULL
        THROW 50602, 'No existe dbo.RSMAPS_Cuenta. Ejecutar primero Paso 01.', 1;

    IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'IdCuenta') IS NULL
        THROW 50603, 'RSMAPS_Inmueble no tiene IdCuenta. Ejecutar primero Paso 02.', 1;

    /* ------------------------------------------------------------
       2. Validar integridad antes de crear restricciones
       ------------------------------------------------------------ */
    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE IdCuenta IS NULL)
        THROW 50610, 'Hay inmuebles activos sin IdCuenta.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        LEFT JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = i.IdCuenta
        WHERE c.IdCuenta IS NULL
    )
        THROW 50611, 'Hay inmuebles con una Cuenta inexistente.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        LEFT JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
        WHERE i.idAsesor IS NOT NULL
          AND u.idAsesor IS NULL
    )
        THROW 50612, 'Hay inmuebles con un asesor inexistente.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        LEFT JOIN dbo.RSMAPS_TipoPropiedades t
            ON t.idTipoPropiedad = i.idTipo
        WHERE i.idTipo IS NOT NULL
          AND t.idTipoPropiedad IS NULL
    )
        THROW 50613, 'Hay inmuebles con un tipo de propiedad inexistente.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_Inmueble i
        LEFT JOIN dbo.RSMAPS_Inmobiliaria inm
            ON inm.idInmobiliaria = i.idInmobiliaria
        WHERE i.idInmobiliaria IS NOT NULL
          AND inm.idInmobiliaria IS NULL
    )
        THROW 50614, 'Hay inmuebles con una inmobiliaria legacy inexistente.', 1;

    /* ------------------------------------------------------------
       3. IdCuenta ya puede ser obligatorio
       ------------------------------------------------------------ */
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IdCuenta'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble
            ALTER COLUMN IdCuenta int NOT NULL;
    END;

    /* ------------------------------------------------------------
       4. Garantizar claves candidatas para relaciones legacy
       Paso 01 ya creo UX_RSMAPS_Usuario_idAsesor.
       RSMAPS_Inmobiliaria no tiene PK, asi que usamos UNIQUE INDEX.
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Usuario')
          AND name = N'UX_RSMAPS_Usuario_idAsesor'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_Usuario_idAsesor
            ON dbo.RSMAPS_Usuario(idAsesor);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmobiliaria')
          AND name = N'UX_RSMAPS_Inmobiliaria_idInmobiliaria'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_RSMAPS_Inmobiliaria_idInmobiliaria
            ON dbo.RSMAPS_Inmobiliaria(idInmobiliaria);
    END;

    /* ------------------------------------------------------------
       5. Relaciones formales del inventario activo
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_Cuenta'
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_Cuenta
            FOREIGN KEY (IdCuenta)
            REFERENCES dbo.RSMAPS_Cuenta(IdCuenta);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_Usuario'
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_Usuario
            FOREIGN KEY (idAsesor)
            REFERENCES dbo.RSMAPS_Usuario(idAsesor);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_TipoPropiedad'
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_TipoPropiedad
            FOREIGN KEY (idTipo)
            REFERENCES dbo.RSMAPS_TipoPropiedades(idTipoPropiedad);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'FK_RSMAPS_Inmueble_InmobiliariaLegacy'
    )
    BEGIN
        ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK
            ADD CONSTRAINT FK_RSMAPS_Inmueble_InmobiliariaLegacy
            FOREIGN KEY (idInmobiliaria)
            REFERENCES dbo.RSMAPS_Inmobiliaria(idInmobiliaria);
    END;

    /* Asegurar que las restricciones existentes/nuevas queden confiables. */
    ALTER TABLE dbo.RSMAPS_Inmueble CHECK CONSTRAINT ALL;

    /* ------------------------------------------------------------
       6. Indices para inventario privado y filtros frecuentes
       ------------------------------------------------------------ */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IX_RSMAPS_Inmueble_IdCuenta_IdAsesor'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_Inmueble_IdCuenta_IdAsesor
            ON dbo.RSMAPS_Inmueble(IdCuenta, idAsesor);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
          AND name = N'IX_RSMAPS_Inmueble_IdCuenta_IdTipo'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_Inmueble_IdCuenta_IdTipo
            ON dbo.RSMAPS_Inmueble(IdCuenta, idTipo);
    END;

    COMMIT TRANSACTION;

    /* ------------------------------------------------------------
       7. Validacion final
       ------------------------------------------------------------ */
    SELECT
        COUNT(*) AS TotalInmuebles,
        SUM(CASE WHEN IdCuenta IS NOT NULL THEN 1 ELSE 0 END) AS ConCuenta,
        SUM(CASE WHEN IdCuenta IS NULL THEN 1 ELSE 0 END) AS SinCuenta
    FROM dbo.RSMAPS_Inmueble;

    SELECT
        c.name AS Columna,
        TYPE_NAME(c.user_type_id) AS TipoDato,
        c.is_nullable AS PermiteNull
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
      AND c.name = N'IdCuenta';

    SELECT
        fk.name AS ForeignKey,
        fk.is_disabled AS Deshabilitada,
        fk.is_not_trusted AS NoConfiable
    FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
    ORDER BY fk.name;

    SELECT
        i.name AS Indice,
        i.is_unique AS EsUnico
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.RSMAPS_Inmueble')
      AND i.name IN
      (
          N'IX_RSMAPS_Inmueble_IdCuenta',
          N'IX_RSMAPS_Inmueble_IdCuenta_IdAsesor',
          N'IX_RSMAPS_Inmueble_IdCuenta_IdTipo'
      )
    ORDER BY i.name;

    PRINT 'Paso 06 RSMaps 2.0 terminado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
