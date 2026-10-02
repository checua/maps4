using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public static class RadarCentralIntelligenceClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static bool Habilitada =>
        string.Equals(
            Environment.GetEnvironmentVariable("RADAR_INTELLIGENCE_MODE")?.Trim(),
            "central",
            StringComparison.OrdinalIgnoreCase);

    public static bool FallbackLocalHabilitado
    {
        get
        {
            string? valor = Environment.GetEnvironmentVariable("RADAR_CENTRAL_FALLBACK_LOCAL")?.Trim();
            if (string.Equals(valor, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valor, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Durante la transición conservamos fallback únicamente si la PC ya
            // tiene una API key local. Cuando validemos central al 100%, se elimina.
            return !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }
    }

    public static async Task<RadarInterpretationResult?> ProcesarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            RadarAgentRuntimeHealth.Current.SetCentralState(
                "Degraded",
                "CENTRAL_CREDENTIAL_UNAVAILABLE");
            Console.WriteLine($"  ⚠ Intelligence central sin credencial Agent: {Recortar(detalle, 160)}");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/intelligence/agent");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(mensaje);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                RadarAgentRuntimeHealth.Current.SetCentralState(
                    "Degraded",
                    $"CENTRAL_HTTP_{(int)response.StatusCode}");
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine(
                    $"  ⚠ Intelligence central respondió {(int)response.StatusCode}: " +
                    Recortar(body, 220));
                return null;
            }

            RadarInterpretationResult? result = await response.Content.ReadFromJsonAsync<RadarInterpretationResult>(
                cancellationToken: cancellationToken);
            RadarAgentRuntimeHealth.Current.SetCentralState(
                result is null ? "Degraded" : "Healthy",
                result is null ? "CENTRAL_INVALID_RESPONSE" : null,
                result is null ? null : DateTime.UtcNow);
            return result;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RadarAgentRuntimeHealth.Current.SetCentralState("Degraded", "CENTRAL_TIMEOUT");
            Console.WriteLine("  ⚠ Intelligence central excedió el tiempo de espera.");
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            RadarAgentRuntimeHealth.Current.SetCentralState(
                "Degraded",
                "CENTRAL_INVALID_RESPONSE");
            Console.WriteLine("  ⚠ Intelligence central devolvió una respuesta inválida.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            RadarAgentRuntimeHealth.Current.SetCentralState("Degraded", "CENTRAL_NETWORK");
            Console.WriteLine(
                $"  ⚠ Intelligence central no disponible: {Recortar(ex.Message, 180)}");
            return null;
        }
        finally
        {
            token = string.Empty;
        }
    }

    private static string Recortar(string texto, int max)
    {
        texto = texto.Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "…";
    }
}
