param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)
$nl = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

$stateAnchor = @(
    'var enviosConfirmadosPorSolicitud = new HashSet<string>(StringComparer.OrdinalIgnoreCase);',
    '',
    'IRadarInterpreter interpreter = RadarInterpreterFactory.Create();'
) -join $nl

$stateReplacement = @(
    'var enviosConfirmadosPorSolicitud = new HashSet<string>(StringComparer.OrdinalIgnoreCase);',
    '',
    '// LAB-only state used to simulate one failed delivery followed by recovery.',
    'var entregasLabFalladasUnaVez = new HashSet<string>(StringComparer.OrdinalIgnoreCase);',
    '',
    'IRadarInterpreter interpreter = RadarInterpreterFactory.Create();'
) -join $nl

$oldSafeBlock = @(
    '                    if (RadarSettings.ModoSeguroLab)',
    '                    {',
    '                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");',
    '                        continue;',
    '                    }',
    '',
    '                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);',
    '                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))',
    '                    {',
    '                        Console.WriteLine("  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.");',
    '                        continue;',
    '                    }',
    '',
    '                    var envio = await EnviarAlerta(page, solicitud);'
) -join $nl

$newSafeBlock = @(
    '                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);',
    '                    var pruebaEntregaLab = Environment.GetEnvironmentVariable("RADAR_SAFE_LAB_DELIVERY_TEST")?.Trim();',
    '                    var simularFailOnce = RadarSettings.ModoSeguroLab',
    '                        && string.Equals(pruebaEntregaLab, "fail-once", StringComparison.OrdinalIgnoreCase);',
    '',
    '                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)',
    '                    {',
    '                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");',
    '                        continue;',
    '                    }',
    '',
    '                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))',
    '                    {',
    '                        Console.WriteLine("  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.");',
    '                        continue;',
    '                    }',
    '',
    '                    (bool Enviada, bool MarcadoNoLeido, string Detalle) envio;',
    '',
    '                    if (simularFailOnce)',
    '                    {',
    '                        if (entregasLabFalladasUnaVez.Add(claveEntrega))',
    '                        {',
    '                            envio = (false, false, "SAFE LAB simulated first delivery failure");',
    '                            Console.WriteLine("  [SAFE LAB TEST] First delivery attempt intentionally failed; no WhatsApp message was sent.");',
    '                        }',
    '                        else',
    '                        {',
    '                            envio = (true, true, "SAFE LAB simulated delivery recovery");',
    '                            Console.WriteLine("  [SAFE LAB TEST] Retry delivery intentionally succeeded; no WhatsApp message was sent.");',
    '                        }',
    '                    }',
    '                    else',
    '                    {',
    '                        envio = await EnviarAlerta(page, solicitud);',
    '                    }'
) -join $nl

$alreadyApplied = $content.Contains('RADAR_SAFE_LAB_DELIVERY_TEST') -and
    $content.Contains('entregasLabFalladasUnaVez')

if ($alreadyApplied)
{
    Write-Host "RADAR SAFE LAB fail-once delivery simulation is already applied."
    exit 0
}

if (!$content.Contains($stateAnchor))
{
    throw "Could not find delivery-state anchor in Program.cs."
}

if (!$content.Contains($oldSafeBlock))
{
    throw "Could not find current SAFE LAB delivery block in Program.cs."
}

$content = $content.Replace($stateAnchor, $stateReplacement)
$content = $content.Replace($oldSafeBlock, $newSafeBlock)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $content, $utf8NoBom)

Write-Host "RADAR SAFE LAB fail-once delivery simulation applied successfully."
Write-Host "Use RADAR_SAFE_LAB=1 and RADAR_SAFE_LAB_DELIVERY_TEST=fail-once."
Write-Host "First delivery attempt will fail synthetically; next retry will succeed synthetically."
Write-Host "No WhatsApp alert will be sent while this LAB simulation is active."
