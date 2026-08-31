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
$marker = '[UNCERTAIN TEST BLOCK] Real delivery blocked while CENTRAL 010 exclusive-send test is armed.'

$replacement = @'
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

if ($content.Contains($marker)) {
    Write-Host 'RADAR CENTRAL 010 exclusive-send guard is already applied.'
}
else {
    if (-not $content.Contains($anchor)) {
        throw 'Could not locate the real EnviarAlertaPayload call. Apply prior CENTRAL 010 helpers first.'
    }

    $content = $content.Replace($anchor, $replacement.TrimEnd("`r", "`n"))
    [System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)
}

Write-Host 'RADAR CENTRAL 010 exclusive-send guard applied successfully.'
Write-Host 'When RADAR_UNCERTAIN_SEND_TEST=crash-after-send, only PRUEBA RADAR CENTRAL 010 may send a real WhatsApp alert.'
Write-Host 'Any unrelated useful match stays pending and is not acknowledged or sent during the test.'
