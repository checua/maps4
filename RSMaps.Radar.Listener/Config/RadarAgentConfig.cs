using System.Text.Json;

namespace RSMaps.Radar.Listener.Config;

public sealed class RadarAgentConfig
{
    public string? Usuario { get; set; }
    public string? Cuenta { get; set; }
    public string[] ChatsMonitoreados { get; set; } = [];
    public string? DestinoAlertas { get; set; }
    public int? IntervaloRevisionMs { get; set; }
    public Dictionary<string, string[]> TerminosBusqueda { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class RadarAgentConfigLoader
{
    public static RadarAgentConfig? CargarDesdeEntorno()
    {
        var path = Environment.GetEnvironmentVariable("RADAR_AGENT_CONFIG_PATH")?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"RADAR_AGENT_CONFIG_PATH apunta a un archivo inexistente: '{path}'.");
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<RadarAgentConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (config is null)
                throw new InvalidOperationException("El archivo de configuración del Agent está vacío.");

            config.ChatsMonitoreados ??= [];
            config.TerminosBusqueda = new Dictionary<string, string[]>(
                config.TerminosBusqueda ?? new Dictionary<string, string[]>(),
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine();
            Console.WriteLine("RADAR Agent configurado:");
            Console.WriteLine($"  Usuario: {Mostrar(config.Usuario)}");
            Console.WriteLine($"  Cuenta:  {Mostrar(config.Cuenta)}");
            Console.WriteLine($"  Archivo: {path}");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"No fue posible leer la configuración RADAR Agent de '{path}': {ex.Message}",
                ex);
        }
    }

    private static string Mostrar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "(sin configurar)" : valor.Trim();
}
