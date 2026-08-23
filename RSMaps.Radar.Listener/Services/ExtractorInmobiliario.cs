using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class ExtractorInmobiliario
{
    public static SolicitudInmobiliaria Extraer(
        string texto,
        string chatOrigen,
        string messageId)
    {
        var solicitud = new SolicitudInmobiliaria
        {
            ChatOrigen = chatOrigen,
            MessageId = messageId,
            MensajeOriginal = texto,
            DetectadoEn = DateTime.Now
        };

        var normalizado = Normalizar(texto);

        solicitud.TipoPropiedad = ExtraerTipoPropiedad(normalizado);
        solicitud.Operacion = ExtraerOperacion(normalizado);
        solicitud.Recamaras = ExtraerEntero(normalizado, @"(\d+)\s*(recamaras?|habitaciones?|cuartos?)");
        solicitud.Banos = ExtraerEntero(normalizado, @"(\d+)\s*(banos?)");
        solicitud.PrecioMaximo = ExtraerPrecioMaximo(normalizado);
        solicitud.TerrenoM2 = ExtraerSuperficie(normalizado, @"(?:terreno|superficie|lote).*?(\d+(?:[.,]\d+)?)\s*(?:m2|mts2|metros)");
        solicitud.ConstruccionM2 = ExtraerSuperficie(normalizado, @"(?:construccion|construidos).*?(\d+(?:[.,]\d+)?)\s*(?:m2|mts2|metros)");
        solicitud.AceptaMascotas = ExtraerMascotas(normalizado);
        solicitud.Amueblado = ExtraerAmueblado(normalizado);
        solicitud.Zona = ExtraerZona(normalizado);

        return solicitud;
    }

    private static string? ExtraerTipoPropiedad(string texto)
    {
        var tipos = new Dictionary<string, string>
        {
            ["casa"] = "Casa",
            ["departamento"] = "Departamento",
            ["depa"] = "Departamento",
            ["terreno"] = "Terreno",
            ["lote"] = "Terreno",
            ["bodega"] = "Bodega",
            ["local"] = "Local",
            ["oficina"] = "Oficina",
            ["rancho"] = "Rancho",
            ["quinta"] = "Quinta"
        };

        foreach (var tipo in tipos)
        {
            if (Regex.IsMatch(texto, $@"\b{Regex.Escape(tipo.Key)}\b"))
                return tipo.Value;
        }

        return null;
    }

    private static string? ExtraerOperacion(string texto)
    {
        if (Regex.IsMatch(texto, @"\b(renta|rentar|arrendar|alquiler)\b"))
            return "Renta";

        if (Regex.IsMatch(texto, @"\b(compra|comprar|venta|adquirir)\b"))
            return "Venta";

        var precio = ExtraerPrecioMaximo(texto);

        if (precio.HasValue)
            return precio.Value <= 100_000 ? "Renta" : "Venta";

        return null;
    }

    private static int? ExtraerEntero(string texto, string patron)
    {
        var match = Regex.Match(texto, patron, RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return int.TryParse(match.Groups[1].Value, out var valor)
            ? valor
            : null;
    }

    private static decimal? ExtraerPrecioMaximo(string texto)
    {
        var millones = Regex.Match(
            texto,
            @"(?:hasta|maximo|presupuesto(?:\s+maximo)?(?:\s+de)?|no\s+pase\s+de).*?\$?\s*(\d+(?:[.,]\d+)?)\s*(millones?|millon)");

        if (millones.Success)
        {
            var numero = ParseDecimalFlexible(millones.Groups[1].Value);
            if (numero.HasValue)
                return numero.Value * 1_000_000;
        }

        var match = Regex.Match(
            texto,
            @"(?:hasta|maximo|presupuesto(?:\s+maximo)?(?:\s+de)?|no\s+pase\s+de).*?\$?\s*([\d,\.]+)");

        if (!match.Success)
            return null;

        var limpio = Regex.Replace(match.Groups[1].Value, @"[^\d]", "");

        return decimal.TryParse(limpio, out var precio)
            ? precio
            : null;
    }

    private static decimal? ExtraerSuperficie(string texto, string patron)
    {
        var match = Regex.Match(texto, patron, RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return ParseDecimalFlexible(match.Groups[1].Value);
    }

    private static bool? ExtraerMascotas(string texto)
    {
        if (texto.Contains("no mascotas") || texto.Contains("sin mascotas"))
            return false;

        if (texto.Contains("mascota") || texto.Contains("pet friendly"))
            return true;

        return null;
    }

    private static bool? ExtraerAmueblado(string texto)
    {
        if (texto.Contains("sin amueblar") || texto.Contains("no amueblado"))
            return false;

        if (texto.Contains("amueblado") || texto.Contains("amueblada"))
            return true;

        return null;
    }

    private static string? ExtraerZona(string texto)
    {
        var match = Regex.Match(
            texto,
            @"(?:\ben\b|\bpor\b|\bzona\b)\s+([a-z0-9\s]+?)(?=,|\.|\bhasta\b|\bpresupuesto\b|\bmaximo\b|\bde\s+\d+\s+recamaras\b|$)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        var zona = match.Groups[1].Value.Trim();

        if (zona.Length < 3)
            return null;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(zona);
    }

    private static decimal? ParseDecimalFlexible(string valor)
    {
        var normalizado = valor.Replace(",", ".");

        return decimal.TryParse(
            normalizado,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var numero)
            ? numero
            : null;
    }

    private static string Normalizar(string texto)
    {
        texto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();

        foreach (var c in texto)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);

            if (categoria != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
