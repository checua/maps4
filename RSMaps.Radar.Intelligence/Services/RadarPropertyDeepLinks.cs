using System.Text;

namespace RSMaps.Radar.Listener.Services;

public static class RadarPropertyDeepLinks
{
    public static string NormalizarBaseUrl(string? baseUrl, string fallback)
    {
        string valor = string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl;
        return valor.Trim().TrimEnd('/');
    }

    public static void Agregar(StringBuilder sb, int idInmueble, string baseUrl)
    {
        if (idInmueble <= 0 || string.IsNullOrWhiteSpace(baseUrl))
            return;

        string id = idInmueble.ToString(System.Globalization.CultureInfo.InvariantCulture);
        sb.AppendLine($"📋 Inventario: {baseUrl}/i/{id}");
        sb.AppendLine($"📍 Mapa: {baseUrl}/m/{id}");
    }
}
