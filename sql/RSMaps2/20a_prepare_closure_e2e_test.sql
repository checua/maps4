/* ============================================================
   RSMaps 2.0 - Paso 20A
   PREPARAR INMUEBLE TEMPORAL PARA PRUEBA E2E DE CIERRE

   Objetivo:
   - Crear UN inmueble temporal claramente identificable.
   - Permitir cerrarlo desde /Inventario/CerrarOperacion.
   - No tocar propiedades reales.
   - Dejar historial inicial explícito de prueba.
   - Eliminarlo después con 20b_cleanup_closure_e2e_test.sql.

   IMPORTANTE:
   - Este script SI deja datos temporales persistentes hasta ejecutar 20B.
   - No ejecutar repetidamente: si ya existe la prueba, reutiliza la misma.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52000, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @Marcador nvarchar(100) = N'[RSMAPS-TEST-CIERRE-E2E]';
DECLARE @Correo varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdInmueble int;
DECLARE @IdAsesor int;
DECLARE @IdCuenta int;
DECLARE @IdInmobiliariaLegacy int;
DECLARE @Telefono varchar(max);
DECLARE @IdTipo int;
DECLARE @FechaPublicacionUtc datetime2(0) = DATEADD(DAY, -15, SYSUTCDATETIME());
DECLARE @Precio decimal(18,2) = 1234567.00;

/* Reutilizar una prueba pendiente si ya existe. */
SELECT TOP (1) @IdInmueble = i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE CONVERT(nvarchar(max), i.observaciones) LIKE N'%' + @Marcador + N'%'
ORDER BY i.idInmueble DESC;

IF @IdInmueble IS NOT NULL
BEGIN
    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        i.precio,
        i.FechaPublicacionUtc,
        tp.nombre AS TipoNombre,
        'YA EXISTIA - USAR ESTA PROPIEDAD PARA LA PRUEBA' AS EstadoPreparacion
    FROM dbo.RSMAPS_Inmueble i
    LEFT JOIN dbo.RSMAPS_TipoPropiedades tp
        ON tp.idTipoPropiedad = i.idTipo
    WHERE i.idInmueble = @IdInmueble;

    RETURN;
END;

SELECT
    @IdAsesor = u.idAsesor,
    @Telefono = u.telefono
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @Correo;

IF @IdAsesor IS NULL
    THROW 52001, 'No existe el usuario esperado para la prueba E2E.', 1;

SELECT TOP (1)
    @IdCuenta = cu.IdCuenta,
    @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdAsesor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @IdCuenta IS NULL
    THROW 52002, 'El usuario de prueba no pertenece a una cuenta activa.', 1;

SELECT TOP (1) @IdTipo = tp.idTipoPropiedad
FROM dbo.RSMAPS_TipoPropiedades tp
WHERE tp.nombre LIKE '%Casa%'
  AND tp.nombre LIKE '%Venta%'
ORDER BY tp.idTipoPropiedad;

IF @IdTipo IS NULL
    SELECT TOP (1) @IdTipo = idTipoPropiedad
    FROM dbo.RSMAPS_TipoPropiedades
    ORDER BY idTipoPropiedad;

IF @IdTipo IS NULL
    THROW 52003, 'No existe un tipo de propiedad disponible para la prueba.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT dbo.RSMAPS_Inmueble
    (
        idInmobiliaria,
        idAsesor,
        direccion,
        lat,
        lng,
        idTipo,
        telefono,
        terreno,
        construccion,
        precio,
        observaciones,
        exclusiva,
        link,
        contacto_a,
        IdCuenta,
        EstadoCodigo,
        VisibilidadCodigo,
        FechaPublicacionUtc,
        FechaUltimoCambioEstadoUtc
    )
    VALUES
    (
        @IdInmobiliariaLegacy,
        @IdAsesor,
        'PRUEBA E2E CIERRE - NO ES PROPIEDAD REAL',
        24.027500,
        -104.653100,
        @IdTipo,
        @Telefono,
        150,
        120,
        CONVERT(float, @Precio),
        @Marcador + N' Propiedad temporal para validar cierre end-to-end desde la interfaz. NO REAL.',
        1,
        'TEST-E2E',
        'PRUEBA CONTROLADA',
        @IdCuenta,
        'PUBLICADO',
        'PUBLICO',
        @FechaPublicacionUtc,
        @FechaPublicacionUtc
    );

    SET @IdInmueble = CONVERT(int, SCOPE_IDENTITY());

    INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble, Imagenes)
    VALUES (@IdInmueble, 0);

    INSERT dbo.RSMAPS_InmueblePrecioHistorial
    (
        IdInmueble,
        IdCuenta,
        IdAsesor,
        PrecioAnterior,
        PrecioNuevo,
        Moneda,
        FechaCambioUtc,
        Motivo,
        Origen,
        EsDatoConfiable
    )
    VALUES
    (
        @IdInmueble,
        @IdCuenta,
        @IdAsesor,
        NULL,
        @Precio,
        'MXN',
        @FechaPublicacionUtc,
        N'Precio inicial de propiedad temporal para prueba E2E.',
        'PRUEBA',
        1
    );

    INSERT dbo.RSMAPS_InmuebleCambioEstado
    (
        IdInmueble,
        IdCuenta,
        EstadoAnterior,
        EstadoNuevo,
        VisibilidadAnterior,
        VisibilidadNueva,
        IdAsesorCambio,
        FechaCambioUtc,
        Motivo,
        Origen
    )
    VALUES
    (
        @IdInmueble,
        @IdCuenta,
        NULL,
        'PUBLICADO',
        NULL,
        'PUBLICO',
        @IdAsesor,
        @FechaPublicacionUtc,
        N'Alta controlada de inmueble temporal para prueba E2E de cierre.',
        'PRUEBA'
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    i.idInmueble,
    i.IdCuenta,
    i.idAsesor,
    u.correo,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioPublicado,
    i.FechaPublicacionUtc,
    tp.nombre AS TipoNombre,
    'OK - PROPIEDAD TEMPORAL LISTA PARA CIERRE E2E' AS EstadoPreparacion
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = i.idAsesor
LEFT JOIN dbo.RSMAPS_TipoPropiedades tp
    ON tp.idTipoPropiedad = i.idTipo
WHERE i.idInmueble = @IdInmueble;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble = @IdInmueble) AS HistorialPrecio,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueble) AS HistorialEstado,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueble) AS Operaciones,
    'NO EJECUTAR 20B HASTA TERMINAR LA PRUEBA DESDE LA INTERFAZ' AS SiguientePaso;
