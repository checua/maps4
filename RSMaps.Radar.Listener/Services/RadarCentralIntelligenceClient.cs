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
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine(
                    $"  ⚠ Intelligence central respondió {(int)response.StatusCode}: " +
                    Recortar(body, 220));
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RadarInterpretationResult>(
                cancellationToken: cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("  ⚠ Intelligence central excedió el tiempo de espera.");
            return null;
        }
        catch (HttpRequestException ex)
        {
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
