namespace RSMaps.Radar.Listener.Config;

public sealed class RadarAgentRemoteConfig
{
    public Guid IdAgent { get; set; }
    public bool Configurada { get; set; }
    public string[] ChatsMonitoreados { get; set; } = [];
    public string? DestinoAlertas { get; set; }
    public int IntervaloRevisionMs { get; set; } = 60_000;
    public Dictionary<string, string[]> TerminosBusqueda { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public DateTime? ActualizadoUtc { get; set; }
}

public static class RadarAgentRemoteConfigCache
{
    private static RadarAgentRemoteConfig? _actual;

    public static RadarAgentRemoteConfig? Actual => Volatile.Read(ref _actual);

    public static void Aplicar(RadarAgentRemoteConfig? configuracion)
    {
        if (configuracion is not null)
        {
            configuracion.ChatsMonitoreados ??= [];
            configuracion.TerminosBusqueda = new Dictionary<string, string[]>(
                configuracion.TerminosBusqueda ?? new Dictionary<string, string[]>(),
                StringComparer.OrdinalIgnoreCase);
        }

        Volatile.Write(ref _actual, configuracion);
    }
}
