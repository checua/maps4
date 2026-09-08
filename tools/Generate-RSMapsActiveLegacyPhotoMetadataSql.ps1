param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $RepoRoot 'App_Data\LegacyPhotoMigration\active-photo-manifest.csv'
$modernRoot = Join-Path $RepoRoot 'App_Data\RSMapsImages'
$outputPath = Join-Path $RepoRoot 'sql\RSMaps2\52_migrate_active_legacy_photo_metadata.generated.sql'

$active = [ordered]@{
    79=6;80=11;81=15;84=5;85=21;86=25;88=17;89=17;90=13;91=3;92=13;93=22;94=14;95=15;96=29;97=10;98=22;99=21;
    100=7;101=5;102=7;103=7;104=15;105=9;106=11;107=12;108=16;109=6;110=9;111=10;112=7;113=25;114=6;115=10;116=11;117=19;
    118=21;119=20;120=5;121=5;122=6;123=24;124=12;125=12;130=4;132=13;133=12;135=22;138=25;142=33;145=1;146=1;147=1;148=2;
    150=34;151=2;153=19;154=13;155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;166=10;167=1;168=1;169=28
}

if ($active.Count -ne 72) { throw "Proteccion: se esperaban 72 inmuebles activos." }
$expectedPhotos = 0
foreach ($v in $active.Values) { $expectedPhotos += [int]$v }
if ($expectedPhotos -ne 808) { throw "Proteccion: se esperaban 808 fotos activas." }

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "No existe el manifiesto esperado: $manifestPath"
}

$rows = @(Import-Csv -LiteralPath $manifestPath)
if ($rows.Count -ne 808) {
    throw "Proteccion: el manifiesto contiene $($rows.Count) filas; se esperaban 808."
}

function Get-Sha256 {
    param([Parameter(Mandatory=$true)][string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Sql-NString {
    param([Parameter(Mandatory=$true)][string]$Value)
    return "N'" + $Value.Replace("'", "''") + "'"
}

$seen = @{}
$validated = New-Object System.Collections.Generic.List[object]

foreach ($row in $rows) {
    $id = [int]$row.IdInmueble
    $orden = [int]$row.Orden
    $bytes = [int64]$row.Bytes
    $portada = [int]$row.EsPortada
    $nombre = [string]$row.NombreOriginal
    $clave = [string]$row.ClaveAlmacenamiento
    $mime = [string]$row.MimeType
    $sha = ([string]$row.Sha256).ToUpperInvariant()

    if (-not $active.Contains($id)) { throw "Proteccion: inmueble inesperado en manifiesto: $id" }
    $max = [int]$active[$id]
    if ($orden -lt 1 -or $orden -gt $max) { throw "Proteccion: orden invalido $id/$orden." }
    if ($nombre -ne "${id}_${orden}.jpg") { throw "Proteccion: nombre inesperado para ${id}/${orden}: $nombre" }
    if ($clave -ne "${id}/${nombre}") { throw "Proteccion: clave inesperada para ${id}/${orden}: $clave" }
    if ($mime -ne 'image/jpeg') { throw "Proteccion: MimeType inesperado para ${nombre}: $mime" }
    if ($bytes -le 0) { throw "Proteccion: Bytes invalidos para $nombre." }
    if ($portada -ne $(if ($orden -eq 1) { 1 } else { 0 })) { throw "Proteccion: portada invalida para $nombre." }
    if ($sha -notmatch '^[0-9A-F]{64}$') { throw "Proteccion: SHA-256 invalido para $nombre." }

    $key = "$id/$orden"
    if ($seen.ContainsKey($key)) { throw "Proteccion: fila duplicada $key." }
    $seen[$key] = $true

    $physicalPath = Join-Path (Join-Path $modernRoot ([string]$id)) $nombre
    if (-not (Test-Path -LiteralPath $physicalPath)) { throw "Falta archivo moderno preparado: $physicalPath" }
    $item = Get-Item -LiteralPath $physicalPath
    if ([int64]$item.Length -ne $bytes) { throw "Bytes distintos para $nombre." }
    if ((Get-Sha256 -Path $physicalPath) -ne $sha) { throw "SHA-256 distinto para $nombre." }

    $validated.Add([PSCustomObject]@{
        IdInmueble = $id
        Orden = $orden
        NombreOriginal = $nombre
        ClaveAlmacenamiento = $clave
        Bytes = $bytes
        EsPortada = $portada
    })
}

foreach ($entry in $active.GetEnumerator()) {
    $id = [int]$entry.Key
    $count = [int]$entry.Value
    $actual = @($validated | Where-Object { $_.IdInmueble -eq $id })
    if ($actual.Count -ne $count) { throw "Proteccion: inmueble $id tiene $($actual.Count) filas; se esperaban $count." }
    if (@($actual | Where-Object { $_.EsPortada -eq 1 }).Count -ne 1) { throw "Proteccion: inmueble $id no tiene exactamente una portada." }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('/* ============================================================')
[void]$sb.AppendLine('   RSMaps 2.0 - Paso 52 (GENERADO)')
[void]$sb.AppendLine('   MIGRACION MASIVA DE METADATA LEGACY -> MODERNA')
[void]$sb.AppendLine('   Alcance: 72 inmuebles PUBLICADOS/PUBLICOS de IdCuenta=1, 808 fotos.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('   Precondicion fisica:')
[void]$sb.AppendLine('   - tools/Prepare-RSMapsActiveLegacyPhotoMigration.ps1 termino OK.')
[void]$sb.AppendLine('   - Los 808 archivos existen en App_Data/RSMapsImages/<id>.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('   Seguridad:')
[void]$sb.AppendLine('   - No mueve ni elimina archivos.')
[void]$sb.AppendLine('   - No cambia precio, estado, visibilidad ni datos comerciales.')
[void]$sb.AppendLine('   - Rechaza metadata moderna parcial o distinta.')
[void]$sb.AppendLine('   - Es idempotente: una segunda ejecucion valida el resultado y no duplica.')
[void]$sb.AppendLine('   - El inmueble historico 152 NO forma parte de este paso.')
[void]$sb.AppendLine('   ============================================================ */')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('SET NOCOUNT ON;')
[void]$sb.AppendLine('SET XACT_ABORT ON;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("IF DB_NAME() <> 'mapsMarkers'")
[void]$sb.AppendLine("    THROW 55200, 'Este script debe ejecutarse en la base mapsMarkers.', 1;")
[void]$sb.AppendLine("IF OBJECT_ID(N'dbo.RSMAPS_Inmueble', N'U') IS NULL THROW 55201, 'No existe dbo.RSMAPS_Inmueble.', 1;")
[void]$sb.AppendLine("IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagen', N'U') IS NULL THROW 55202, 'No existe dbo.RSMAPS_InmuebleImagen.', 1;")
[void]$sb.AppendLine("IF OBJECT_ID(N'dbo.RSMAPS_InmuebleImagenes', N'U') IS NULL THROW 55203, 'No existe dbo.RSMAPS_InmuebleImagenes.', 1;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('DECLARE @Esperado TABLE')
[void]$sb.AppendLine('(')
[void]$sb.AppendLine('    IdInmueble int NOT NULL,')
[void]$sb.AppendLine('    Orden int NOT NULL,')
[void]$sb.AppendLine('    NombreOriginal nvarchar(255) NOT NULL,')
[void]$sb.AppendLine('    ClaveAlmacenamiento nvarchar(500) NOT NULL,')
[void]$sb.AppendLine('    Bytes bigint NOT NULL,')
[void]$sb.AppendLine('    EsPortada bit NOT NULL,')
[void]$sb.AppendLine('    PRIMARY KEY (IdInmueble, Orden)')
[void]$sb.AppendLine(');')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('INSERT @Esperado (IdInmueble, Orden, NombreOriginal, ClaveAlmacenamiento, Bytes, EsPortada) VALUES')

$sorted = @($validated | Sort-Object IdInmueble, Orden)
for ($i = 0; $i -lt $sorted.Count; $i++) {
    $r = $sorted[$i]
    $suffix = if ($i -eq $sorted.Count - 1) { ';' } else { ',' }
    $line = '({0},{1},{2},{3},{4},{5}){6}' -f $r.IdInmueble,$r.Orden,(Sql-NString $r.NombreOriginal),(Sql-NString $r.ClaveAlmacenamiento),$r.Bytes,$r.EsPortada,$suffix
    [void]$sb.AppendLine($line)
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine("IF (SELECT COUNT(*) FROM @Esperado) <> 808 THROW 55204, 'El manifiesto SQL no contiene exactamente 808 fotos.', 1;")
[void]$sb.AppendLine("IF (SELECT COUNT(DISTINCT IdInmueble) FROM @Esperado) <> 72 THROW 55205, 'El manifiesto SQL no contiene exactamente 72 inmuebles.', 1;")
[void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM @Esperado GROUP BY IdInmueble HAVING MAX(Orden) > 40 OR SUM(CASE WHEN EsPortada=1 THEN 1 ELSE 0 END) <> 1) THROW 55206, 'Orden o portada invalidos en manifiesto.', 1;")
[void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM @Esperado WHERE IdInmueble=152) THROW 55207, 'Seguridad: el inmueble historico 152 no debe migrarse en este paso.', 1;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine(';WITH Objetivo AS')
[void]$sb.AppendLine('(')
[void]$sb.AppendLine('    SELECT IdInmueble, COUNT(*) AS Esperadas FROM @Esperado GROUP BY IdInmueble')
[void]$sb.AppendLine(')')
[void]$sb.AppendLine('SELECT 1 AS Dummy INTO #Precondicion FROM Objetivo WHERE 1=0;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM (SELECT DISTINCT IdInmueble FROM @Esperado) e LEFT JOIN dbo.RSMAPS_Inmueble i ON i.idInmueble=e.IdInmueble WHERE i.idInmueble IS NULL OR i.IdCuenta<>1 OR i.EstadoCodigo<>'PUBLICADO' OR i.VisibilidadCodigo<>'PUBLICO')")
[void]$sb.AppendLine("    THROW 55208, 'Precondicion fallida: algun inmueble ya no es PUBLICADO/PUBLICO de la cuenta 1.', 1;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM (SELECT IdInmueble, COUNT(*) Esperadas FROM @Esperado GROUP BY IdInmueble) e OUTER APPLY (SELECT ISNULL(MAX(ii.Imagenes),0) Legacy FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=e.IdInmueble) x WHERE x.Legacy<>e.Esperadas)")
[void]$sb.AppendLine("    THROW 55209, 'Precondicion fallida: algun contador legacy ya no coincide con el manifiesto.', 1;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('DECLARE @MetadataActual int = (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE EXISTS (SELECT 1 FROM @Esperado e WHERE e.IdInmueble=f.IdInmueble));')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('IF @MetadataActual > 0')
[void]$sb.AppendLine('BEGIN')
[void]$sb.AppendLine("    IF @MetadataActual <> 808 THROW 55210, 'Existe metadata moderna parcial o adicional en los inmuebles objetivo. No se modifica nada.', 1;")
[void]$sb.AppendLine('    IF EXISTS')
[void]$sb.AppendLine('    (')
[void]$sb.AppendLine('        SELECT 1 FROM @Esperado e')
[void]$sb.AppendLine('        LEFT JOIN dbo.RSMAPS_InmuebleImagen f')
[void]$sb.AppendLine("          ON f.IdInmueble=e.IdInmueble AND f.Orden=e.Orden AND f.NombreOriginal=e.NombreOriginal AND f.ClaveAlmacenamiento=e.ClaveAlmacenamiento AND f.MimeType='image/jpeg' AND f.Bytes=e.Bytes AND f.EsPortada=e.EsPortada AND f.Activo=1")
[void]$sb.AppendLine('        WHERE f.IdImagen IS NULL')
[void]$sb.AppendLine('    )')
[void]$sb.AppendLine("        THROW 55211, 'La metadata moderna existente no coincide exactamente con el manifiesto. No se modifica nada.', 1;")
[void]$sb.AppendLine("    SELECT 72 AS InmueblesMigrados, 808 AS FotosModernasActivas, 'OK - PASO 52 YA ESTABA MIGRADO' AS EstadoPaso52;")
[void]$sb.AppendLine('    RETURN;')
[void]$sb.AppendLine('END;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('BEGIN TRY')
[void]$sb.AppendLine('    BEGIN TRANSACTION;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    INSERT dbo.RSMAPS_InmuebleImagen (IdInmueble,IdCuenta,ClaveAlmacenamiento,NombreOriginal,MimeType,Bytes,Orden,EsPortada,Activo,FechaAltaUtc)')
[void]$sb.AppendLine("    SELECT e.IdInmueble,1,e.ClaveAlmacenamiento,e.NombreOriginal,'image/jpeg',e.Bytes,e.Orden,e.EsPortada,1,SYSUTCDATETIME() FROM @Esperado e ORDER BY e.IdInmueble,e.Orden;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    UPDATE ii SET ii.Imagenes=e.Esperadas')
[void]$sb.AppendLine('    FROM dbo.RSMAPS_InmuebleImagenes ii')
[void]$sb.AppendLine('    JOIN (SELECT IdInmueble,COUNT(*) Esperadas FROM @Esperado GROUP BY IdInmueble) e ON e.IdInmueble=ii.idInmueble;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    INSERT dbo.RSMAPS_InmuebleImagenes (idInmueble,Imagenes)')
[void]$sb.AppendLine('    SELECT e.IdInmueble,e.Esperadas FROM (SELECT IdInmueble,COUNT(*) Esperadas FROM @Esperado GROUP BY IdInmueble) e')
[void]$sb.AppendLine('    WHERE NOT EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagenes ii WHERE ii.idInmueble=e.IdInmueble);')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("    IF (SELECT COUNT(*) FROM dbo.RSMAPS_InmuebleImagen f WHERE f.Activo=1 AND EXISTS (SELECT 1 FROM @Esperado e WHERE e.IdInmueble=f.IdInmueble)) <> 808 THROW 55212, 'Verificacion fallida: no quedaron 808 fotos modernas activas.', 1;")
[void]$sb.AppendLine("    IF EXISTS (SELECT 1 FROM dbo.RSMAPS_InmuebleImagen f WHERE f.Activo=1 AND EXISTS (SELECT 1 FROM @Esperado e WHERE e.IdInmueble=f.IdInmueble) GROUP BY f.IdInmueble HAVING SUM(CASE WHEN f.EsPortada=1 THEN 1 ELSE 0 END)<>1) THROW 55213, 'Verificacion fallida: alguna propiedad no tiene exactamente una portada.', 1;")
[void]$sb.AppendLine('    IF EXISTS')
[void]$sb.AppendLine('    (')
[void]$sb.AppendLine('        SELECT 1 FROM @Esperado e')
[void]$sb.AppendLine('        LEFT JOIN dbo.RSMAPS_InmuebleImagen f')
[void]$sb.AppendLine("          ON f.IdInmueble=e.IdInmueble AND f.Orden=e.Orden AND f.NombreOriginal=e.NombreOriginal AND f.ClaveAlmacenamiento=e.ClaveAlmacenamiento AND f.MimeType='image/jpeg' AND f.Bytes=e.Bytes AND f.EsPortada=e.EsPortada AND f.Activo=1")
[void]$sb.AppendLine('        WHERE f.IdImagen IS NULL')
[void]$sb.AppendLine('    )')
[void]$sb.AppendLine("        THROW 55214, 'Verificacion fallida: la metadata insertada no coincide con el manifiesto.', 1;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    COMMIT TRANSACTION;')
[void]$sb.AppendLine('END TRY')
[void]$sb.AppendLine('BEGIN CATCH')
[void]$sb.AppendLine('    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;')
[void]$sb.AppendLine('    THROW;')
[void]$sb.AppendLine('END CATCH;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('SELECT')
[void]$sb.AppendLine('    COUNT(DISTINCT f.IdInmueble) AS InmueblesMigrados,')
[void]$sb.AppendLine('    COUNT(*) AS FotosModernasActivas,')
[void]$sb.AppendLine('    SUM(CASE WHEN f.EsPortada=1 THEN 1 ELSE 0 END) AS PortadasModernas,')
[void]$sb.AppendLine("    'OK - 72 INMUEBLES / 808 FOTOS MIGRADOS A METADATA MODERNA' AS EstadoPaso52")
[void]$sb.AppendLine('FROM dbo.RSMAPS_InmuebleImagen f')
[void]$sb.AppendLine('WHERE f.Activo=1 AND EXISTS (SELECT 1 FROM @Esperado e WHERE e.IdInmueble=f.IdInmueble);')

$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($outputPath, $sb.ToString(), $utf8NoBom)

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    InmueblesValidados = 72
    FotosValidadas = $validated.Count
    ArchivoSqlGenerado = $outputPath
    BytesSql = (Get-Item -LiteralPath $outputPath).Length
    Estado = 'OK - SQL 52 GENERADO DESDE MANIFIESTO Y ARCHIVOS VALIDADOS'
} | Format-List

Write-Host ''
Write-Host 'Este helper NO modifica la base de datos.'
Write-Host 'Revise/ejecute el archivo generado en SSMS solo despues de confirmar este resumen.'