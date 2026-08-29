using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public static class RadarAgentBackendClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static string BaseUrl =>
        (Environment.GetEnvironmentVariable("RSMAPS_BASE_URL")?.Trim()
         ?? "http://localhost:5102").TrimEnd('/');

    public static async Task<RadarAgentRemoteContext?> ValidarAsync(
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out _))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/radar/agent/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<RadarAgentRemoteContext>(
                cancellationToken: cancellationToken);
        }
        finally
        {
            token = string.Empty;
        }
    }

    public static async Task<RadarAgentRemoteConfig?> ObtenerConfiguracionAsync(
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out _))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/radar/agent/config");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<RadarAgentRemoteConfig>(
                cancellationToken: cancellationToken);
        }
        finally
        {
            token = string.Empty;
        }
    }

    public static async Task<bool> SincronizarConfiguracionAsync(
        RadarAgentConfig config,
        bool mostrarEstado = false,
        CancellationToken cancellationToken = default)
    {
        RadarAgentRemoteConfig? remota = await ObtenerConfiguracionAsync(config, cancellationToken);
        if (remota is null)
            return false;

        RadarAgentRemoteConfig? anterior = RadarAgentRemoteConfigCache.Actual;
        bool cambio = anterior is null
            || anterior.Configurada != remota.Configurada
            || anterior.ActualizadoUtc != remota.ActualizadoUtc;

        RadarAgentRemoteConfigCache.Aplicar(remota);

        if (mostrarEstado || cambio)
        {
            if (remota.Configurada)
            {
                Console.WriteLine(
                    $"  ⚙ Configuración RSMaps: central activa · {remota.ChatsMonitoreados.Length} chat(s) · " +
                    $"intervalo {remota.IntervaloRevisionMs / 1000}s · destino " +
                    $"{(string.IsNullOrWhiteSpace(remota.DestinoAlertas) ? "Propiedades" : remota.DestinoAlertas)}.");
            }
            else
            {
                Console.WriteLine("  Configuración RSMaps: todavía no guardada; usando fallback local.");
            }
        }

        return true;
    }

    public static void MostrarEstado(RadarAgentConfig config)
    {
        string ruta = RadarAgentCredentialStore.ObtenerRuta(config);

        if (!File.Exists(ruta))
        {
            Console.WriteLine("  Vinculación RSMaps: pendiente");
            Console.WriteLine($"  Credencial: {ruta}");
            return;
        }

        try
        {
            RadarAgentRemoteContext? remoto = ValidarAsync(config).GetAwaiter().GetResult();
            if (remoto is null)
            {
                Console.WriteLine("  Vinculación RSMaps: credencial no validada");
                Console.WriteLine($"  Credencial: {ruta}");
                return;
            }

            Console.WriteLine("  Vinculación RSMaps: autenticada");
            Console.WriteLine($"  IdAgent: {remoto.IdAgent}");
            Console.WriteLine($"  Cuenta:  {remoto.Cuenta} (Id {remoto.IdCuenta})");
            Console.WriteLine($"  Rol:     {remoto.Rol}");
            Console.WriteLine($"  Equipo:  {remoto.Equipo ?? "(sin registrar)"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Vinculación RSMaps: no disponible ({ex.Message})");
        }
    }
}

public sealed class RadarAgentRemoteContext
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = string.Empty;
    public string? Equipo { get; set; }
    public int IdAsesor { get; set; }
    public int IdCuenta { get; set; }
    public string Cuenta { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}
