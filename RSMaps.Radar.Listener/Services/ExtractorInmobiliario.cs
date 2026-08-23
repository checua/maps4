using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class ExtractorInmobiliario
{
    private static readonly (string Patron, string Nombre)[] Tipos =
    [
        (@"\bcasa\b", "Casa"),
        (@"\bdepartamento\b|\bdepa\b", "Departamento"),
        (@"\bterreno\b|\blote\b", "Terreno"),
        (@"\bbodega\b", "Bodega"),
        (@"\blocal\b", "Local"),
        (@"\boficina(?:s)?\b", "Oficina"),
        (@"\brancho\b", "Rancho"),
        (@"\bquinta\b", "Quinta")
    ];

    public static SolicitudInmobiliaria Extraer(
        string texto,
        string chatOrigen,
        string messageId)
    {
        var normalizado = Normalizar(texto);

        var solicitud = new SolicitudInmobiliaria
        {
            ChatOrigen = chatOrigen,
            MessageId = messageId,
            MensajeOriginal = texto,
            DetectadoEn = DateTime.Now,
            Operacion = ExtraerOperacion(normalizado),
            TiposPropiedad = ExtraerTipos(normalizado),
            Zonas = ExtraerZonas(normalizado),
            AceptaMascotas = ExtraerMascotas(normalizado),
            Amueblado = ExtraerAmueblado(normalizado),
            UnaPlanta = ExtraerUnaPlanta(normalizado),
            CasetaVigilancia = normalizado.Contains("caseta de vigilancia") ? true : null,
            CocheraMinAutos = ExtraerCochera(normalizado),
            ModalidadesPago = ExtraerModalidadesPago(normalizado)
        };

        (solicitud.RecamarasMin, solicitud.RecamarasMax) =
            ExtraerRangoEntero(normalizado, @"(\d+)\s*(?:a|-|hasta)\s*(\d+)\s*(?:recamaras?|habitaciones?|cuartos?)", @"(\d+)\s*(?:recamaras?|habitaciones?|cuartos?)");

        (solicitud.BanosMin, solicitud.BanosMax) =
            ExtraerRangoEntero(normalizado, @"(\d+)\s*(?:a|-|hasta)\s*(\d+)\s*banos?", @"(\d+)\s*banos?");

        (solicitud.PrecioMinimo, solicitud.PrecioMaximo) = ExtraerRangoPrecio(normalizado, solicitud.Operacion);

        solicitud.TerrenoMinM2 = ExtraerSuperficieMinima(normalizado);
        solicitud.ConstruccionMinM2 = ExtraerConstruccionMinima(normalizado);

        return solicitud;
    }

    private static List<string> ExtraerTipos(string texto)
    {
        var encontrados = new List<string>();

        foreach (var (patron, nombre) in Tipos)
        {
            if (Regex.IsMatch(texto, patron, RegexOptions.IgnoreCase) &&
                !encontrados.Contains(nombre, StringComparer.OrdinalIgnoreCase))
            {
                encontrados.Add(nombre);
            }
        }

        return encontrados;
    }

    private static string? ExtraerOperacion(string texto)
    {
        if (Regex.IsMatch(texto, @"\b(renta|rentar|arrendar|alquiler)\b"))
            return "Renta";

        if (Regex.IsMatch(texto, @"\b(compra|comprar|venta|adquirir)\b"))
            return "Venta";

        return null;
    }

    private static (int? Min, int? Max) ExtraerRangoEntero(
        string texto,
        string patronRango,
        string patronSimple)
    {
        var rango = Regex.Match(texto, patronRango, RegexOptions.IgnoreCase);
        if (rango.Success &&
            int.TryParse(rango.Groups[1].Value, out var min) &&
            int.TryParse(rango.Groups[2].Value, out var max))
        {
            return (Math.Min(min, max), Math.Max(min, max));
        }

        var simple = Regex.Match(texto, patronSimple, RegexOptions.IgnoreCase);
        if (simple.Success && int.TryParse(simple.Groups[1].Value, out var valor))
            return (valor, valor);

        return (null, null);
    }

    private static (decimal? Min, decimal? Max) ExtraerRangoPrecio(string texto, string? operacion)
    {
        var textoPrecio = texto
            .Replace("'", "")
            .Replace("’", "")
            .Replace(" ", " ");

        var rango = Regex.Match(
            textoPrecio,
            @"\$?\s*([\d][\d\s,\.]*)\s*(?:a|hasta|-)\s*\$?\s*([\d][\d\s,\.]*)",
            RegexOptions.IgnoreCase);

        if (rango.Success)
        {
            var min = ParsePrecio(rango.Groups[1].Value, operacion);
            var max = ParsePrecio(rango.Groups[2].Value, operacion);

            if (min.HasValue && max.HasValue)
                return (Math.Min(min.Value, max.Value), Math.Max(min.Value, max.Value));
        }

        var maximo = Regex.Match(
            textoPrecio,
            @"(?:maximo|max|tope|hasta|presupuesto(?:\s+maximo)?(?:\s+de)?|no\s+pase\s+de|precio)\D{0,20}\$?\s*([\d][\d\s,\.]*)",
            RegexOptions.IgnoreCase);

        if (maximo.Success)
        {
            var valor = ParsePrecio(maximo.Groups[1].Value, operacion);
            return (null, valor);
        }

        var millones = Regex.Match(
            textoPrecio,
            @"\$?\s*(\d+(?:[\.,]\d+)?)\s*(?:millones?|millon|mdp)\b",
            RegexOptions.IgnoreCase);

        if (millones.Success)
        {
            var numero = ParseDecimalFlexible(millones.Groups[1].Value);
            if (numero.HasValue)
                return (null, numero.Value < 100 ? numero.Value * 1_000_000 : numero.Value);
        }

        return (null, null);
    }

    private static decimal? ParsePrecio(string valor, string? operacion)
    {
        var limpio = Regex.Replace(valor, @"[^\d]", "");
        if (!decimal.TryParse(limpio, out var numero))
            return null;

        // En mensajes inmobiliarios reales, rangos como "$25-$35" en renta
        // suelen significar miles de pesos.
        if (operacion == "Renta" && numero > 0 && numero < 1000)
            return numero * 1000;

        // En venta, cifras abreviadas de 1 a 20 suelen representar millones.
        if (operacion == "Venta" && numero > 0 && numero < 100)
            return numero * 1_000_000;

        return numero;
    }

    private static decimal? ExtraerSuperficieMinima(string texto)
    {
        var match = Regex.Match(
            texto,
            @"(?:min(?:imo)?\.?\s*)?(\d+(?:[\.,]\d+)?)\s*(?:m2|mts2|mts|metros(?:\s+cuadrados)?)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return ParseDecimalFlexible(match.Groups[1].Value);
    }

    private static decimal? ExtraerConstruccionMinima(string texto)
    {
        var match = Regex.Match(
            texto,
            @"(?:construccion|construidos).*?(\d+(?:[\.,]\d+)?)\s*(?:m2|mts2|mts|metros)",
            RegexOptions.IgnoreCase);

        return match.Success ? ParseDecimalFlexible(match.Groups[1].Value) : null;
    }

    private static List<string> ExtraerZonas(string texto)
    {
        var zonas = new List<string>();

        var patrones = new[]
        {
            @"(?:ubicad[ao]s?\s+en|ubicada\s+por\s+rumbos\s+de|rumbos?\s+de|rumbo\s+a|por\s+la\s+zona\s+de|zona\s+de|zona|en\s+el|en\s+la|en)\s+([^\n\.]+)",
            @"(?:exclusivamente\s+en|cerca\s+de|fraccionamiento|col\.?|colonia)\s+([^\n\.]+)"
        };

        foreach (var patron in patrones)
        {
            foreach (Match match in Regex.Matches(texto, patron, RegexOptions.IgnoreCase))
            {
                var bloque = LimpiarBloqueZona(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(bloque))
                    continue;

                foreach (var parte in Regex.Split(bloque, @"\s*(?:,|\bo\b|\by\b|/|;)\s*", RegexOptions.IgnoreCase))
                {
                    var zona = LimpiarZona(parte);
                    if (zona.Length < 3 || EsFalsoPositivoZona(zona))
                        continue;

                    var titulo = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(zona);
                    if (!zonas.Contains(titulo, StringComparer.OrdinalIgnoreCase))
                        zonas.Add(titulo);
                }
            }
        }

        return zonas.Take(8).ToList();
    }

    private static string LimpiarBloqueZona(string valor)
    {
        var corte = Regex.Split(
            valor,
            @"\b(?:presupuesto|maximo|max|precio|credito|de\s+\d+\s+(?:a\s+\d+\s+)?recamaras?|\d+\s+recamaras?|\d+\s+banos?|sin\s+amueblar|con\s+mascota|que\s+acepte|agradezco|para\s+credito)\b",
            RegexOptions.IgnoreCase);

        return corte[0].Trim(' ', ',', '-', ':');
    }

    private static string LimpiarZona(string valor)
    {
        return Regex.Replace(valor, @"\s+", " ")
            .Replace("alrededores de estos fraccionamientos", "", StringComparison.OrdinalIgnoreCase)
            .Replace("o alrededores", "", StringComparison.OrdinalIgnoreCase)
            .Replace("y alrededores", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ',', '-', ':');
    }

    private static bool EsFalsoPositivoZona(string zona)
    {
        var z = zona.ToLowerInvariant();
        return z is "renta" or "venta" or "compra" or "casa" or "departamento" or "terreno" or "bodega" or "local" ||
               z.StartsWith("renta ") || z.StartsWith("venta ") || z.StartsWith("compra ");
    }

    private static bool? ExtraerMascotas(string texto)
    {
        if (texto.Contains("no mascotas") || texto.Contains("sin mascotas"))
            return false;

        if (texto.Contains("mascota") || texto.Contains("perro") || texto.Contains("pet friendly"))
            return true;

        return null;
    }

    private static bool? ExtraerAmueblado(string texto)
    {
        if (texto.Contains("sin amueblar") || texto.Contains("no amueblado") || texto.Contains("no amueblada"))
            return false;

        if (texto.Contains("amueblado") || texto.Contains("amueblada"))
            return true;

        return null;
    }

    private static bool? ExtraerUnaPlanta(string texto)
    {
        if (Regex.IsMatch(texto, @"\b(1|una|un)\s+planta\b|\bde\s+un\s+piso\b"))
            return true;

        return null;
    }

    private static int? ExtraerCochera(string texto)
    {
        var match = Regex.Match(texto, @"cochera.*?(\d+)\s*(?:auto|autos|vehiculo|vehiculos)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var autos))
            return autos;

        if (texto.Contains("cochera"))
            return 1;

        return null;
    }

    private static List<string> ExtraerModalidadesPago(string texto)
    {
        var modalidades = new List<string>();
        var opciones = new Dictionary<string, string>
        {
            ["infonavit"] = "Infonavit",
            ["fovissste"] = "Fovissste",
            ["credito bancario"] = "Crédito bancario",
            ["hipotecario"] = "Crédito hipotecario",
            ["contado"] = "Contado"
        };

        foreach (var opcion in opciones)
        {
            if (texto.Contains(opcion.Key) && !modalidades.Contains(opcion.Value, StringComparer.OrdinalIgnoreCase))
                modalidades.Add(opcion.Value);
        }

        return modalidades;
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
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
