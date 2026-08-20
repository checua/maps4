/* ============================================================
   RSMaps 2.0 - Paso 31
   CORREGIR NOTAS PRIVADAS INICIALES Y LIMPIAR BORRADORES DE PRUEBA DUPLICADOS

   Objetivo:
   - Corregir RSMAPS_sp_CrearBorradorInmueble para que contacto_a nazca NULL.
     El telefono del asesor ya vive en la columna telefono y no debe convertirse
     accidentalmente en una nota privada.
   - Limpiar SOLO el valor automatico de telefono migrado a NotasPrivadas en
     los borradores de prueba 176, 178 y 179, cuando siga coincidiendo exactamente
     con el telefono del asesor y no exista edicion posterior.
   - Conservar el borrador 176 como borrador de desarrollo.
   - Eliminar de forma segura SOLO los borradores vacios 178 y 179.
   - No tocar notas privadas/contacto_a de propiedades reales o legacy.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53100, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF OBJECT_ID(N'dbo.RSMAPS_sp_CrearBorradorInmueble', N'P') IS NULL
    THROW 53101, 'No existe RSMAPS_sp_CrearBorradorInmueble. Ejecutar primero Paso 27.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'NotasPrivadas') IS NULL
    THROW 53102, 'No existe NotasPrivadas. Ejecutar primero Paso 29/30.', 1;

/* ============================================================
   1. Reinstalar el alta moderna: telefono separado, contacto_a = NULL
   ============================================================ */
DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.RSMAPS_sp_CrearBorradorInmueble
    @correo VARCHAR(200),
    @lat DECIMAL(10,6),
    @lng DECIMAL(10,6),
    @idTipo INT,
    @idInmueble INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdAsesor INT;
    DECLARE @IdCuenta INT;
    DECLARE @RolCodigo VARCHAR(30);
    DECLARE @IdInmobiliariaLegacy INT;
    DECLARE @Telefono VARCHAR(MAX);
    DECLARE @MembresiasActivas INT;
    DECLARE @PuedeCrear BIT = 0;
    DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();

    SET @idInmueble = NULL;

    IF @lat IS NULL OR @lat < -90 OR @lat > 90
        THROW 52720, ''La latitud del borrador no es valida.'', 1;
    IF @lng IS NULL OR @lng < -180 OR @lng > 180
        THROW 52721, ''La longitud del borrador no es valida.'', 1;

    IF @idTipo IS NULL OR NOT EXISTS
    (
        SELECT 1 FROM dbo.RSMAPS_TipoPropiedades WHERE idTipoPropiedad = @idTipo
    )
        THROW 52722, ''El tipo de propiedad seleccionado no existe.'', 1;

    SELECT @IdAsesor = u.idAsesor, @Telefono = u.telefono
    FROM dbo.RSMAPS_Usuario u
    WHERE u.correo = @correo;

    IF @IdAsesor IS NULL
        THROW 52723, ''No existe un usuario RSMaps con el correo autenticado.'', 1;

    SELECT TOP (1)
        @IdCuenta = cu.IdCuenta,
        @RolCodigo = cu.RolCodigo,
        @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
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
        WHERE cu.IdAsesor = @IdAsesor AND cu.Activo = 1 AND c.Activo = 1;

        IF @MembresiasActivas = 1
        BEGIN
            SELECT TOP (1)
                @IdCuenta = cu.IdCuenta,
                @RolCodigo = cu.RolCodigo,
                @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
            FROM dbo.RSMAPS_CuentaUsuario cu
            INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
            WHERE cu.IdAsesor = @IdAsesor AND cu.Activo = 1 AND c.Activo = 1
            ORDER BY cu.IdCuenta;
        END
        ELSE IF @MembresiasActivas = 0
            THROW 52724, ''El usuario autenticado no pertenece a ninguna cuenta activa.'', 1;
        ELSE
            THROW 52725, ''El usuario pertenece a varias cuentas y no tiene una predeterminada.'', 1;
    END;

    SELECT @PuedeCrear = CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RSMAPS_RolPermiso rp
        INNER JOIN dbo.RSMAPS_Permiso p ON p.Codigo = rp.PermisoCodigo
        WHERE rp.RolCodigo = @RolCodigo
          AND rp.PermisoCodigo = ''INMUEBLE_CREAR_PROPIO''
          AND p.Activo = 1
    ) THEN 1 ELSE 0 END;

    IF @PuedeCrear = 0
        THROW 52726, ''El rol actual no tiene permiso para crear borradores propios.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.RSMAPS_Inmueble
        (
            idInmobiliaria, idAsesor, direccion, lat, lng, idTipo, telefono,
            terreno, construccion, precio, observaciones, exclusiva, link,
            contacto_a, IdCuenta, EstadoCodigo, VisibilidadCodigo,
            FechaPublicacionUtc, FechaUltimoCambioEstadoUtc
        )
        VALUES
        (
            @IdInmobiliariaLegacy, @IdAsesor, ''Ubicacion registrada en mapa'',
            @lat, @lng, @idTipo, @Telefono,
            0, 0, 0, NULL, 1, ''BORRADOR'',
            NULL, @IdCuenta, ''BORRADOR'', ''CUENTA'', NULL, @AhoraUtc
        );

        SET @idInmueble = CONVERT(INT, SCOPE_IDENTITY());

        INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
        VALUES (@idInmueble, 0);

        INSERT dbo.RSMAPS_InmuebleCambioEstado
        (
            IdInmueble, IdCuenta, EstadoAnterior, EstadoNuevo,
            VisibilidadAnterior, VisibilidadNueva,
            IdAsesorResponsable, IdAsesorCambio,
            FechaCambioUtc, Motivo, Origen
        )
        VALUES
        (
            @idInmueble, @IdCuenta, NULL, ''BORRADOR'', NULL, ''CUENTA'',
            @IdAsesor, @IdAsesor, @AhoraUtc,
            N''Borrador creado desde el flujo moderno de captura.'', ''APLICACION''
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT i.idInmueble, i.IdCuenta, i.idAsesor, i.lat, i.lng, i.idTipo,
           i.precio, i.EstadoCodigo, i.VisibilidadCodigo,
           i.FechaPublicacionUtc, i.FechaUltimoCambioEstadoUtc
    FROM dbo.RSMAPS_Inmueble i
    WHERE i.idInmueble = @idInmueble;
END;';

EXEC sys.sp_executesql @sql;

/* ============================================================
   2. Mostrar exactamente el valor automatico detectado antes de limpiar
   ============================================================ */
SELECT
    i.idInmueble,
    i.idAsesor,
    u.telefono AS TelefonoAsesor,
    CONVERT(varchar(max), i.contacto_a) AS ContactoLegacy,
    i.NotasPrivadas,
    i.FechaUltimaEdicionUtc,
    CASE
        WHEN i.EstadoCodigo = 'BORRADOR'
         AND i.VisibilidadCodigo = 'CUENTA'
         AND i.FechaPublicacionUtc IS NULL
         AND i.FechaUltimaEdicionUtc IS NULL
         AND CONVERT(varchar(max), i.contacto_a) = u.telefono
         AND i.NotasPrivadas = u.telefono
        THEN 'AUTOMATICO - APTO PARA LIMPIAR'
        ELSE 'REVISAR - NO SE TOCARA'
    END AS EstadoNota
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
WHERE i.idInmueble IN (176,178,179)
ORDER BY i.idInmueble;

/* ============================================================
   3. Limpiar SOLO el telefono automatico en los borradores de prueba
   ============================================================ */
UPDATE i
SET contacto_a = NULL,
    NotasPrivadas = NULL
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
WHERE i.idInmueble IN (176,178,179)
  AND i.EstadoCodigo = 'BORRADOR'
  AND i.VisibilidadCodigo = 'CUENTA'
  AND i.FechaPublicacionUtc IS NULL
  AND i.FechaUltimaEdicionUtc IS NULL
  AND CONVERT(varchar(max), i.contacto_a) = u.telefono
  AND i.NotasPrivadas = u.telefono;

/* ============================================================
   4. Validar que 178 y 179 sigan siendo borradores vacios de prueba
   ============================================================ */
DECLARE @Borrar TABLE (IdInmueble int PRIMARY KEY);
INSERT @Borrar (IdInmueble) VALUES (178),(179);

IF EXISTS
(
    SELECT 1
    FROM @Borrar b
    LEFT JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble = b.IdInmueble
    WHERE i.idInmueble IS NULL
       OR i.IdCuenta <> 1
       OR i.idAsesor <> 1
       OR i.EstadoCodigo <> 'BORRADOR'
       OR i.VisibilidadCodigo <> 'CUENTA'
       OR ISNULL(i.precio,0) <> 0
       OR ISNULL(i.terreno,0) <> 0
       OR ISNULL(i.construccion,0) <> 0
       OR i.FechaPublicacionUtc IS NOT NULL
       OR i.FechaUltimaEdicionUtc IS NOT NULL
       OR ISNULL(CONVERT(varchar(max), i.observaciones),'') <> ''
       OR ISNULL(CONVERT(varchar(max), i.contacto_a),'') <> ''
       OR ISNULL(i.NotasPrivadas,N'') <> N''
)
    THROW 53110, 'Seguridad: 178 o 179 ya no es un borrador vacio esperado. No se borro ninguno.', 1;

IF EXISTS
(
    SELECT 1 FROM @Borrar b
    WHERE EXISTS (SELECT 1 FROM dbo.RSMAPS_OperacionInmueble o WHERE o.IdInmueble = b.IdInmueble)
       OR EXISTS (SELECT 1 FROM dbo.RSMAPS_InmueblePrecioHistorial p WHERE p.IdInmueble = b.IdInmueble)
       OR ISNULL((SELECT MAX(ii.Imagenes) FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble = b.IdInmueble),0) <> 0
       OR (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado h WHERE h.IdInmueble = b.IdInmueble) <> 1
)
    THROW 53111, 'Seguridad: 178 o 179 tiene actividad adicional. No se borro ninguno.', 1;

/* ============================================================
   5. Eliminar 178 y 179 juntos dentro de una sola transaccion
   ============================================================ */
BEGIN TRY
    BEGIN TRANSACTION;

    DELETE h
    FROM dbo.RSMAPS_InmuebleCambioEstado h
    INNER JOIN @Borrar b ON b.IdInmueble = h.IdInmueble;

    DELETE ii
    FROM dbo.RSMAPS_InmuebleImagenes ii
    INNER JOIN @Borrar b ON b.IdInmueble = ii.idInmueble;

    DELETE i
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN @Borrar b ON b.IdInmueble = i.idInmueble;

    IF @@ROWCOUNT <> 2
        THROW 53112, 'Seguridad: no se eliminaron exactamente los dos borradores esperados.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* ============================================================
   6. Estado final: conservar 176 y confirmar limpieza
   ============================================================ */
SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    i.precio,
    i.FechaUltimaEdicionUtc,
    CONVERT(varchar(max), i.contacto_a) AS ContactoLegacy,
    i.NotasPrivadas,
    'OK - BORRADOR 176 CONSERVADO' AS Estado176
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = 176;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE idInmueble = 178) AS Borrador178Restante,
    (SELECT COUNT(*) FROM dbo.RSMAPS_Inmueble WHERE idInmueble = 179) AS Borrador179Restante,
    CASE
        WHEN EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble = 176 AND EstadoCodigo = 'BORRADOR')
         AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble IN (178,179))
        THEN 'OK - 176 CONSERVADO Y DUPLICADOS 178/179 LIMPIADOS'
        ELSE 'REVISAR'
    END AS EstadoFinal;

SELECT
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.RSMAPS_sp_CrearBorradorInmueble')) LIKE '%NULL, @IdCuenta, ''BORRADOR'', ''CUENTA''%'
         THEN 'OK - NUEVOS BORRADORES YA NO INICIALIZAN contacto_a CON EL TELEFONO'
         ELSE 'REVISAR PROCEDIMIENTO'
    END AS EstadoProcedimiento;
