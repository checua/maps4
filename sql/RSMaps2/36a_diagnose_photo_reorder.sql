/* ============================================================
   RSMaps 2.0 - Paso 36A
   DIAGNOSTICO CONTROLADO DE REORDENAMIENTO DE FOTOS

   No deja cambios permanentes. Ejecuta la misma operacion que la Web
   dentro de una transaccion y devuelve el error SQL real si falla.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT OFF;

IF DB_NAME() <> 'mapsMarkers'
    THROW 53610, 'Este script debe ejecutarse en la base mapsMarkers.', 1;

DECLARE @IdInmueble INT = 176;
DECLARE @Correo VARCHAR(200);
DECLARE @IdImagen BIGINT;

SELECT @Correo = u.correo
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
WHERE i.idInmueble = @IdInmueble;

/* Estructura efectiva de la tabla en ESTA base. */
SELECT
    i.name AS Indice,
    i.is_unique AS EsUnico,
    i.filter_definition AS Filtro,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
FROM sys.indexes i
INNER JOIN sys.index_columns ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
  AND i.index_id > 0
  AND ic.is_included_column = 0
GROUP BY i.name, i.is_unique, i.filter_definition
ORDER BY i.name;

SELECT
    cc.name AS Restriccion,
    cc.definition AS Definicion
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
ORDER BY cc.name;

SELECT
    tr.name AS TriggerNombre,
    tr.is_disabled AS Deshabilitado
FROM sys.triggers tr
WHERE tr.parent_id = OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen')
ORDER BY tr.name;

SELECT TOP (1) @IdImagen = f.IdImagen
FROM dbo.RSMAPS_InmuebleImagen f
WHERE f.IdInmueble = @IdInmueble
  AND f.Activo = 1
ORDER BY f.Orden, f.IdImagen;

SELECT
    @IdInmueble AS IdInmueble,
    @Correo AS Correo,
    @IdImagen AS IdImagenMover,
    p.name AS Procedimiento,
    p.modify_date AS FechaModificacionProcedimiento
FROM sys.procedures p
WHERE p.object_id = OBJECT_ID(N'dbo.RSMAPS_sp_MoverFotoBorradorWeb');

SELECT
    f.IdImagen,
    f.Orden,
    f.EsPortada,
    f.Activo,
    'ANTES' AS Momento
FROM dbo.RSMAPS_InmuebleImagen f
WHERE f.IdInmueble = @IdInmueble
ORDER BY f.Orden, f.IdImagen;

IF @Correo IS NULL OR @IdImagen IS NULL
BEGIN
    SELECT
        'REVISAR - NO SE ENCONTRO EL BORRADOR 176 CON FOTOS ACTIVAS' AS EstadoDiagnostico;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC dbo.RSMAPS_sp_MoverFotoBorradorWeb
        @correo = @Correo,
        @idInmueble = @IdInmueble,
        @idImagen = @IdImagen,
        @direccion = 1;

    SELECT
        f.IdImagen,
        f.Orden,
        f.EsPortada,
        f.Activo,
        'DURANTE' AS Momento
    FROM dbo.RSMAPS_InmuebleImagen f
    WHERE f.IdInmueble = @IdInmueble
    ORDER BY f.Orden, f.IdImagen;

    ROLLBACK TRANSACTION;

    SELECT
        CAST(NULL AS INT) AS NumeroError,
        CAST(NULL AS NVARCHAR(4000)) AS MensajeError,
        'OK - EL PROCEDIMIENTO SQL REORDENA; SI LA WEB FALLA EL PROBLEMA ESTA EN LA PETICION/CONTROLADOR' AS EstadoDiagnostico;
END TRY
BEGIN CATCH
    DECLARE @NumeroError INT = ERROR_NUMBER();
    DECLARE @MensajeError NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ProcedimientoError NVARCHAR(200) = ERROR_PROCEDURE();
    DECLARE @LineaError INT = ERROR_LINE();

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    SELECT
        @NumeroError AS NumeroError,
        @MensajeError AS MensajeError,
        @ProcedimientoError AS ProcedimientoError,
        @LineaError AS LineaError,
        'ERROR SQL REPRODUCIDO - USAR ESTOS DATOS PARA CORREGIR' AS EstadoDiagnostico;
END CATCH;
GO
