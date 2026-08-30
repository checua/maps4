param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$programPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
$servicePath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Services\RadarDeliveryClient.cs'

if (-not (Test-Path -LiteralPath $programPath)) {
    throw "Program.cs not found: $programPath"
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$program = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $programPath))

$oldBlock = @'
                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);
                    var pruebaEntregaLab = Environment.GetEnvironmentVariable("RADAR_SAFE_LAB_DELIVERY_TEST")?.Trim();
                    var simularFailOnce = RadarSettings.ModoSeguroLab
                        && string.Equals(pruebaEntregaLab, "fail-once", StringComparison.OrdinalIgnoreCase);

                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)
                    {
                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");
                        continue;
                    }

                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))
                    {
                        Console.WriteLine("  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.");
                        continue;
                    }

                    (bool Enviada, bool MarcadoNoLeido, string Detalle) envio;

                    if (simularFailOnce)
                    {
                        if (entregasLabFalladasUnaVez.Add(claveEntrega))
                        {
                            envio = (false, false, "SAFE LAB simulated first delivery failure");
                            Console.WriteLine("  [SAFE LAB TEST] First delivery attempt intentionally failed; no WhatsApp message was sent.");
                        }
                        else
                        {
                            envio = (true, true, "SAFE LAB simulated delivery recovery");
                            Console.WriteLine("  [SAFE LAB TEST] Retry delivery intentionally succeeded; no WhatsApp message was sent.");
                        }
                    }
                    else
                    {
                        envio = await EnviarAlerta(page, solicitud);
                    }

                    if (!envio.Enviada)
                    {
                        mensajeCompletado = false;
                        Console.WriteLine(
                            $"  [PENDING] Could not deliver alert to {AlertSettings.ChatDestino}. Stage: {envio.Detalle}. Message will be retried.");
                        break;
                    }

                    enviosConfirmadosPorSolicitud.Add(claveEntrega);
'@

$newBlock = @'
                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);
                    var pruebaEntregaLab = Environment.GetEnvironmentVariable("RADAR_SAFE_LAB_DELIVERY_TEST")?.Trim();
                    var simularFailOnce = RadarSettings.ModoSeguroLab
                        && string.Equals(pruebaEntregaLab, "fail-once", StringComparison.OrdinalIgnoreCase);

                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)
                    {
                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");
                        continue;
                    }

                    RadarDeliveryPrepareClientResult? entregaDurable = null;
                    if (RadarDeliveryClient.Habilitada)
                    {
                        entregaDurable = await RadarDeliveryClient.PrepararAsync(
                            solicitud.ChatOrigen,
                            messageId,
                            indice,
                            claveEntrega,
                            solicitud.IdInmuebleCoincidente,
                            solicitud.MejorCoincidencia,
                            solicitud.MensajeOriginal + Environment.NewLine + Environment.NewLine + solicitud.MatchingResumen);

                        if (!entregaDurable.Ok)
                        {
                            mensajeCompletado = false;
                            Console.WriteLine(
                                $"  [PENDING] Durable delivery could not be prepared: {entregaDurable.Detalle}. Message will be retried.");
                            break;
                        }

                        if (entregaDurable.YaEnviado)
                        {
                            enviosConfirmadosPorSolicitud.Add(claveEntrega);
                            Console.WriteLine(
                                $"  [DEDUP DURABLE] Delivery #{entregaDurable.IdRadarMessageDelivery} was already confirmed by RSMaps; duplicate WhatsApp send skipped.");
                            continue;
                        }

                        Console.WriteLine(
                            $"  [DELIVERY] Durable delivery #{entregaDurable.IdRadarMessageDelivery} prepared · attempt {entregaDurable.IntentosEntrega}.");
                    }

                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))
                    {
                        if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                        {
                            var confirmacionPendiente = await RadarDeliveryClient.CompletarAsync(
                                entregaDurable.IdRadarMessageDelivery,
                                true,
                                null);

                            if (!confirmacionPendiente.Ok)
                            {
                                mensajeCompletado = false;
                                Console.WriteLine(
                                    $"  [PENDING] Alert was already delivered locally, but durable confirmation is still pending: {confirmacionPendiente.Detalle}.");
                                break;
                            }

                            Console.WriteLine(
                                $"  [DEDUP] Previous local delivery confirmed durably as #{entregaDurable.IdRadarMessageDelivery}; duplicate send skipped.");
                            continue;
                        }

                        Console.WriteLine("  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.");
                        continue;
                    }

                    (bool Enviada, bool MarcadoNoLeido, string Detalle) envio;

                    if (simularFailOnce)
                    {
                        if (entregasLabFalladasUnaVez.Add(claveEntrega))
                        {
                            envio = (false, false, "SAFE LAB simulated first delivery failure");
                            Console.WriteLine("  [SAFE LAB TEST] First delivery attempt intentionally failed; no WhatsApp message was sent.");
                        }
                        else
                        {
                            envio = (true, true, "SAFE LAB simulated delivery recovery");
                            Console.WriteLine("  [SAFE LAB TEST] Retry delivery intentionally succeeded; no WhatsApp message was sent.");
                        }
                    }
                    else
                    {
                        envio = await EnviarAlerta(page, solicitud);
                    }

                    if (!envio.Enviada)
                    {
                        if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                        {
                            var falloDurable = await RadarDeliveryClient.CompletarAsync(
                                entregaDurable.IdRadarMessageDelivery,
                                false,
                                envio.Detalle);

                            if (!falloDurable.Ok)
                            {
                                Console.WriteLine(
                                    $"  [WARN] Could not persist delivery failure: {falloDurable.Detalle}");
                            }
                        }

                        mensajeCompletado = false;
                        Console.WriteLine(
                            $"  [PENDING] Could not deliver alert to {AlertSettings.ChatDestino}. Stage: {envio.Detalle}. Message will be retried.");
                        break;
                    }

                    enviosConfirmadosPorSolicitud.Add(claveEntrega);

                    if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                    {
                        var confirmacionDurable = await RadarDeliveryClient.CompletarAsync(
                            entregaDurable.IdRadarMessageDelivery,
                            true,
                            null);

                        if (!confirmacionDurable.Ok)
                        {
                            mensajeCompletado = false;
                            Console.WriteLine(
                                $"  [PENDING] WhatsApp delivery succeeded, but durable confirmation failed: {confirmacionDurable.Detalle}. No duplicate will be sent during this process.");
                            break;
                        }

                        Console.WriteLine(
                            $"  [DELIVERY] Durable delivery #{entregaDurable.IdRadarMessageDelivery} confirmed ENVIADO.");
                    }
'@

if (-not $program.Contains($oldBlock)) {
    if ($program.Contains('[DEDUP DURABLE]')) {
        Write-Host 'Program.cs already contains durable delivery integration.'
    }
    else {
        throw 'Expected delivery block was not found in Program.cs. The helper made no Program.cs changes.'
    }
}
else {
    $program = $program.Replace($oldBlock, $newBlock)
    [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $programPath), $program, $utf8NoBom)
}

$serviceContent = @'
using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarDeliveryPrepareClientResult
{
    public bool Ok { get; init; }
    public long IdRadarMessageDelivery { get; init; }
    public string Estado { get; init; } = "";
    public bool YaEnviado { get; init; }
    public int IntentosEntrega { get; init; }
    public string Detalle { get; init; } = "";
}

public sealed class RadarDeliveryCompleteClientResult
{
    public bool Ok { get; init; }
    public long IdRadarMessageDelivery { get; init; }
    public string Estado { get; init; } = "";
    public bool YaEnviado { get; init; }
    public int IntentosEntrega { get; init; }
    public string Detalle { get; init; } = "";
}

public static class RadarDeliveryClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static bool Habilitada => RadarCentralIntelligenceClient.Habilitada;

    public static async Task<RadarDeliveryPrepareClientResult> PrepararAsync(
        string chatOrigen,
        string messageId,
        int solicitudIndice,
        string claveEntrega,
        int? idInmueble,
        double? puntuacion,
        string? payloadAlerta,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarDeliveryPrepareClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            int? puntuacionEntera = puntuacion.HasValue
                ? Math.Clamp((int)Math.Round(puntuacion.Value, MidpointRounding.AwayFromZero), 0, 100)
                : null;

            var body = new RadarDeliveryPrepareRequestDto
            {
                ChatOrigen = chatOrigen,
                MessageId = messageId,
                SolicitudIndice = solicitudIndice,
                ClaveEntrega = claveEntrega,
                IdInmueble = idInmueble,
                Puntuacion = puntuacionEntera,
                PayloadAlerta = payloadAlerta
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/delivery/agent/prepare");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(body);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarDeliveryPrepareClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(errorBody, 220)}"
                };
            }

            RadarDeliveryPrepareResponseDto? dto = await response.Content.ReadFromJsonAsync<RadarDeliveryPrepareResponseDto>(
                cancellationToken: cancellationToken);

            if (dto is null || dto.IdRadarMessageDelivery <= 0)
            {
                return new RadarDeliveryPrepareClientResult
                {
                    Ok = false,
                    Detalle = "RSMaps returned an invalid durable delivery response."
                };
            }

            return new RadarDeliveryPrepareClientResult
            {
                Ok = true,
                IdRadarMessageDelivery = dto.IdRadarMessageDelivery,
                Estado = dto.Estado ?? "",
                YaEnviado = dto.YaEnviado,
                IntentosEntrega = dto.IntentosEntrega,
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarDeliveryPrepareClientResult
            {
                Ok = false,
                Detalle = "Durable delivery prepare timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarDeliveryPrepareClientResult
            {
                Ok = false,
                Detalle = "RSMaps unavailable: " + Recortar(ex.Message, 180)
            };
        }
        finally
        {
            token = string.Empty;
        }
    }

    public static async Task<RadarDeliveryCompleteClientResult> CompletarAsync(
        long idRadarMessageDelivery,
        bool enviada,
        string? error,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarDeliveryCompleteClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            var body = new RadarDeliveryCompleteRequestDto
            {
                IdRadarMessageDelivery = idRadarMessageDelivery,
                Enviada = enviada,
                Error = error
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/delivery/agent/complete");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(body);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarDeliveryCompleteClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(errorBody, 220)}"
                };
            }

            RadarDeliveryCompleteResponseDto? dto = await response.Content.ReadFromJsonAsync<RadarDeliveryCompleteResponseDto>(
                cancellationToken: cancellationToken);

            if (dto is null || dto.IdRadarMessageDelivery <= 0)
            {
                return new RadarDeliveryCompleteClientResult
                {
                    Ok = false,
                    Detalle = "RSMaps returned an invalid durable delivery confirmation."
                };
            }

            return new RadarDeliveryCompleteClientResult
            {
                Ok = true,
                IdRadarMessageDelivery = dto.IdRadarMessageDelivery,
                Estado = dto.Estado ?? "",
                YaEnviado = dto.YaEnviado,
                IntentosEntrega = dto.IntentosEntrega,
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarDeliveryCompleteClientResult
            {
                Ok = false,
                Detalle = "Durable delivery confirmation timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarDeliveryCompleteClientResult
            {
                Ok = false,
                Detalle = "RSMaps unavailable: " + Recortar(ex.Message, 180)
            };
        }
        finally
        {
            token = string.Empty;
        }
    }

    private static string Recortar(string texto, int max)
    {
        texto = (texto ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "...";
    }

    private sealed class RadarDeliveryPrepareRequestDto
    {
        public string ChatOrigen { get; set; } = "";
        public string MessageId { get; set; } = "";
        public int SolicitudIndice { get; set; }
        public string ClaveEntrega { get; set; } = "";
        public int? IdInmueble { get; set; }
        public int? Puntuacion { get; set; }
        public string? PayloadAlerta { get; set; }
    }

    private sealed class RadarDeliveryPrepareResponseDto
    {
        public long IdRadarMessageDelivery { get; set; }
        public string? Estado { get; set; }
        public bool YaEnviado { get; set; }
        public int IntentosEntrega { get; set; }
    }

    private sealed class RadarDeliveryCompleteRequestDto
    {
        public long IdRadarMessageDelivery { get; set; }
        public bool Enviada { get; set; }
        public string? Error { get; set; }
    }

    private sealed class RadarDeliveryCompleteResponseDto
    {
        public long IdRadarMessageDelivery { get; set; }
        public string? Estado { get; set; }
        public bool YaEnviado { get; set; }
        public int IntentosEntrega { get; set; }
    }
}
'@

$serviceDirectory = Split-Path -Parent $servicePath
if (-not (Test-Path -LiteralPath $serviceDirectory)) {
    New-Item -ItemType Directory -Path $serviceDirectory -Force | Out-Null
}
[System.IO.File]::WriteAllText($servicePath, $serviceContent, $utf8NoBom)

Write-Host 'RADAR durable delivery Agent integration applied successfully.'
Write-Host 'Central mode now prepares and confirms alert deliveries through RSMaps.'
Write-Host 'SAFE LAB fail-once writes failure/retry/success states without sending WhatsApp.'
Write-Host 'If RSMaps delivery persistence is unavailable, the Agent keeps the message pending and does not send.'
