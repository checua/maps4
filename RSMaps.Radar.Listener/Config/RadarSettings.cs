namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    // Nombre tal como normalmente aparece en WhatsApp Web.
    public static readonly string[] ChatsMonitoreados =
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

    // Operación normal: un barrido cada 20 minutos.
    // Si necesitamos una prueba rápida puntual, se puede activar temporalmente ModoPruebas.
    public const bool ModoPruebas = false;

    public static int IntervaloRevisionMs =>
        ModoPruebas
            ? 30_000
            : 20 * 60_000;

    public const int EsperaBusquedaMs = 900;
}
