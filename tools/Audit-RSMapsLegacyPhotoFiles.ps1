param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$sourceRoot = Join-Path $RepoRoot 'wwwroot\Cargas'
if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "No existe el directorio legacy esperado: $sourceRoot"
}

# Candidatos obtenidos del Paso 51. Solo lectura del sistema de archivos.
$principal = [ordered]@{
    79=6;80=11;81=15;84=5;85=21;86=25;88=17;89=17;90=13;91=3;92=13;93=22;94=14;95=15;96=29;97=10;98=22;99=21;100=7;101=5;102=7;103=7;104=15;105=9;106=11;107=12;108=16;109=6;110=9;111=10;112=7;113=25;114=6;115=10;116=11;117=19;118=21;119=20;120=5;121=5;122=6;123=24;124=12;125=12;130=4;132=13;133=12;135=22;138=25;142=33;145=1;146=1;147=1;148=2;150=34;151=2;153=19;154=13;155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;166=10;167=1;168=1;169=28
}

$historico = [ordered]@{ 152=13 }

$rows = New-Object System.Collections.Generic.List[object]
$missingFiles = New-Object System.Collections.Generic.List[string]
$unexpectedFiles = New-Object System.Collections.Generic.List[string]
$totalEsperadas = 0
$totalEncontradas = 0
$totalBytes = [int64]0

function Audit-Group {
    param(
        [Parameter(Mandatory=$true)][string]$Grupo,
        [Parameter(Mandatory=$true)][System.Collections.IDictionary]$Items
    )

    foreach ($entry in $Items.GetEnumerator()) {
        $id = [int]$entry.Key
        $esperadas = [int]$entry.Value
        $script:totalEsperadas += $esperadas

        $found = 0
        $bytes = [int64]0
        for ($n = 1; $n -le $esperadas; $n++) {
            $name = "${id}_${n}.jpg"
            $path = Join-Path $sourceRoot $name
            if (Test-Path -LiteralPath $path) {
                $item = Get-Item -LiteralPath $path
                $found++
                $bytes += $item.Length
            }
            else {
                $missingFiles.Add($name)
            }
        }

        # Detecta archivos legacy adicionales para el mismo inmueble fuera del rango declarado.
        $prefix = "${id}_"
        Get-ChildItem -LiteralPath $sourceRoot -File -Filter "${id}_*.jpg" | ForEach-Object {
            if ($_.BaseName -match '^([0-9]+)_([0-9]+)$') {
                $orden = [int]$Matches[2]
                if ($orden -lt 1 -or $orden -gt $esperadas) {
                    $unexpectedFiles.Add($_.Name)
                }
            }
        }

        $script:totalEncontradas += $found
        $script:totalBytes += $bytes
        $estado = if ($found -eq $esperadas) { 'OK' } else { 'REVISAR' }

        $rows.Add([PSCustomObject]@{
            Grupo = $Grupo
            IdInmueble = $id
            Esperadas = $esperadas
            Encontradas = $found
            Faltantes = ($esperadas - $found)
            Bytes = $bytes
            Estado = $estado
        })
    }
}

Audit-Group -Grupo 'INVENTARIO_PRINCIPAL' -Items $principal
Audit-Group -Grupo 'HISTORICO' -Items $historico

$rows | Sort-Object Grupo, IdInmueble | Format-Table -AutoSize

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    InmueblesAuditados = $rows.Count
    FotosEsperadas = $totalEsperadas
    FotosEncontradas = $totalEncontradas
    FotosFaltantes = $missingFiles.Count
    ArchivosAdicionales = $unexpectedFiles.Count
    BytesEncontrados = $totalBytes
    MBEncontrados = [math]::Round($totalBytes / 1MB, 2)
    Estado = if ($missingFiles.Count -eq 0 -and $unexpectedFiles.Count -eq 0 -and $totalEsperadas -eq $totalEncontradas) { 'OK' } else { 'REVISAR' }
} | Format-List

if ($missingFiles.Count -gt 0) {
    Write-Host ''
    Write-Host 'Faltantes:'
    $missingFiles | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

if ($unexpectedFiles.Count -gt 0) {
    Write-Host ''
    Write-Host 'Archivos adicionales respecto al contador legacy:'
    $unexpectedFiles | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

Write-Host ''
Write-Host 'SOLO LECTURA - no se copio, movio, elimino ni modifico ningun archivo.'
