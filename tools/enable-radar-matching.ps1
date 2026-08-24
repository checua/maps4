$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$programPath = Join-Path $repoRoot 'RSMaps.Radar.Listener\Program.cs'

if (-not (Test-Path $programPath)) {
    throw "No encontré Program.cs en $programPath"
}

$content = Get-Content $programPath -Raw -Encoding UTF8
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

Set-Content $programPath -Value $content -Encoding UTF8 -NoNewline
Write-Host 'OK: Program.cs actualizado a Radar v0.8.0-matching.' -ForegroundColor Green
Write-Host 'Revisa con: git diff -- RSMaps.Radar.Listener/Program.cs'
