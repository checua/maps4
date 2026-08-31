using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingWorkflowClientItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string? MotorInteligencia { get; init; }
    public string ResultadoCentralJson { get; init; } = string.Empty;
    public DateTime DetectadoUtc { get; init; }
    public DateTime? MatchingCompletadoUtc { get; init; }
}

public sealed class RadarPendingWorkflowClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingWorkflowClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = string.Empty;
}

public sealed class RadarTerminalAckClientResult
{
    public bool Ok { get; init; }
    public string Detalle { get; init; } = string.Empty;
}

public static class RadarWorkflowClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingWorkflowClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        if (!RadarCentralIntelligenceClient.Habilitada)
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = true,
                Detalle = "Central Intelligence is disabled."
            };
        }

        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingWorkflowClientResult
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
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/pending-downstream?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingWorkflowClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            List<RadarPendingWorkflowClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingWorkflowClientItem>>(
                    cancellationToken: cancellationToken);

            return new RadarPendingWorkflowClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = false,
                Detalle = "Pending terminal workflow query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingWorkflowClientResult
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

    public static async Task<RadarTerminalAckClientResult> ConfirmarTerminalAsync(
        string chatOrigen,
        string messageId,
        string disposicionTerminal,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarTerminalAckClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/terminal-ack");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                chatOrigen,
                messageId,
                disposicionTerminal
            });

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarTerminalAckClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            return new RadarTerminalAckClientResult
            {
                Ok = true,
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarTerminalAckClientResult
            {
                Ok = false,
                Detalle = "Terminal workflow ACK timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarTerminalAckClientResult
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