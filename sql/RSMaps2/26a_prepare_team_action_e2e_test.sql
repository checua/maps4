/* ============================================================
   RSMaps 2.0 - Paso 26A
   PREPARAR INMUEBLE TEMPORAL PARA PRUEBA E2E ADMINISTRATIVA

   Objetivo:
   - Crear UN inmueble temporal en la misma cuenta del usuario de prueba.
   - Asignarlo a OTRO asesor responsable de la cuenta.
   - Permitir validar desde la UI que un ADMINISTRADOR puede operar el
     inmueble conservando Responsable != Actor en la auditoria.
   - No tocar propiedades reales.
   - Eliminarlo despues con 26b_cleanup_team_action_e2e_test.sql.

   IMPORTANTE:
   - Este script SI deja un dato temporal persistente hasta ejecutar 26B.
   - El usuario actor debe estar como ASESOR al preparar la prueba.
   - Para operar desde UI, despues ejecutar Paso 22A para elevarlo
     temporalmente a ADMINISTRADOR.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
    THROW 52600, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

IF COL_LENGTH(N'dbo.RSMAPS_InmuebleCambioEstado', N'IdAsesorResponsable') IS NULL
    THROW 52601, 'Falta auditoria del Paso 23.', 1;
IF COL_LENGTH(N'dbo.RSMAPS_OperacionInmueble', N'IdAsesorActor') IS NULL
    THROW 52602, 'Falta auditoria del Paso 23.', 1;

DECLARE @Marcador nvarchar(120) = N'[RSMAPS-TEST-ADMIN-E2E]';
DECLARE @DireccionPrueba varchar(max) = 'PRUEBA E2E ADMIN - NO ES PROPIEDAD REAL';
DECLARE @CorreoActor varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdActor int;
DECLARE @IdCuenta int;
DECLARE @RolActor varchar(30);
DECLARE @IdAsesorResponsable int;
DECLARE @CorreoResponsable varchar(200);
DECLARE @NombreResponsable nvarchar(300);
DECLARE @IdInmobiliariaLegacy int;
DECLARE @TelefonoResponsable varchar(max);
DECLARE @IdTipo int;
DECLARE @IdInmueble int;
DECLARE @FechaPublicacionUtc datetime2(0) = DATEADD(DAY, -10, SYSUTCDATETIME());
DECLARE @Precio decimal(18,2) = 2345678.00;

/* Reutilizar solamente la prueba exacta si ya existe. */
SELECT TOP (1) @IdInmueble = i.idInmueble
FROM dbo.RSMAPS_Inmueble i
WHERE CHARINDEX(@Marcador, CONVERT(nvarchar(max), i.observaciones)) > 0
  AND CONVERT(varchar(max), i.direccion) = @DireccionPrueba
  AND CONVERT(varchar(max), i.link) = 'TEST-ADMIN-E2E'
  AND CONVERT(varchar(max), i.contacto_a) = 'PRUEBA ADMINISTRATIVA'
ORDER BY i.idInmueble DESC;

IF @IdInmueble IS NOT NULL
BEGIN
    SELECT
        i.idInmueble,
        i.IdCuenta,
        i.idAsesor AS IdAsesorResponsable,
        CONCAT(u.nombres, ' ', u.aPaterno) AS AsesorResponsable,
        u.correo AS CorreoResponsable,
        i.EstadoCodigo,
        i.VisibilidadCodigo,
        TRY_CONVERT(decimal(18,2), i.precio) AS PrecioPublicado,
        i.FechaPublicacionUtc,
        i.direccion,
        'YA EXISTIA - USAR ESTA PROPIEDAD PARA LA PRUEBA ADMINISTRATIVA' AS EstadoPreparacion
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
    WHERE i.idInmueble = @IdInmueble;

    RETURN;
END;

/* Resolver actor y cuenta actual. */
SELECT @IdActor = u.idAsesor
FROM dbo.RSMAPS_Usuario u
WHERE u.correo = @CorreoActor;

IF @IdActor IS NULL
    THROW 52603, 'No existe el usuario actor configurado para la prueba.', 1;

SELECT TOP (1)
    @IdCuenta = cu.IdCuenta,
    @RolActor = cu.RolCodigo,
    @IdInmobiliariaLegacy = c.IdInmobiliariaLegacy
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Cuenta c ON c.IdCuenta = cu.IdCuenta
WHERE cu.IdAsesor = @IdActor
  AND cu.Activo = 1
  AND c.Activo = 1
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;

IF @IdCuenta IS NULL
    THROW 52604, 'El usuario actor no pertenece a una cuenta activa.', 1;

IF @RolActor <> 'ASESOR'
    THROW 52605, 'El usuario actor debe estar como ASESOR antes de preparar la prueba. Ejecuta 22B si quedo elevado.', 1;

/* Elegir automaticamente otro asesor activo de la misma cuenta.
   Se prioriza alguien que ya tenga inventario para representar un caso real de equipo. */
;WITH Conteo AS
(
    SELECT i.IdCuenta, i.idAsesor, COUNT(*) AS Inmuebles
    FROM dbo.RSMAPS_Inmueble i
    GROUP BY i.IdCuenta, i.idAsesor
)
SELECT TOP (1)
    @IdAsesorResponsable = cu.IdAsesor,
    @CorreoResponsable = u.correo,
    @NombreResponsable = CONCAT(u.nombres, N' ', u.aPaterno),
    @TelefonoResponsable = u.telefono
FROM dbo.RSMAPS_CuentaUsuario cu
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = cu.IdAsesor
LEFT JOIN Conteo ct ON ct.IdCuenta = cu.IdCuenta AND ct.idAsesor = cu.IdAsesor
WHERE cu.IdCuenta = @IdCuenta
  AND cu.IdAsesor <> @IdActor
  AND cu.Activo = 1
  AND u.correo IS NOT NULL
ORDER BY ISNULL(ct.Inmuebles, 0) DESC, cu.IdAsesor;

IF @IdAsesorResponsable IS NULL
    THROW 52606, 'No existe otro asesor activo en la misma cuenta para la prueba de equipo.', 1;

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
    THROW 52607, 'No existe un tipo de propiedad disponible para la prueba.', 1;

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
        @IdAsesorResponsable,
        @DireccionPrueba,
        24.031100,
        -104.658900,
        @IdTipo,
        @TelefonoResponsable,
        180,
        140,
        CONVERT(float, @Precio),
        @Marcador + N' Propiedad temporal para validar acciones administrativas auditadas. NO REAL.',
        1,
        'TEST-ADMIN-E2E',
        'PRUEBA ADMINISTRATIVA',
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
        IdInmueble, IdCuenta, IdAsesor, PrecioAnterior, PrecioNuevo,
        Moneda, FechaCambioUtc, Motivo, Origen, EsDatoConfiable
    )
    VALUES
    (
        @IdInmueble, @IdCuenta, @IdAsesorResponsable, NULL, @Precio,
        'MXN', @FechaPublicacionUtc,
        N'Precio inicial de propiedad temporal para prueba administrativa E2E.', 'PRUEBA', 1
    );

    INSERT dbo.RSMAPS_InmuebleCambioEstado
    (
        IdInmueble, IdCuenta, EstadoAnterior, EstadoNuevo,
        VisibilidadAnterior, VisibilidadNueva,
        IdAsesorResponsable, IdAsesorCambio,
        FechaCambioUtc, Motivo, Origen
    )
    VALUES
    (
        @IdInmueble, @IdCuenta, NULL, 'PUBLICADO',
        NULL, 'PUBLICO',
        @IdAsesorResponsable, NULL,
        @FechaPublicacionUtc,
        N'Alta controlada de inmueble temporal para prueba administrativa E2E.', 'PRUEBA'
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    i.idInmueble,
    i.IdCuenta,
    @IdActor AS IdAsesorActorEsperado,
    @CorreoActor AS CorreoActor,
    i.idAsesor AS IdAsesorResponsable,
    @NombreResponsable AS AsesorResponsable,
    @CorreoResponsable AS CorreoResponsable,
    i.EstadoCodigo,
    i.VisibilidadCodigo,
    TRY_CONVERT(decimal(18,2), i.precio) AS PrecioPublicado,
    i.FechaPublicacionUtc,
    i.direccion,
    CASE WHEN i.idAsesor <> @IdActor
         THEN 'OK - PROPIEDAD TEMPORAL AJENA LISTA PARA PRUEBA ADMIN E2E'
         ELSE 'REVISAR'
    END AS EstadoPreparacion
FROM dbo.RSMAPS_Inmueble i
WHERE i.idInmueble = @IdInmueble;

SELECT
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmueblePrecioHistorial WHERE IdInmueble = @IdInmueble) AS HistorialPrecio,
    (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleCambioEstado WHERE IdInmueble = @IdInmueble) AS HistorialEstado,
    (SELECT COUNT(*) FROM dbo.RSMAPS_OperacionInmueble WHERE IdInmueble = @IdInmueble) AS Operaciones,
    'AHORA EJECUTAR 22A, ENTRAR COMO ADMINISTRADOR Y OPERAR SOLO ESTE ID. NO EJECUTAR 26B HASTA TERMINAR.' AS SiguientePaso;
