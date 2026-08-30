param(
    [string]$Project = ".\RSMaps.Radar.Listener\RSMaps.Radar.Listener.csproj"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$env:RADAR_AGENT_CONFIG_PATH = Join-Path $env:LOCALAPPDATA "RSMaps\RadarAgent\agent-beta-casa.json"
$env:RADAR_SAFE_LAB = "1"
Remove-Item Env:RADAR_CHAT_DISCOVERY_MINUTES -ErrorAction SilentlyContinue

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$marks = @{}
$initializedChats = 0
$stabilizationOpenCount = 0
$roundStartSeconds = $null
$roundDurations = New-Object System.Collections.Generic.List[double]
$inStabilization = $false
$summaryPrinted = $false

function Set-Mark([string]$Name) {
    if (-not $marks.ContainsKey($Name)) {
        $marks[$Name] = $stopwatch.Elapsed.TotalSeconds
    }
}

function Format-Seconds([double]$Seconds) {
    return ("{0,7:N2} s" -f $Seconds)
}

function Write-StartupSummary {
    if ($summaryPrinted) {
        return
    }

    $script:summaryPrinted = $true
    Set-Mark "Active"

    Write-Host ""
    Write-Host "================ TIEMPOS DE ARRANQUE ================"

    if ($marks.ContainsKey("WhatsAppOpened")) {
        Write-Host ("Proceso -> WhatsApp Web abierto:       {0}" -f (Format-Seconds $marks["WhatsAppOpened"]))
    }

    if ($marks.ContainsKey("WhatsAppOpened") -and $marks.ContainsKey("InitStart")) {
        Write-Host ("WhatsApp/config -> Inicializando:      {0}" -f (Format-Seconds ($marks["InitStart"] - $marks["WhatsAppOpened"])))
    }

    if ($marks.ContainsKey("InitStart") -and $marks.ContainsKey("StabilizeStart")) {
        Write-Host ("Inicialización de chats:               {0}" -f (Format-Seconds ($marks["StabilizeStart"] - $marks["InitStart"])))
    }

    for ($i = 0; $i -lt $roundDurations.Count; $i++) {
        Write-Host ("Estabilización ronda {0}:               {1}" -f ($i + 1), (Format-Seconds $roundDurations[$i]))
    }

    if ($marks.ContainsKey("StabilizeStart")) {
        Write-Host ("Estabilización total:                  {0}" -f (Format-Seconds ($marks["Active"] - $marks["StabilizeStart"])))
    }

    Write-Host ("TOTAL hasta RADAR ACTIVO:               {0}" -f (Format-Seconds $marks["Active"]))
    Write-Host ("Chats inicializados correctamente:      {0}" -f $initializedChats)
    Write-Host "======================================================"
    Write-Host ""
}

Write-Host "Midiendo arranque de RADAR..."
Write-Host "Proyecto: $Project"
Write-Host "Agent:    $env:RADAR_AGENT_CONFIG_PATH"
Write-Host ""

& dotnet run --project $Project 2>&1 | ForEach-Object {
    $line = $_.ToString()
    $elapsed = $stopwatch.Elapsed.TotalSeconds

    Write-Host ("[{0,8:N2}s] {1}" -f $elapsed, $line)

    if ($line -eq "WhatsApp Web abierto.") {
        Set-Mark "WhatsAppOpened"
    }
    elseif ($line -eq "RADAR Agent configurado:") {
        Set-Mark "AgentConfig"
    }
    elseif ($line -eq "Inicializando chats...") {
        Set-Mark "InitStart"
    }
    elseif (-not $inStabilization -and $line.StartsWith("✓ ")) {
        $initializedChats++
    }
    elseif ($line -eq "Estabilizando historial visible...") {
        Set-Mark "StabilizeStart"
        $inStabilization = $true
        $roundStartSeconds = $stopwatch.Elapsed.TotalSeconds
    }
    elseif ($inStabilization -and (
        $line.Contains(": abierto desde lista visible.") -or
        $line.Trim() -eq "chat confirmado abierto.")) {

        $stabilizationOpenCount++

        if ($initializedChats -gt 0 -and ($stabilizationOpenCount % $initializedChats) -eq 0) {
            $roundEnd = $stopwatch.Elapsed.TotalSeconds
            $roundDurations.Add($roundEnd - $roundStartSeconds)
            $roundStartSeconds = $roundEnd
        }
    }
    elseif ($line.Trim() -eq "RADAR ACTIVO") {
        Write-StartupSummary
    }
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
