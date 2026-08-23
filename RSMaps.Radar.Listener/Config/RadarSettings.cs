namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    // Empezamos con pocos grupos para validar estabilidad.
    // Agrega aquí los nombres EXACTOS tal como aparecen en WhatsApp.
    public static readonly string[] ChatsMonitoreados =
    [
        "INVENTARIOS Y PROSPECTOS",
        "Leones Inmobiliarios Dgo"
    ];

    public const int IntervaloRevisionMs = 3000;
}
