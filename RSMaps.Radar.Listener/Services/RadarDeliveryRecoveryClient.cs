using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingDeliveryClientItem
{
    public long IdRadarMessageDelivery { get; init; }
    public string ChatOrigen { get; init; } = "";
    public string MessageId { get; init; } = "";
    public int SolicitudIndice { get; init; }
    public string ClaveEntrega { get; init; } = "";
    public int? IdInmueble { get; init; }
    public int? Puntuacion { get; init; }
    public string Estado { get; init; } = "";
    public int IntentosEntrega { get; init; }
    public string? PayloadAlerta { get; init; }
}

public sealed class RadarPendingDeliveryClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingDeliveryClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = "";
}

public static class RadarDeliveryRecoveryClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingDeliveryClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingDeliveryClientResult
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
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/delivery/agent/pending?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingDeliveryClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(errorBody, 220)}"
                };
            }

            List<RadarPendingDeliveryClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingDeliveryClientItem>>(cancellationToken: cancellationToken);

            return new RadarPendingDeliveryClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingDeliveryClientResult
            {
                Ok = false,
                Detalle = "Pending durable delivery query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingDeliveryClientResult
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
}