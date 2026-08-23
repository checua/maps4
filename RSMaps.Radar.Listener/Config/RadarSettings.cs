namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    // Nombre EXACTO tal como aparece en WhatsApp Web.
    public static readonly string[] ChatsMonitoreados =
    [
        "INVENTARIOS Y PROSPECTOS",
        "Leones Inmobiliarios Dgo",
        "José Juan (Tú)"
    ];

    // Términos alternativos para localizar chats cuando el nombre visible
    // no funciona bien en el buscador de WhatsApp Web.
    // Para el chat propio usamos el número como fallback de búsqueda.
    private static readonly Dictionary<string, string[]> TerminosBusqueda =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["José Juan (Tú)"] =
            [
                "+52 1 618 299 9621",
                "6182999621",
                "José Juan"
            ]
        };

    public static IEnumerable<string> ObtenerTerminosBusqueda(string chat)
    {
        // Siempre probamos primero el nombre visible.
        yield return chat;

        if (!TerminosBusqueda.TryGetValue(chat, out var alternativos))
            yield break;

        foreach (var termino in alternativos)
            yield return termino;
    }

    // Durante desarrollo usamos ciclos cortos para poder probar sin esperar.
    // En producción cambia ModoPruebas a false: el barrido será cada 5 minutos.
    public const bool ModoPruebas = true;

    public static int IntervaloRevisionMs =>
        ModoPruebas
            ? 30_000
            : 5 * 60_000;

    public const int EsperaBusquedaMs = 900;
}
