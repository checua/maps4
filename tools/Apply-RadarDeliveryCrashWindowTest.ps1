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

$anchor = '                        envio = await EnviarAlertaPayload(page, payloadEntrega, solicitud.ChatOrigen);'
$marker = '[UNCERTAIN TEST] WhatsApp send succeeded; terminating Agent before durable ENVIADO confirmation.'

$replacement = @'
                        envio = await EnviarAlertaPayload(page, payloadEntrega, solicitud.ChatOrigen);

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

if ($content.Contains($marker)) {
    Write-Host 'RADAR uncertain-send crash window test hook is already applied.'
}
else {
    if (-not $content.Contains($anchor)) {
        throw 'Could not locate the live EnviarAlertaPayload call. Apply the uncertain-send reconciliation patch first.'
    }

    $content = $content.Replace($anchor, $replacement.TrimEnd("`r", "`n"))
    [System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)
}

Write-Host 'RADAR uncertain-send crash window test hook applied successfully.'
Write-Host 'It activates only when RADAR_UNCERTAIN_SEND_TEST=crash-after-send.'
Write-Host 'It activates only for a message containing PRUEBA RADAR CENTRAL 010.'
Write-Host 'After WhatsApp reports a successful send, the Agent exits with code 86 before SQL ENVIADO confirmation.'
Write-Host 'Restart recovery should then find the RADAR-DELIVERY marker and reconcile without resending.'
