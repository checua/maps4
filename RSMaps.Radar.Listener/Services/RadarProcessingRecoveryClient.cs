using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingProcessingClientItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public int IntentosProcesamiento { get; init; }
    public DateTime DetectadoUtc { get; init; }
    public DateTime? ReintentarDespuesUtc { get; init; }
    public DateTime? LeaseHastaUtc { get; init; }
}

public sealed class RadarPendingProcessingClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingProcessingClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = string.Empty;
}

public static class RadarProcessingRecoveryClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingProcessingClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        if (!RadarCentralIntelligenceClient.Habilitada)
        {
            return new RadarPendingProcessingClientResult
            {
                Ok = true,
                Detalle = "Central Intelligence is disabled."
            };
        }

        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingProcessingClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            int limite = Math.Clamp(max, 1, 100);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/pending?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingProcessingClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            List<RadarPendingProcessingClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingProcessingClientItem>>(
                    cancellationToken: cancellationToken);

            return new RadarPendingProcessingClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingProcessingClientResult
            {
                Ok = false,
                Detalle = "Pending durable processing query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingProcessingClientResult
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
        texto = (texto ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "...";
    }
}