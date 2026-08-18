/* ============================================================
   RSMaps 2.0 - Paso 04
   PRUEBA CONTROLADA DE ALTA DE INMUEBLE CON ROLLBACK

   Base esperada: mapsMarkers

   Objetivo:
   - Probar RSMAPS_sp_insertar_coordenadas sin dejar datos permanentes.
   - Confirmar que IdCuenta se resuelve desde RSMAPS_CuentaUsuario.
   - Confirmar que idInmobiliaria legacy queda sincronizado.
   - Confirmar que el inmueble y su registro de imagen participan en
     la transacción externa y desaparecen después del ROLLBACK.

   IMPORTANTE:
   - Este script NO debe dejar una propiedad de prueba guardada.
   - Puede consumir un valor IDENTITY aunque la transacción haga ROLLBACK.
     Eso es normal en SQL Server y no implica pérdida de integridad.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 50400, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

DECLARE @Correo varchar(200) = 'profesor76@hotmail.com';
DECLARE @IdTipo int;
DECLARE @NuevoId int = NULL;
DECLARE @TotalAntes int;
DECLARE @TotalDespues int;

SELECT TOP (1)
    @IdTipo = idTipoPropiedad
FROM dbo.RSMAPS_TipoPropiedades
ORDER BY idTipoPropiedad;

IF @IdTipo IS NULL
    THROW 50401, 'No hay tipos de propiedad disponibles para la prueba.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_Usuario u
    INNER JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdAsesor = u.idAsesor
       AND cu.Activo = 1
       AND cu.EsPredeterminada = 1
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
       AND c.Activo = 1
    WHERE u.correo = @Correo
)
    THROW 50402, 'El usuario de prueba no tiene una cuenta activa/predeterminada.', 1;

SELECT @TotalAntes = COUNT(*)
FROM dbo.RSMAPS_Inmueble;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC dbo.RSMAPS_sp_insertar_coordenadas
        @idInmueble = @NuevoId OUTPUT,
        @correo = @Correo,
        @idInmobiliaria = NULL,
        @lat = 24.027700,
        @lng = -104.653200,
        @idTipo = @IdTipo,
        @terreno = 123.45,
        @construccion = 98.76,
        @precio = 1234567,
        @observaciones = 'PRUEBA RSMaps 2.0 - DEBE HACER ROLLBACK',
        @contacto = 'PRUEBA ROLLBACK',
        @numImagenes = 0;

    /* Resultado 1: inmueble creado dentro de la transacción. */
    SELECT
        i.idInmueble,
        i.IdCuenta,
        c.Nombre AS Cuenta,
        c.TipoCuenta,
        i.idInmobiliaria AS IdInmobiliariaLegacy,
        i.idAsesor,
        u.correo,
        i.idTipo,
        i.precio,
        i.observaciones
    FROM dbo.RSMAPS_Inmueble i
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = i.IdCuenta
    INNER JOIN dbo.RSMAPS_Usuario u
        ON u.idAsesor = i.idAsesor
    WHERE i.idInmueble = @NuevoId;

    /* Resultado 2: registro de imágenes creado dentro de la transacción. */
    SELECT
        ii.idInmuebleImagenes,
        ii.idInmueble,
        ii.Imagenes
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = @NuevoId;

    /* Resultado 3: verificaciones esperadas antes del rollback. */
    SELECT
        @TotalAntes AS TotalAntes,
        COUNT(*) AS TotalDurantePrueba,
        @NuevoId AS IdGenerado,
        SUM(CASE WHEN i.idInmueble = @NuevoId AND i.IdCuenta IS NOT NULL THEN 1 ELSE 0 END) AS NuevoConCuenta
    FROM dbo.RSMAPS_Inmueble i;

    ROLLBACK TRANSACTION;

    /* Resultado 4: confirmar que no quedó el inmueble. */
    SELECT @TotalDespues = COUNT(*)
    FROM dbo.RSMAPS_Inmueble;

    SELECT
        @TotalAntes AS TotalAntes,
        @TotalDespues AS TotalDespues,
        @NuevoId AS IdGeneradoDurantePrueba,
        CASE
            WHEN @TotalAntes = @TotalDespues
             AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_Inmueble WHERE idInmueble = @NuevoId)
             AND NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes WHERE idInmueble = @NuevoId)
            THEN 'OK - ROLLBACK COMPLETO'
            ELSE 'REVISAR'
        END AS EstadoFinal;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
