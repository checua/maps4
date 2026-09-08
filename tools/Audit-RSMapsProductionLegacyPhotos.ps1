param(
    [string]$BaseUrl = 'https://rsmap.azurewebsites.net'
)

$ErrorActionPreference = 'Stop'

# Fotos que el Paso 51/52 no localizo en ninguna copia local de casa.
$expected = [ordered]@{
    145=1;146=1;147=1;148=2;150=34;151=2;152=13;153=19;154=13;
    155=1;156=1;157=1;158=1;159=1;161=1;162=1;163=1;164=1;165=1;
    166=10;167=1;168=1;169=28
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Test-RemotePhoto {
    param([Parameter(Mandatory=$true)][string]$Url)

    try {
        $request = [System.Net.HttpWebRequest]::Create($Url)
        $request.Method = 'HEAD'
        $request.AllowAutoRedirect = $true
        $request.Timeout = 15000
        $request.ReadWriteTimeout = 15000
        $request.UserAgent = 'RSMaps-Photo-Audit/1.0'

        $response = [System.Net.HttpWebResponse]$request.GetResponse()
        try {
            return [PSCustomObject]@{
                Found = ($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)
                Status = [int]$response.StatusCode
                Length = [int64]$response.ContentLength
                FinalUrl = $response.ResponseUri.AbsoluteUri
            }
        }
        finally {
            $response.Close()
        }
    }
    catch [System.Net.WebException] {
        $status = $null
        $finalUrl = $Url
        $length = [int64]-1
        if ($_.Exception.Response) {
            $resp = [System.Net.HttpWebResponse]$_.Exception.Response
            try {
                $status = [int]$resp.StatusCode
                $finalUrl = $resp.ResponseUri.AbsoluteUri
                $length = [int64]$resp.ContentLength
            }
            finally {
                $resp.Close()
            }
        }

        return [PSCustomObject]@{
            Found = $false
            Status = $status
            Length = $length
            FinalUrl = $finalUrl
        }
    }
}

Write-Host "Base remota: $BaseUrl"
Write-Host ''
Write-Host 'Controles conocidos:'
foreach ($control in @('79_1.jpg','87_1.jpg')) {
    $upper = "$BaseUrl/Cargas/$control"
    $r = Test-RemotePhoto -Url $upper
    if (-not $r.Found) {
        $lower = "$BaseUrl/cargas/$control"
        $r2 = Test-RemotePhoto -Url $lower
        if ($r2.Found) { $r = $r2 }
    }
    Write-Host ("  {0,-12} HTTP {1} Found={2} Url={3}" -f $control, $r.Status, $r.Found, $r.FinalUrl)
}

Write-Host ''
Write-Host 'Auditando las 136 fotos no localizadas en casa...'

$rows = New-Object System.Collections.Generic.List[object]
$missing = New-Object System.Collections.Generic.List[string]
$totalExpected = 0
$totalFound = 0
$totalBytes = [int64]0

foreach ($entry in $expected.GetEnumerator()) {
    $id = [int]$entry.Key
    $count = [int]$entry.Value
    $totalExpected += $count
    $foundForProperty = 0
    $bytesForProperty = [int64]0

    for ($n = 1; $n -le $count; $n++) {
        $name = "${id}_${n}.jpg"
        $upper = "$BaseUrl/Cargas/$name"
        $result = Test-RemotePhoto -Url $upper

        if (-not $result.Found) {
            $lower = "$BaseUrl/cargas/$name"
            $lowerResult = Test-RemotePhoto -Url $lower
            if ($lowerResult.Found) { $result = $lowerResult }
        }

        if ($result.Found) {
            $foundForProperty++
            $totalFound++
            if ($result.Length -gt 0) {
                $bytesForProperty += $result.Length
                $totalBytes += $result.Length
            }
        }
        else {
            $missing.Add($name)
        }
    }

    $rows.Add([PSCustomObject]@{
        IdInmueble = $id
        Esperadas = $count
        EncontradasProduccion = $foundForProperty
        FaltantesProduccion = $count - $foundForProperty
        BytesReportados = $bytesForProperty
        Estado = if ($foundForProperty -eq $count) { 'OK' } elseif ($foundForProperty -gt 0) { 'PARCIAL' } else { 'NO_ENCONTRADAS' }
    })
}

$rows | Format-Table -AutoSize

Write-Host ''
Write-Host 'Resumen:'
[PSCustomObject]@{
    FotosEsperadas = $totalExpected
    FotosEncontradasEnProduccion = $totalFound
    FotosAunNoLocalizadas = $missing.Count
    MBReportados = [math]::Round($totalBytes / 1MB, 2)
    Estado = if ($missing.Count -eq 0) { 'OK - TODAS DISPONIBLES EN PRODUCCION' } elseif ($totalFound -gt 0) { 'PARCIAL - REVISAR FALTANTES' } else { 'NO LOCALIZADAS EN PRODUCCION' }
} | Format-List

if ($missing.Count -gt 0) {
    Write-Host ''
    Write-Host 'Faltantes en produccion:'
    $missing | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

Write-Host ''
Write-Host 'SOLO LECTURA REMOTA - no se descargo, copio, movio, elimino ni modifico ningun archivo.'
