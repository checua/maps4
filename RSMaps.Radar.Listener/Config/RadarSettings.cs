namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    // Nombres tal como aparecen en WhatsApp Web.
    // El chat personal se incluye temporalmente para pruebas controladas.
    public static readonly string[] ChatsMonitoreados =
    [
        "INVENTARIOS Y PROSPECTOS",
        "Leones Inmobiliarios Dgo",
        "José Juan (Tú)"
    ];

    public const int IntervaloRevisionMs = 3000;
    public const int EsperaBusquedaMs = 800;
}
