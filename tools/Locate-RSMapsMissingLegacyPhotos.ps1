param(
    [string]$RsMapsRoot = 'D:\Repos\checua\RSMaps'
)

$ErrorActionPreference = 'Stop'

# IDs whose legacy counters report photos but the publication worktree had zero
# physical files in wwwroot\Cargas during Step 51 filesystem audit.
$expected = [ordered]@{
    145=1;146=1;147=1;148=2;150=34;151=2;152=13;153=19;154=13;
    155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;
    166=10;167=1;168=1;169=28
}

if (-not (Test-Path -LiteralPath $RsMapsRoot)) {
    throw "No existe la raiz esperada: $RsMapsRoot"
}

$knownRoots = @(
    (Join-Path $RsMapsRoot 'maps4-publication\wwwroot\Cargas'),
    (Join-Path $RsMapsRoot 'maps4\wwwroot\Cargas'),
    (Join-Path $RsMapsRoot 'maps4-master\wwwroot\Cargas')
) | Where-Object { Test-Path -LiteralPath $_ }

Write-Host 'Raices Cargas conocidas:'
$knownRoots | ForEach-Object { Write-Host "  $_" }
Write-Host ''
Write-Host 'Indexando una sola vez los archivos locales bajo RSMaps...'

# Indexar una sola vez para evitar recorrer todo el arbol por cada foto faltante.
$allFiles = Get-ChildItem -LiteralPath $RsMapsRoot -Recurse -File -ErrorAction SilentlyContinue
$byBaseName = @{}
foreach ($file in $allFiles) {
    $ext = $file.Extension.ToLowerInvariant()
    if ($ext -notin @('.jpg','.jpeg','.png','.webp')) { continue }

    $key = $file.BaseName.ToLowerInvariant()
    if (-not $byBaseName.ContainsKey($key)) {
        $byBaseName[$key] = New-Object System.Collections.Generic.List[object]
    }
    $byBaseName[$key].Add($file)
}

$rows = New-Object System.Collections.Generic.List[object]
$totalExpected = 0
$totalExactFound = 0
$totalAlternativeFound = 0

foreach ($entry in $expected.GetEnumerator()) {
    $id = [int]$entry.Key
    $count = [int]$entry.Value
    $totalExpected += $count

    $exactForProperty = 0
    $alternativeForProperty = 0
    $locations = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

    for ($n = 1; $n -le $count; $n++) {
        $base = "${id}_${n}"
        $exactName = "${base}.jpg"
        $foundExact = $false

        foreach ($root in $knownRoots) {
            $path = Join-Path $root $exactName
            if (Test-Path -LiteralPath $path) {
                $foundExact = $true
                [void]$locations.Add($root)
            }
        }

        if ($foundExact) {
            $exactForProperty++
            continue
        }

        $key = $base.ToLowerInvariant()
        if ($byBaseName.ContainsKey($key)) {
            $matches = @($byBaseName[$key])
            if ($matches.Count -gt 0) {
                $alternativeForProperty++
                foreach ($m in $matches) {
                    [void]$locations.Add($m.DirectoryName)
                }
            }
        }
    }

    $totalExactFound += $exactForProperty
    $totalAlternativeFound += $alternativeForProperty

    $rows.Add([PSCustomObject]@{
        IdInmueble = $id
        Esperadas = $count
        EncontradasCargas = $exactForProperty
        EncontradasAlternas = $alternativeForProperty
        FaltantesLocales = $count - $exactForProperty - $alternativeForProperty
        Ubicaciones = (($locations | Sort-Object) -join ' | ')
    })
}

$rows | Format-Table -AutoSize -Wrap

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    FotosEsperadas = $totalExpected
    EncontradasEnCargasConocidas = $totalExactFound
    EncontradasEnOtrasRutasLocales = $totalAlternativeFound
    AunNoLocalizadas = $totalExpected - $totalExactFound - $totalAlternativeFound
    Patron = 'El auditor anterior encontro completas 79..142 y cero desde 145 en adelante; este helper comprueba si existe otra copia local o extension alternativa.'
} | Format-List

Write-Host ''
Write-Host 'SOLO LECTURA - no se copio, movio, elimino ni modifico ningun archivo.'
