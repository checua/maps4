namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    public static bool ModoSeguroLab =>
        string.Equals(
            Environment.GetEnvironmentVariable("RADAR_SAFE_LAB"),
            "1",
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            Environment.GetEnvironmentVariable("RADAR_SAFE_LAB"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    // En modo seguro sólo se observa el chat de pruebas del propio usuario.
    public static string[] ChatsMonitoreados => ModoSeguroLab
        ? ["José Juan (Tú)"]
        :
        [
            "INVENTARIOS Y PROSPECTOS",
            "Leones Inmobiliarios Dgo",
            "Terrenos en venta Dgo",
            "AISE tu socio en el éxito!",
            "José Juan (Tú)"
        ];

    // Términos alternativos para localizar chats cuando el título visible
    // contiene sufijos, emojis o WhatsApp lo indexa de otra forma.
    private static readonly Dictionary<string, string[]> TerminosBusqueda =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["José Juan (Tú)"] =
            [
                "+52 1 618 299 9621",
                "6182999621",
                "José Juan"
            ],
            ["AISE tu socio en el éxito!"] =
            [
                "AISE tu socio en el éxito",
                "AISE tu socio"
            ],
            ["Terrenos en venta Dgo"] =
            [
                "Terrenos en venta"
            ]
        };

    public static IEnumerable<string> ObtenerTerminosBusqueda(string chat)
    {
        yield return chat;

        if (!TerminosBusqueda.TryGetValue(chat, out var alternativos))
            yield break;

        foreach (var termino in alternativos)
            yield return termino;
    }

    // El modo seguro revisa rápido para pruebas E2E; producción conserva 20 minutos.
    public static int IntervaloRevisionMs =>
        ModoSeguroLab
            ? 10_000
            : 20 * 60_000;

    public const int EsperaBusquedaMs = 900;
}
