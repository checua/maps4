param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs",
    [string]$MatchingClientPath = ".\RSMaps.Radar.Listener\Services\RsMapsMatchingClient.cs"
)

$ErrorActionPreference = "Stop"

function Read-Text([string]$Path) {
    $resolved = Resolve-Path -LiteralPath $Path
    return [System.IO.File]::ReadAllText($resolved)
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    $resolved = Resolve-Path -LiteralPath $Path
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($resolved, $Content, $utf8NoBom)
}

$program = Read-Text $ProgramPath
$nl = if ($program.Contains("`r`n")) { "`r`n" } else { "`n" }

if ($program.Contains("DemandasInterpretadas") -and
    $program.Contains("enviosConfirmadosPorSolicitud") -and
    $program.Contains("TieneCoincidenciaUtil")) {
    Write-Host "RADAR end-to-end acknowledgement patch is already applied."
    exit 0
}

$stateAnchor = @(
    "var idsConocidosPorChat = new Dictionary<string, HashSet<string>>(",
    "    StringComparer.OrdinalIgnoreCase);",
    "",
    "IRadarInterpreter interpreter = RadarInterpreterFactory.Create();"
) -join $nl

$stateReplacement = @(
    "var idsConocidosPorChat = new Dictionary<string, HashSet<string>>(",
    "    StringComparer.OrdinalIgnoreCase);",
    "",
    "// Tracks successful partial deliveries while a multi-request message is pending.",
    "// This prevents re-sending already confirmed alerts during an in-process retry.",
    "var enviosConfirmadosPorSolicitud = new HashSet<string>(StringComparer.OrdinalIgnoreCase);",
    "",
    "IRadarInterpreter interpreter = RadarInterpreterFactory.Create();"
) -join $nl

if (!$program.Contains($stateAnchor)) {
    throw "Could not find RADAR local state anchor in Program.cs."
}
$program = $program.Replace($stateAnchor, $stateReplacement)

$loopStartMarker = "            foreach (var solicitud in resultado.Solicitudes)"
$loopSuffixMarker = "${nl}        }${nl}    }${nl}    catch (PlaywrightException ex)"
$loopStart = $program.IndexOf($loopStartMarker, [System.StringComparison]::Ordinal)
if ($loopStart -lt 0) {
    throw "Could not find current per-solicitud processing loop in Program.cs."
}
$loopEnd = $program.IndexOf($loopSuffixMarker, $loopStart, [System.StringComparison]::Ordinal)
if ($loopEnd -lt 0) {
    throw "Could not find end of current per-solicitud processing loop in Program.cs."
}

$newLoop = @(
    "            foreach (var messageId in resultado.DemandasInterpretadas)",
    "            {",
    "                var solicitudesMensaje = resultado.Solicitudes",
    "                    .Where(x => string.Equals(x.MessageId, messageId, StringComparison.OrdinalIgnoreCase))",
    "                    .ToList();",
    "",
    "                if (solicitudesMensaje.Count == 0)",
    "                {",
    "                    idsConocidos.Add(messageId);",
    "                    Console.WriteLine($\"  [ACK] {messageId}: Intelligence finished with no actionable requests.\");",
    "                    continue;",
    "                }",
    "",
    "                var mensajeCompletado = true;",
    "",
    "                for (var indice = 0; indice < solicitudesMensaje.Count; indice++)",
    "                {",
    "                    var solicitud = solicitudesMensaje[indice];",
    "                    MostrarSolicitud(solicitud);",
    "",
    "                    solicitud.MatchingResumen = await RsMapsMatchingClient.ConstruirResumenAsync(solicitud);",
    "                    Console.WriteLine(",
    "                        $\"  MATCH RSMAPS: {solicitud.MatchingResumen.Replace(\"\\r\", \" \" ).Replace(\"\\n\", \" | \" )}\");",
    "",
    "                    if (MatchingTemporalmenteNoDisponible(solicitud))",
    "                    {",
    "                        mensajeCompletado = false;",
    "                        Console.WriteLine(\"  [PENDING] Matching is not confirmed; message will be retried.\");",
    "                        break;",
    "                    }",
    "",
    "                    if (!TieneCoincidenciaUtil(solicitud))",
    "                    {",
    "                        Console.WriteLine(\"  [NO ALERT] No useful match; WhatsApp alert is not sent.\");",
    "                        continue;",
    "                    }",
    "",
    "                    if (RadarSettings.ModoSeguroLab)",
    "                    {",
    "                        Console.WriteLine(\"  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.\");",
    "                        continue;",
    "                    }",
    "",
    "                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);",
    "                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))",
    "                    {",
    "                        Console.WriteLine(\"  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.\");",
    "                        continue;",
    "                    }",
    "",
    "                    var envio = await EnviarAlerta(page, solicitud);",
    "",
    "                    if (!envio.Enviada)",
    "                    {",
    "                        mensajeCompletado = false;",
    "                        Console.WriteLine(",
    "                            $\"  [PENDING] Could not deliver alert to '{AlertSettings.ChatDestino}'. Stage: {envio.Detalle}. Message will be retried.\");",
    "                        break;",
    "                    }",
    "",
    "                    enviosConfirmadosPorSolicitud.Add(claveEntrega);",
    "",
    "                    if (envio.MarcadoNoLeido)",
    "                    {",
    "                        Console.WriteLine(",
    "                            $\"  [SENT] Alert delivered to '{AlertSettings.ChatDestino}', returned to origin and marked unread.\");",
    "                    }",
    "                    else",
    "                    {",
    "                        Console.WriteLine(",
    "                            $\"  [SENT] Alert delivered to '{AlertSettings.ChatDestino}'. Could not mark it unread.\");",
    "                    }",
    "                }",
    "",
    "                if (mensajeCompletado)",
    "                {",
    "                    idsConocidos.Add(messageId);",
    "                    var prefijo = messageId + \":\";",
    "                    enviosConfirmadosPorSolicitud.RemoveWhere(",
    "                        x => x.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase));",
    "                    Console.WriteLine($\"  [ACK] {messageId}: terminal processing completed.\");",
    "                }",
    "                else",
    "                {",
    "                    Console.WriteLine($\"  [PENDING] {messageId}: not acknowledged; retry remains enabled.\");",
    "                }",
    "            }"
) -join $nl

$program = $program.Substring(0, $loopStart) + $newLoop + $program.Substring($loopEnd)

$oldSignature = "static async Task<(int Revisados, List<SolicitudInmobiliaria> Solicitudes)> ProcesarMensajesNuevos("
$newSignature = "static async Task<(int Revisados, List<SolicitudInmobiliaria> Solicitudes, HashSet<string> DemandasInterpretadas)> ProcesarMensajesNuevos("
if (!$program.Contains($oldSignature)) {
    throw "Could not find ProcesarMensajesNuevos signature."
}
$program = $program.Replace($oldSignature, $newSignature)

$requestsAnchor = "    var solicitudes = new List<SolicitudInmobiliaria>();"
$requestsReplacement = @(
    "    var solicitudes = new List<SolicitudInmobiliaria>();",
    "    var demandasInterpretadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);"
) -join $nl
if (!$program.Contains($requestsAnchor)) {
    throw "Could not find request list initialization."
}
$program = $program.Replace($requestsAnchor, $requestsReplacement)

$oldInterpretationAck = @(
    "        // Demand messages are acknowledged only after Intelligence returns successfully.",
    "        var interpretacion = await interpreter.InterpretarAsync(radarMessage);",
    "        knownIds.Add(id);",
    "        revisados++;"
) -join $nl
$newInterpretationAck = @(
    "        // Demand messages are only acknowledged after all terminal downstream work finishes.",
    "        var interpretacion = await interpreter.InterpretarAsync(radarMessage);",
    "        demandasInterpretadas.Add(id);",
    "        revisados++;"
) -join $nl
if (!$program.Contains($oldInterpretationAck)) {
    throw "Could not find current Intelligence acknowledgement block."
}
$program = $program.Replace($oldInterpretationAck, $newInterpretationAck)

$oldReturn = "    return (revisados, solicitudes);"
$newReturn = "    return (revisados, solicitudes, demandasInterpretadas);"
if (!$program.Contains($oldReturn)) {
    throw "Could not find ProcesarMensajesNuevos return statement."
}
$program = $program.Replace($oldReturn, $newReturn)

$sendMarker = "static async Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta("
if (!$program.Contains($sendMarker)) {
    throw "Could not find EnviarAlerta marker."
}

$helpers = @(
    "static bool MatchingTemporalmenteNoDisponible(SolicitudInmobiliaria solicitud)",
    "{",
    "    if (string.IsNullOrWhiteSpace(solicitud.MatchingResumen))",
    "        return true;",
    "",
    "    // Warning summaries represent a transient/operational matching failure, not a terminal no-match.",
    "    return solicitud.MatchingResumen.Contains('\\u26A0');",
    "}",
    "",
    "static bool TieneCoincidenciaUtil(SolicitudInmobiliaria solicitud)",
    "{",
    "    // Keep this aligned with RadarMatchingService.PuntuacionMinimaCandidato.",
    "    return solicitud.IdInmuebleCoincidente.HasValue",
    "        && solicitud.MejorCoincidencia.HasValue",
    "        && solicitud.MejorCoincidencia.Value >= 55;",
    "}",
    "",
    "static string ClaveEntrega(string messageId, int indice, SolicitudInmobiliaria solicitud) =>",
    "    $\"{messageId}:{indice}:{solicitud.IdInmuebleCoincidente?.ToString() ?? \"-\"}\";",
    "",
    $sendMarker
) -join $nl

$program = $program.Replace($sendMarker, $helpers)
Write-Utf8NoBom $ProgramPath $program

$matching = Read-Text $MatchingClientPath
$matchingNl = if ($matching.Contains("`r`n")) { "`r`n" } else { "`n" }

if (!$matching.Contains("solicitud.MejorCoincidencia = mejor.Puntuacion;")) {
    $candidateAnchor = "            if (resultado.TotalCandidatos <= 0 || resultado.Resultados.Count == 0)"
    if (!$matching.Contains($candidateAnchor)) {
        throw "Could not find matching candidate anchor in RsMapsMatchingClient.cs."
    }

    $candidateInsert = @(
        "            var mejor = resultado.Resultados",
        "                .OrderByDescending(x => x.Puntuacion)",
        "                .FirstOrDefault();",
        "",
        "            if (mejor is not null)",
        "            {",
        "                solicitud.MejorCoincidencia = mejor.Puntuacion;",
        "                solicitud.IdInmuebleCoincidente = mejor.IdInmueble;",
        "            }",
        "",
        $candidateAnchor
    ) -join $matchingNl

    $matching = $matching.Replace($candidateAnchor, $candidateInsert)
}

$timeoutPattern = 'catch \(TaskCanceledException\)\s*\{\s*return "COINCIDENCIAS RSMAPS\\n[^\r\n]*";\s*\}'
$timeoutReplacement = @(
    "catch (TaskCanceledException)",
    "        {",
    "            return \"COINCIDENCIAS RSMAPS\\n\\u26A0 La comparaci\\u00F3n excedi\\u00F3 el tiempo de espera; no se enviar\\u00E1 alerta hasta confirmar matching.\";",
    "        }"
) -join $matchingNl

$matchingUpdated = [System.Text.RegularExpressions.Regex]::Replace(
    $matching,
    $timeoutPattern,
    $timeoutReplacement,
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

if ($matchingUpdated -eq $matching -and $matching.Contains("la alerta se env")) {
    throw "Could not replace unsafe timeout message in RsMapsMatchingClient.cs."
}
$matching = $matchingUpdated

Write-Utf8NoBom $MatchingClientPath $matching

Write-Host "RADAR end-to-end acknowledgement patch applied successfully."
Write-Host "Demand IDs are acknowledged only after terminal matching/delivery handling."
Write-Host "No useful match means no WhatsApp alert. Matching warnings remain pending for retry."
Write-Host "Partial successful deliveries are de-duplicated during in-process retries."
