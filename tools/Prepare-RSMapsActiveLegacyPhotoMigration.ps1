param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$legacyRoot = Join-Path $RepoRoot 'wwwroot\Cargas'
$recoveryRoot = Join-Path $RepoRoot 'App_Data\LegacyPhotoRecovery\Cargas'
$modernRoot = Join-Path $RepoRoot 'App_Data\RSMapsImages'
$manifestPath = Join-Path $RepoRoot 'App_Data\LegacyPhotoMigration\active-photo-manifest.csv'

$active = [ordered]@{
    79=6;80=11;81=15;84=5;85=21;86=25;88=17;89=17;90=13;91=3;92=13;93=22;94=14;95=15;96=29;97=10;98=22;99=21;
    100=7;101=5;102=7;103=7;104=15;105=9;106=11;107=12;108=16;109=6;110=9;111=10;112=7;113=25;114=6;115=10;116=11;117=19;
    118=21;119=20;120=5;121=5;122=6;123=24;124=12;125=12;130=4;132=13;133=12;135=22;138=25;142=33;145=1;146=1;147=1;148=2;
    150=34;151=2;153=19;154=13;155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;166=10;167=1;168=1;169=28
}

if ($active.Count -ne 72) {
    throw "Proteccion: se esperaban 72 inmuebles activos y se encontraron $($active.Count)."
}

$expectedPhotos = 0
foreach ($v in $active.Values) { $expectedPhotos += [int]$v }
if ($expectedPhotos -ne 808) {
    throw "Proteccion: se esperaban 808 fotos activas y se calcularon $expectedPhotos."
}

if (-not (Test-Path -LiteralPath $legacyRoot)) {
    throw "No existe la carpeta legacy esperada: $legacyRoot"
}
if (-not (Test-Path -LiteralPath $recoveryRoot)) {
    throw "No existe la carpeta de recuperacion esperada: $recoveryRoot"
}

function Get-Sha256 {
    param([Parameter(Mandatory=$true)][string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Test-JpegSignature {
    param([Parameter(Mandatory=$true)][string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        if ($stream.Length -lt 3) { return $false }
        $b1 = $stream.ReadByte()
        $b2 = $stream.ReadByte()
        $b3 = $stream.ReadByte()
        return ($b1 -eq 0xFF -and $b2 -eq 0xD8 -and $b3 -eq 0xFF)
    }
    finally {
        $stream.Dispose()
    }
}

$preflight = New-Object System.Collections.Generic.List[object]
$missing = New-Object System.Collections.Generic.List[string]
$conflicts = New-Object System.Collections.Generic.List[string]
$invalid = New-Object System.Collections.Generic.List[string]

foreach ($entry in $active.GetEnumerator()) {
    $id = [int]$entry.Key
    $count = [int]$entry.Value

    if ($count -gt 40) {
        throw "Proteccion: inmueble $id declara $count fotos y excede el limite de 40."
    }

    for ($n = 1; $n -le $count; $n++) {
        $name = "${id}_${n}.jpg"
        $legacyPath = Join-Path $legacyRoot $name
        $recoveryPath = Join-Path $recoveryRoot $name

        $sourcePath = $null
        $sourceKind = $null
        if (Test-Path -LiteralPath $legacyPath) {
            $sourcePath = $legacyPath
            $sourceKind = 'LEGACY_LOCAL'
        }
        elseif (Test-Path -LiteralPath $recoveryPath) {
            $sourcePath = $recoveryPath
            $sourceKind = 'RECUPERADA_PRODUCCION'
        }
        else {
            $missing.Add($name)
            continue
        }

        if (-not (Test-JpegSignature -Path $sourcePath)) {
            $invalid.Add($name)
            continue
        }

        $sourceItem = Get-Item -LiteralPath $sourcePath
        if ($sourceItem.Length -le 0) {
            $invalid.Add($name)
            continue
        }

        $sha = Get-Sha256 -Path $sourcePath
        $destDir = Join-Path $modernRoot ([string]$id)
        $destPath = Join-Path $destDir $name
        $destState = 'PENDIENTE_COPIA'

        if (Test-Path -LiteralPath $destPath) {
            $destSha = Get-Sha256 -Path $destPath
            if ($destSha -ne $sha) {
                $conflicts.Add($name)
                continue
            }
            $destState = 'YA_OK'
        }

        $preflight.Add([PSCustomObject]@{
            IdInmueble = $id
            Orden = $n
            Nombre = $name
            ClaveAlmacenamiento = "${id}/${name}"
            FuenteTipo = $sourceKind
            Fuente = $sourcePath
            Destino = $destPath
            Bytes = [int64]$sourceItem.Length
            Sha256 = $sha
            EstadoDestino = $destState
        })
    }
}

if ($missing.Count -gt 0 -or $invalid.Count -gt 0 -or $conflicts.Count -gt 0 -or $preflight.Count -ne 808) {
    Write-Host ''
    Write-Host 'PREVUELO DETENIDO - no se copiara ningun archivo.'
    if ($missing.Count -gt 0) {
        Write-Host 'Faltantes:'
        $missing | Sort-Object | ForEach-Object { Write-Host "  $_" }
    }
    if ($invalid.Count -gt 0) {
        Write-Host 'JPEG invalidos:'
        $invalid | Sort-Object | ForEach-Object { Write-Host "  $_" }
    }
    if ($conflicts.Count -gt 0) {
        Write-Host 'Conflictos en destino moderno:'
        $conflicts | Sort-Object | ForEach-Object { Write-Host "  $_" }
    }
    throw "Proteccion: el prevuelo no es consistente. Esperadas=808, Validas=$($preflight.Count), Faltantes=$($missing.Count), Invalidas=$($invalid.Count), Conflictos=$($conflicts.Count)."
}

Write-Host 'PREVUELO OK - 808 fotos activas localizadas y validadas.'
Write-Host 'Copiando de forma idempotente a App_Data\RSMapsImages...'
Write-Host ''

$copied = 0
$alreadyOk = 0
$totalBytes = [int64]0
$manifestRows = New-Object System.Collections.Generic.List[object]

foreach ($row in $preflight) {
    $destDir = Split-Path -Parent $row.Destino
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    if (Test-Path -LiteralPath $row.Destino) {
        $existingSha = Get-Sha256 -Path $row.Destino
        if ($existingSha -ne $row.Sha256) {
            throw "Conflicto inesperado durante copia: $($row.Nombre)"
        }
        $alreadyOk++
        $finalState = 'YA_OK'
    }
    else {
        $tmp = $row.Destino + '.tmp'
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
        Copy-Item -LiteralPath $row.Fuente -Destination $tmp
        $tmpSha = Get-Sha256 -Path $tmp
        if ($tmpSha -ne $row.Sha256) {
            Remove-Item -LiteralPath $tmp -Force
            throw "Hash distinto despues de copiar $($row.Nombre)."
        }
        Move-Item -LiteralPath $tmp -Destination $row.Destino
        $copied++
        $finalState = 'COPIADA'
    }

    $finalItem = Get-Item -LiteralPath $row.Destino
    $totalBytes += $finalItem.Length

    $manifestRows.Add([PSCustomObject]@{
        IdInmueble = $row.IdInmueble
        Orden = $row.Orden
        NombreOriginal = $row.Nombre
        ClaveAlmacenamiento = $row.ClaveAlmacenamiento
        MimeType = 'image/jpeg'
        Bytes = [int64]$finalItem.Length
        Sha256 = $row.Sha256
        EsPortada = if ($row.Orden -eq 1) { 1 } else { 0 }
        FuenteTipo = $row.FuenteTipo
        Estado = $finalState
    })
}

$manifestDir = Split-Path -Parent $manifestPath
if (-not (Test-Path -LiteralPath $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
}
$manifestRows | Sort-Object IdInmueble, Orden | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding UTF8

# Verificacion final fisica.
$finalCount = 0
$finalProblems = New-Object System.Collections.Generic.List[string]
foreach ($row in $preflight) {
    if (-not (Test-Path -LiteralPath $row.Destino)) {
        $finalProblems.Add($row.Nombre)
        continue
    }
    if ((Get-Sha256 -Path $row.Destino) -ne $row.Sha256) {
        $finalProblems.Add($row.Nombre)
        continue
    }
    $finalCount++
}

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    InmueblesActivos = $active.Count
    FotosEsperadas = 808
    FotosValidasEnDestinoModerno = $finalCount
    CopiadasAhora = $copied
    YaExistianValidas = $alreadyOk
    ProblemasFinales = $finalProblems.Count
    MBDestinoModerno = [math]::Round($totalBytes / 1MB, 2)
    Manifest = $manifestPath
    Estado = if ($finalCount -eq 808 -and $finalProblems.Count -eq 0) { 'OK - ARCHIVOS ACTIVOS PREPARADOS PARA METADATA MODERNA' } else { 'REVISAR' }
} | Format-List

if ($finalProblems.Count -gt 0) {
    Write-Host ''
    Write-Host 'Problemas finales:'
    $finalProblems | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

Write-Host ''
Write-Host 'No se modifico la base de datos.'
Write-Host 'No se elimino ningun archivo legacy ni recuperado.'
Write-Host 'El inmueble historico 152 queda fuera de este paso.'
