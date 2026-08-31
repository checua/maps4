param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$programPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
if (-not (Test-Path -LiteralPath $programPath)) {
    throw "Listener Program.cs not found: $programPath"
}

$resolved = Resolve-Path -LiteralPath $programPath
$content = [System.IO.File]::ReadAllText($resolved)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# 1. AbrirChat normally restricts search terms to monitored source chats.
# The alert destination is intentionally not a monitored source, so allow the
# configured destination name as a navigation-only fallback.
$oldForeach = '    foreach (var termino in RadarSettings.ObtenerTerminosBusqueda(nombreChat))'
$newForeach = @'
    var terminosBusqueda = RadarSettings.ObtenerTerminosBusqueda(nombreChat).ToList();
    if (terminosBusqueda.Count == 0 &&
        string.Equals(nombreChat, AlertSettings.ChatDestino, StringComparison.OrdinalIgnoreCase))
    {
        terminosBusqueda.Add(nombreChat);
    }

    foreach (var termino in terminosBusqueda)
'@

if (-not $content.Contains('var terminosBusqueda = RadarSettings.ObtenerTerminosBusqueda(nombreChat).ToList();')) {
    if (-not $content.Contains($oldForeach)) {
        throw 'Could not locate AbrirChat search-term loop.'
    }

    $content = $content.Replace($oldForeach, $newForeach.TrimEnd("`r", "`n"))
}

# 2. Only PENDIENTE is an uncertain-send state. FALLIDO_REINTENTABLE means the
# previous Agent explicitly reported a failed send, so it is safe to retry
# without requiring a WhatsApp marker first.
$verificationLine = '        var verificacionEntrega = await VerificarEntregaEnDestinoAsync(page, pendiente.ClaveEntrega);'
$verificationWrapped = @'
        bool requiereReconciliacionIncierta = string.Equals(
            pendiente.Estado,
            "PENDIENTE",
            StringComparison.OrdinalIgnoreCase);

        if (requiereReconciliacionIncierta)
        {
            var verificacionEntrega = await VerificarEntregaEnDestinoAsync(page, pendiente.ClaveEntrega);
'@

if (-not $content.Contains('bool requiereReconciliacionIncierta = string.Equals(')) {
    if (-not $content.Contains($verificationLine)) {
        throw 'Could not locate uncertain-send verification block.'
    }

    $content = $content.Replace(
        $verificationLine,
        $verificationWrapped.TrimEnd("`r", "`n"))

    $prepareLine = '        RadarDeliveryPrepareClientResult preparada = await RadarDeliveryClient.PrepararAsync('
    $prepareWithClose = @'
        }

        RadarDeliveryPrepareClientResult preparada = await RadarDeliveryClient.PrepararAsync(
'@

    $prepareIndex = $content.IndexOf($prepareLine, [StringComparison]::Ordinal)
    if ($prepareIndex -lt 0) {
        throw 'Could not locate recovery prepare call.'
    }

    $content = $content.Remove($prepareIndex, $prepareLine.Length)
    $content = $content.Insert($prepareIndex, $prepareWithClose.TrimEnd("`r", "`n"))
}

# 3. Extend the CENTRAL 010 crash hook to restart recovery. This lets the
# already-created delivery exercise the exact window after navigation is fixed:
# WhatsApp send succeeds, Agent exits, SQL remains PENDIENTE, next restart
# reconciles the visible marker instead of sending again.
$recoverySendBlock = @'
        else
        {
            envio = await EnviarAlertaPayload(
                page,
                pendiente.PayloadAlerta,
                pendiente.ChatOrigen);
        }

        RadarDeliveryCompleteClientResult confirmacion = await RadarDeliveryClient.CompletarAsync(
'@

$recoverySendReplacement = @'
        else
        {
            envio = await EnviarAlertaPayload(
                page,
                pendiente.PayloadAlerta,
                pendiente.ChatOrigen);
        }

        if (envio.Enviada)
        {
            string? uncertainRecoveryTest = Environment.GetEnvironmentVariable(
                "RADAR_UNCERTAIN_SEND_TEST")?.Trim();
            bool crashAfterRecoveredSend =
                string.Equals(
                    uncertainRecoveryTest,
                    "crash-after-send",
                    StringComparison.OrdinalIgnoreCase) &&
                pendiente.PayloadAlerta.Contains(
                    "PRUEBA RADAR CENTRAL 010",
                    StringComparison.OrdinalIgnoreCase);

            if (crashAfterRecoveredSend)
            {
                Console.WriteLine(
                    "  [UNCERTAIN TEST RECOVERY] WhatsApp send succeeded during recovery; terminating Agent before durable ENVIADO confirmation.");
                Console.Out.Flush();
                Environment.Exit(86);
            }
        }

        RadarDeliveryCompleteClientResult confirmacion = await RadarDeliveryClient.CompletarAsync(
'@

if (-not $content.Contains('[UNCERTAIN TEST RECOVERY]')) {
    if (-not $content.Contains($recoverySendBlock)) {
        throw 'Could not locate recovered delivery send block.'
    }

    $content = $content.Replace($recoverySendBlock, $recoverySendReplacement)
}

[System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)

Write-Host 'RADAR destination navigation and CENTRAL 010 recovery fix applied successfully.'
Write-Host 'The configured alert destination can now be searched even though it is not a monitored source chat.'
Write-Host 'Only PENDIENTE is treated as an uncertain external send; explicit retryable failures may retry normally.'
Write-Host 'CENTRAL 010 can now crash after a successful recovered send and reconcile it on the next restart.'
Write-Host 'No SQL migration is required.'