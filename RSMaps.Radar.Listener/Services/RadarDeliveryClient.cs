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