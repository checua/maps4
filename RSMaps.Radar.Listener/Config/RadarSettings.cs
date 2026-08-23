namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    // Nombres EXACTOS tal como aparecen en WhatsApp Web.
    // "José Juan" se incluye temporalmente para pruebas controladas.
    public static readonly string[] ChatsMonitoreados =
    [
        "INVENTARIOS Y PROSPECTOS",
        "Leones Inmobiliarios Dgo",
        "José Juan"
    ];

    public const int IntervaloRevisionMs = 3000;
    public const int EsperaBusquedaMs = 800;
}
