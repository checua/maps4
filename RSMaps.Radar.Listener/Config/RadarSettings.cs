namespace RSMaps.Radar.Listener.Config;

public static class RadarSettings
{
    private static readonly Lazy<RadarAgentConfig?> Configuracion =
        new(RadarAgentConfigLoader.CargarDesdeEntorno);

    static RadarSettings()
    {
        // Forzamos la carga de la identidad del Agent incluso en SAFE LAB.
        // De otro modo, el retorno temprano de ChatsMonitoreados evitaría
        // materializar la configuración y el banner no mostraría Usuario/Cuenta.
        _ = Configuracion.Value;
    }

    private static readonly string[] ChatsLegacy =
    [
        "INVENTARIOS Y PROSPECTOS",
        "Leones Inmobiliarios Dgo",
        "Terrenos en venta Dgo",
        "AISE tu socio en el éxito!",
        "José Juan (Tú)"
    ];

    private static readonly Dictionary<string, string[]> TerminosBusquedaLegacy =
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

    internal static RadarAgentConfig? ConfiguracionAgente => Configuracion.Value;
    internal static RadarAgentRemoteConfig? ConfiguracionRemota => RadarAgentRemoteConfigCache.Actual;

    public static bool ModoSeguroLab =>
        string.Equals(
            Environment.GetEnvironmentVariable("RADAR_SAFE_LAB"),
            "1",
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            Environment.GetEnvironmentVariable("RADAR_SAFE_LAB"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    // SAFE LAB conserva el aislamiento total del prototipo. Fuera de SAFE LAB,
    // la configuración central de RSMaps tiene prioridad sobre el JSON local.
    public static string[] ChatsMonitoreados
    {
        get
        {
            if (ModoSeguroLab)
                return ["José Juan (Tú)"];

            var remota = ConfiguracionRemota;
            if (remota?.Configurada == true)
            {
                return remota.ChatsMonitoreados
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var configurados = ConfiguracionAgente?.ChatsMonitoreados
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return configurados is { Length: > 0 }
                ? configurados
                : ChatsLegacy;
        }
    }

    public static IEnumerable<string> ObtenerTerminosBusqueda(string chat)
    {
        // Defensa adicional del modo seguro: el navegador sólo puede buscar
        // chats que estén expresamente en la lista monitoreada.
        if (ModoSeguroLab &&
            !ChatsMonitoreados.Contains(chat, StringComparer.OrdinalIgnoreCase))
        {
            yield break;
        }

        yield return chat;

        var remotos = ConfiguracionRemota;
        if (remotos?.Configurada == true
            && remotos.TerminosBusqueda.TryGetValue(chat, out var alternativosRemotos))
        {
            foreach (var termino in alternativosRemotos
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return termino;
            }

            yield break;
        }

        var configurados = ConfiguracionAgente?.TerminosBusqueda;
        if (configurados is not null && configurados.TryGetValue(chat, out var alternativosConfigurados))
        {
            foreach (var termino in alternativosConfigurados
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return termino;
            }

            yield break;
        }

        if (!TerminosBusquedaLegacy.TryGetValue(chat, out var alternativosLegacy))
            yield break;

        foreach (var termino in alternativosLegacy)
            yield return termino;
    }

    public static int IntervaloRevisionMs
    {
        get
        {
            if (ModoSeguroLab)
                return 10_000;

            var remoto = ConfiguracionRemota;
            if (remoto?.Configurada == true && remoto.IntervaloRevisionMs > 0)
                return remoto.IntervaloRevisionMs;

            var configurado = ConfiguracionAgente?.IntervaloRevisionMs;
            return configurado is > 0
                ? configurado.Value
                : 20 * 60_000;
        }
    }

    public const int EsperaBusquedaMs = 900;
}
