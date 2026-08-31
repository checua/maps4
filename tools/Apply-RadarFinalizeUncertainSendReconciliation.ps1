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

if (-not $content.Contains('[RECOVERY RECONCILED]')) {
    throw 'Uncertain-send reconciliation is not present in Listener Program.cs.'
}
if (-not $content.Contains('static string MarcaEntrega(string claveEntrega)')) {
    throw 'RADAR delivery marker support is not present in Listener Program.cs.'
}

# Remove the CENTRAL 010 exclusive-send guard, leaving the real payload send.
$exclusiveBlock = @'
                        string? uncertainSendMode = Environment.GetEnvironmentVariable("RADAR_UNCERTAIN_SEND_TEST")?.Trim();
                        bool central010ExclusiveMode = string.Equals(
                            uncertainSendMode,
                            "crash-after-send",
                            StringComparison.OrdinalIgnoreCase);
                        bool isCentral010 = solicitud.MensajeOriginal.Contains(
                            "PRUEBA RADAR CENTRAL 010",
                            StringComparison.OrdinalIgnoreCase);

                        // While the live uncertain-send test is armed, only the explicit CENTRAL 010
                        // test message may produce a real WhatsApp send. Any unrelated useful match
                        // remains pending so it can be processed normally after the test mode is removed.
                        if (central010ExclusiveMode && !isCentral010)
                        {
                            mensajeCompletado = false;
                            Console.WriteLine(
                                "  [UNCERTAIN TEST BLOCK] Real delivery blocked while CENTRAL 010 exclusive-send test is armed. Message remains pending.");
                            break;
                        }

                        envio = await EnviarAlertaPayload(page, payloadEntrega, solicitud.ChatOrigen);
'@

$normalSend = '                        envio = await EnviarAlertaPayload(page, payloadEntrega, solicitud.ChatOrigen);'
if ($content.Contains($exclusiveBlock)) {
    $content = $content.Replace($exclusiveBlock, $normalSend)
}

# Remove the live crash-after-send diagnostic hook.
$liveCrashBlock = @'

                        // LAB/diagnostic hook for the classic uncertain-send window:
                        // WhatsApp accepted the alert, but the Agent dies before RSMaps receives ENVIADO.
                        string? uncertainTest = Environment.GetEnvironmentVariable("RADAR_UNCERTAIN_SEND_TEST")?.Trim();
                        bool crashAfterSend =
                            string.Equals(uncertainTest, "crash-after-send", StringComparison.OrdinalIgnoreCase) &&
                            solicitud.MensajeOriginal.Contains(
                                "PRUEBA RADAR CENTRAL 010",
                                StringComparison.OrdinalIgnoreCase);

                        if (crashAfterSend && envio.Enviada)
                        {
                            Console.WriteLine(
                                "  [UNCERTAIN TEST] WhatsApp send succeeded; terminating Agent before durable ENVIADO confirmation.");
                            Console.Out.Flush();
                            Environment.Exit(86);
                        }
'@
if ($content.Contains($liveCrashBlock)) {
    $content = $content.Replace($liveCrashBlock, '')
}

# Remove the recovery crash-after-send diagnostic hook.
$recoveryCrashBlock = @'

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
'@
if ($content.Contains($recoveryCrashBlock)) {
    $content = $content.Replace($recoveryCrashBlock, '')
}

if ($content.Contains('RADAR_UNCERTAIN_SEND_TEST') -or
    $content.Contains('[UNCERTAIN TEST]') -or
    $content.Contains('[UNCERTAIN TEST RECOVERY]') -or
    $content.Contains('[UNCERTAIN TEST BLOCK]')) {
    throw 'One or more CENTRAL 010 diagnostic hooks are still present after cleanup.'
}

[System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)

Write-Host 'RADAR uncertain-send production reconciliation finalized successfully.'
Write-Host 'CENTRAL 010 crash and exclusive-send test hooks were removed.'
Write-Host 'Deterministic RADAR-DELIVERY markers and fail-closed restart reconciliation remain active.'
Write-Host 'Destination navigation for the alert chat remains active.'
Write-Host 'No SQL migration is required.'
