$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$programPath = Join-Path $repoRoot 'RSMaps.Radar.Listener\Program.cs'
$tempPath = "$programPath.matching.tmp"
$backupPath = "$programPath.pre-matching.bak"

if (-not (Test-Path $programPath)) {
    throw "No encontré Program.cs en $programPath"
}

$content = Get-Content $programPath -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($content)) {
    throw 'Program.cs está vacío. Recupéralo primero con: git restore --source=HEAD -- RSMaps.Radar.Listener/Program.cs'
}

$original = $content

$oldVersion = 'Console.WriteLine("      RSMaps Radar v0.7.4-stable");'
$newVersion = 'Console.WriteLine("      RSMaps Radar v0.8.0-matching");'
if ($content.Contains($oldVersion)) {
    $content = $content.Replace($oldVersion, $newVersion)
}

$oldLoop = @'
            foreach (var solicitud in resultado.Solicitudes)
            {
                MostrarSolicitud(solicitud);
                var envio = await EnviarAlerta(page, solicitud);
'@

$newLoop = @'
            foreach (var solicitud in resultado.Solicitudes)
            {
                MostrarSolicitud(solicitud);

                solicitud.MatchingResumen = await RsMapsMatchingClient.ConstruirResumenAsync(solicitud);
                Console.WriteLine(
                    $"  🔎 {solicitud.MatchingResumen.Replace("\r", " ").Replace("\n", " | ")}");

                var envio = await EnviarAlerta(page, solicitud);
'@

if (-not $content.Contains($oldLoop)) {
    if (-not $content.Contains('solicitud.MatchingResumen = await RsMapsMatchingClient.ConstruirResumenAsync(solicitud);')) {
        throw 'No encontré el bloque esperado del ciclo de solicitudes. No se modificó Program.cs.'
    }
}
else {
    $content = $content.Replace($oldLoop, $newLoop)
}

$oldStatus = '    sb.AppendLine("Estado: pendiente de comparar con RSMaps");'
$newStatus = @'
    if (!string.IsNullOrWhiteSpace(s.MatchingResumen))
    {
        sb.AppendLine(s.MatchingResumen);
    }
    else
    {
        sb.AppendLine("COINCIDENCIAS RSMAPS");
        sb.AppendLine("⚠ Comparación no disponible.");
    }
'@

if (-not $content.Contains($oldStatus)) {
    if (-not $content.Contains('sb.AppendLine(s.MatchingResumen);')) {
        throw 'No encontré la línea de estado esperada. No se modificó Program.cs.'
    }
}
else {
    $content = $content.Replace($oldStatus, $newStatus.TrimEnd())
}

if ($content -eq $original) {
    Write-Host 'Program.cs ya parece tener habilitado el matching. No hubo cambios.' -ForegroundColor Yellow
    exit 0
}

# Escritura segura: primero genera un archivo temporal completo y luego hace
# un reemplazo atómico. Si Program.cs está bloqueado, el reemplazo falla pero
# el archivo original NO se trunca ni se pierde.
try {
    [System.IO.File]::WriteAllText(
        $tempPath,
        $content,
        [System.Text.UTF8Encoding]::new($false))

    if (Test-Path $backupPath) {
        Remove-Item $backupPath -Force
    }

    [System.IO.File]::Replace($tempPath, $programPath, $backupPath, $true)
}
catch {
    if (Test-Path $tempPath) {
        Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
    }

    throw "No pude reemplazar Program.cs de forma segura. El original se conserva. Detén el Listener/VS si lo está bloqueando y vuelve a intentar. Detalle: $($_.Exception.Message)"
}

Write-Host 'OK: Program.cs actualizado a Radar v0.8.0-matching.' -ForegroundColor Green
Write-Host "Backup: $backupPath"
Write-Host 'Revisa con: git diff -- RSMaps.Radar.Listener/Program.cs'
