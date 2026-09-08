param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BaseUrl = 'https://rsmap.azurewebsites.net'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Las 136 fotos declaradas por legacy que no existen en ninguna copia local de casa,
# pero que el auditor remoto confirmo disponibles en produccion.
$expected = [ordered]@{
    145=1;146=1;147=1;148=2;150=34;151=2;152=13;153=19;154=13;
    155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;
    166=10;167=1;168=1;169=28
}

$destinationRoot = Join-Path $RepoRoot 'App_Data\LegacyPhotoRecovery\Cargas'
New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null

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

function Download-ValidatedPhoto {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Destination
    )

    $urls = @(
        "$BaseUrl/Cargas/$Name",
        "$BaseUrl/cargas/$Name"
    )

    $lastError = $null

    foreach ($url in $urls) {
        $temp = "$Destination.part"
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force
        }

        try {
            $request = [System.Net.HttpWebRequest]::Create($url)
            $request.Method = 'GET'
            $request.AllowAutoRedirect = $true
            $request.Timeout = 30000
            $request.ReadWriteTimeout = 30000
            $request.UserAgent = 'RSMaps-Photo-Recovery/1.0'

            $response = [System.Net.HttpWebResponse]$request.GetResponse()
            try {
                if ($response.StatusCode -ne [System.Net.HttpStatusCode]::OK) {
                    throw "HTTP $([int]$response.StatusCode)"
                }

                $contentType = [string]$response.ContentType
                if (-not $contentType.ToLowerInvariant().StartsWith('image/')) {
                    throw "Content-Type inesperado: $contentType"
                }

                $input = $response.GetResponseStream()
                try {
                    $output = [System.IO.File]::Create($temp)
                    try {
                        $input.CopyTo($output)
                    }
                    finally {
                        $output.Dispose()
                    }
                }
                finally {
                    if ($input) { $input.Dispose() }
                }

                $downloaded = Get-Item -LiteralPath $temp
                if ($downloaded.Length -le 0) {
                    throw 'Archivo descargado vacio.'
                }

                if ($response.ContentLength -gt 0 -and $downloaded.Length -ne $response.ContentLength) {
                    throw "Longitud distinta: HTTP=$($response.ContentLength), archivo=$($downloaded.Length)"
                }

                if (-not (Test-JpegSignature -Path $temp)) {
                    throw 'El archivo no tiene firma JPEG valida.'
                }

                $hash = (Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash
                Move-Item -LiteralPath $temp -Destination $Destination

                return [PSCustomObject]@{
                    Success = $true
                    Url = $response.ResponseUri.AbsoluteUri
                    Bytes = [int64]$downloaded.Length
                    Sha256 = $hash
                    Error = $null
                }
            }
            finally {
                $response.Close()
            }
        }
        catch {
            $lastError = $_.Exception.Message
            if (Test-Path -LiteralPath $temp) {
                Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
            }
        }
    }

    return [PSCustomObject]@{
        Success = $false
        Url = $null
        Bytes = [int64]0
        Sha256 = $null
        Error = $lastError
    }
}

Write-Host "Base remota: $BaseUrl"
Write-Host "Destino de recuperacion: $destinationRoot"
Write-Host 'IMPORTANTE: este helper NO escribe en wwwroot\Cargas.'
Write-Host ''

$rows = New-Object System.Collections.Generic.List[object]
$totalExpected = 0
$totalRecovered = 0
$totalAlreadyValid = 0
$totalFailed = 0
$totalBytes = [int64]0

foreach ($entry in $expected.GetEnumerator()) {
    $id = [int]$entry.Key
    $count = [int]$entry.Value

    for ($n = 1; $n -le $count; $n++) {
        $totalExpected++
        $name = "${id}_${n}.jpg"
        $destination = Join-Path $destinationRoot $name

        if (Test-Path -LiteralPath $destination) {
            $existing = Get-Item -LiteralPath $destination
            if ($existing.Length -gt 0 -and (Test-JpegSignature -Path $destination)) {
                $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
                $totalAlreadyValid++
                $totalBytes += $existing.Length
                $rows.Add([PSCustomObject]@{
                    Archivo = $name
                    Estado = 'YA_RECUPERADA_OK'
                    Bytes = [int64]$existing.Length
                    Sha256 = $hash
                    Url = ''
                    Error = ''
                })
                continue
            }

            throw "Existe un archivo de recuperacion invalido y no se sobrescribira automaticamente: $destination"
        }

        Write-Host ("Recuperando {0}..." -f $name)
        $result = Download-ValidatedPhoto -Name $name -Destination $destination

        if ($result.Success) {
            $totalRecovered++
            $totalBytes += $result.Bytes
            $rows.Add([PSCustomObject]@{
                Archivo = $name
                Estado = 'RECUPERADA_OK'
                Bytes = $result.Bytes
                Sha256 = $result.Sha256
                Url = $result.Url
                Error = ''
            })
        }
        else {
            $totalFailed++
            $rows.Add([PSCustomObject]@{
                Archivo = $name
                Estado = 'ERROR'
                Bytes = 0
                Sha256 = ''
                Url = ''
                Error = $result.Error
            })
        }
    }
}

$manifest = Join-Path (Split-Path -Parent $destinationRoot) 'recovery-manifest.csv'
$rows | Export-Csv -LiteralPath $manifest -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    FotosEsperadas = $totalExpected
    RecuperadasAhora = $totalRecovered
    YaRecuperadasValidas = $totalAlreadyValid
    Errores = $totalFailed
    FotosValidasEnStaging = $totalRecovered + $totalAlreadyValid
    MBValidos = [math]::Round($totalBytes / 1MB, 2)
    Manifest = $manifest
    Estado = if ($totalFailed -eq 0 -and ($totalRecovered + $totalAlreadyValid) -eq $totalExpected) { 'OK - RECUPERACION COMPLETA EN STAGING' } else { 'REVISAR - RECUPERACION INCOMPLETA' }
} | Format-List

if ($totalFailed -gt 0) {
    Write-Host ''
    Write-Host 'Errores:'
    $rows | Where-Object { $_.Estado -eq 'ERROR' } | Format-Table Archivo, Error -AutoSize -Wrap
}

Write-Host ''
Write-Host 'No se modifico wwwroot\Cargas ni la base de datos.'
Write-Host 'Las fotos recuperadas quedan aisladas hasta una promocion/validacion posterior.'
