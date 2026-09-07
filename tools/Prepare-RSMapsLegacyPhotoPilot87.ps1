param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$IdInmueble = 87,
    [int]$ExpectedCount = 18
)

$ErrorActionPreference = 'Stop'

$sourceRoot = Join-Path $RepoRoot 'wwwroot\Cargas'
$targetRoot = Join-Path $RepoRoot ("App_Data\RSMapsImages\{0}" -f $IdInmueble)

if (-not (Test-Path $sourceRoot)) {
    throw "No existe la carpeta legacy: $sourceRoot"
}

$sourceFiles = @()
for ($i = 1; $i -le $ExpectedCount; $i++) {
    $path = Join-Path $sourceRoot ("{0}_{1}.jpg" -f $IdInmueble, $i)
    if (-not (Test-Path $path)) {
        throw "Falta la foto legacy esperada: $path"
    }
    $sourceFiles += Get-Item $path
}

$unexpected = Get-ChildItem $sourceRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match ('^{0}_(\d+)\.(jpg|jpeg|png|webp)$' -f $IdInmueble) } |
    Where-Object {
        if ($_.BaseName -match ('^{0}_(\d+)$' -f $IdInmueble)) {
            $n = [int]$Matches[1]
            return $n -lt 1 -or $n -gt $ExpectedCount
        }
        return $true
    }

if ($unexpected) {
    throw "Se encontraron fotos legacy fuera del rango 1..$ExpectedCount para el inmueble $IdInmueble. Revisar antes de migrar."
}

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

$result = @()
foreach ($sourceFile in $sourceFiles) {
    $targetPath = Join-Path $targetRoot $sourceFile.Name
    $sourceHash = (Get-FileHash $sourceFile.FullName -Algorithm SHA256).Hash

    if (Test-Path $targetPath) {
        $targetHash = (Get-FileHash $targetPath -Algorithm SHA256).Hash
        if ($targetHash -ne $sourceHash) {
            throw "Ya existe $targetPath pero su SHA256 es diferente. No se sobrescribio."
        }
        $action = 'YA_EXISTIA_OK'
    }
    else {
        Copy-Item $sourceFile.FullName $targetPath
        $targetHash = (Get-FileHash $targetPath -Algorithm SHA256).Hash
        if ($targetHash -ne $sourceHash) {
            throw "La copia no coincide con el original: $($sourceFile.Name)"
        }
        $action = 'COPIADA'
    }

    $targetItem = Get-Item $targetPath
    $result += [PSCustomObject]@{
        Archivo = $sourceFile.Name
        Bytes = $targetItem.Length
        Estado = $action
    }
}

$targetFiles = Get-ChildItem $targetRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match ('^{0}_(\d+)\.jpg$' -f $IdInmueble) }

if ($targetFiles.Count -ne $ExpectedCount) {
    throw "La carpeta moderna local contiene $($targetFiles.Count) fotos del inmueble $IdInmueble; se esperaban $ExpectedCount."
}

$result | Format-Table -AutoSize
Write-Host ''
Write-Host ("OK - {0} fotos preparadas en {1}" -f $ExpectedCount, $targetRoot)
Write-Host 'No se elimino ni modifico ningun archivo de wwwroot\Cargas.'
