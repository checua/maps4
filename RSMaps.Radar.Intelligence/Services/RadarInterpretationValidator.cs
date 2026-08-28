using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarInterpretationValidator
{
    private static readonly HashSet<string> TiposCanonicos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Casa", "Departamento", "Terreno", "Local", "Bodega", "Oficina", "Rancho", "Edificio"
    };

    private static readonly HashSet<string> PalabrasZonaIgnorables = new(StringComparer.OrdinalIgnoreCase)
    {
        "zona", "zonas", "col", "colonia", "fracc", "fraccionamiento",
        "de", "del", "la", "las", "el", "los", "y", "al"
    };

    public static RadarValidationResult Validar(
        RadarInterpretationResult resultado,
        RadarMessage mensaje)
    {
        var validacion = new RadarValidationResult();
        var texto = RadarInterpretationNormalizer.NormalizarTexto(mensaje.TextoOriginal);

        var solicitudesEsperadas = EstimarSolicitudesExplicitas(texto);
        if (solicitudesEsperadas >= 2 && resultado.Solicitudes.Count < solicitudesEsperadas)
        {
            validacion.Problemas.Add(
                $"El mensaje parece contener {solicitudesEsperadas} solicitudes explícitas y sólo se extrajeron {resultado.Solicitudes.Count}.");
        }

        for (var i = 0; i < resultado.Solicitudes.Count; i++)
        {
            var s = resultado.Solicitudes[i];
            var prefijo = $"Solicitud #{i + 1}";

            if (s.PrecioMinimo.HasValue && s.PrecioMaximo.HasValue &&
                s.PrecioMinimo.Value > s.PrecioMaximo.Value)
            {
                validacion.Problemas.Add($"{prefijo}: precio mínimo mayor que precio máximo.");
            }

            if (s.RecamarasMin.HasValue && s.RecamarasMax.HasValue &&
                s.RecamarasMin.Value > s.RecamarasMax.Value)
            {
                validacion.Problemas.Add($"{prefijo}: rango de recámaras inválido.");
            }

            if (s.BanosMin.HasValue && s.BanosMax.HasValue &&
                s.BanosMin.Value > s.BanosMax.Value)
            {
                validacion.Problemas.Add($"{prefijo}: rango de baños inválido.");
            }

            foreach (var tipo in s.TiposPropiedad)
            {
                if (!TiposCanonicos.Contains(tipo))
                    validacion.Problemas.Add($"{prefijo}: tipo de propiedad no canónico '{tipo}'.");
            }

            if (!string.IsNullOrWhiteSpace(s.TipoFraccionamiento) &&
                !string.Equals(s.TipoFraccionamiento, "Privado", StringComparison.OrdinalIgnoreCase))
            {
                validacion.Problemas.Add(
                    $"{prefijo}: tipo de fraccionamiento no canónico '{s.TipoFraccionamiento}'.");
            }

            foreach (var zona in s.Zonas)
            {
                if (!ZonaRespaldadaPorMensaje(zona, texto))
                {
                    validacion.Problemas.Add(
                        $"{prefijo}: la zona '{zona}' no está suficientemente respaldada por el mensaje original.");
                }
            }

            var criterios = ContarCriteriosFuertes(s);
            if (criterios < 2)
            {
                validacion.Advertencias.Add(
                    $"{prefijo}: DATOS_INSUFICIENTES para un matching confiable ({criterios} criterio(s) fuerte(s)).");
            }
        }

        return validacion;
    }

    private static int EstimarSolicitudesExplicitas(string texto)
    {
        var matches = Regex.Matches(
            texto,
            @"\b(?:casa|departamento|terreno|local|bodega|oficina|rancho|edificio)\s+en\s+(?:venta|renta)\b",
            RegexOptions.IgnoreCase);

        return matches.Count;
    }

    private static bool ZonaRespaldadaPorMensaje(string zona, string textoNormalizado)
    {
        var zonaNormalizada = RadarInterpretationNormalizer.NormalizarTexto(zona);
        if (string.IsNullOrWhiteSpace(zonaNormalizada))
            return false;

        if (textoNormalizado.Contains(zonaNormalizada, StringComparison.OrdinalIgnoreCase))
            return true;

        var tokens = zonaNormalizada
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 3 && !PalabrasZonaIgnorables.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tokens.Count > 0 && tokens.All(x =>
            textoNormalizado.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private static int ContarCriteriosFuertes(SolicitudInmobiliaria s)
    {
        var total = 0;

        if (!string.IsNullOrWhiteSpace(s.Operacion)) total++;
        if (s.TiposPropiedad.Count > 0) total++;
        if (s.SubtiposPropiedad.Count > 0) total++;
        if (s.Zonas.Count > 0) total++;
        if (!string.IsNullOrWhiteSpace(s.TipoFraccionamiento)) total++;
        if (s.PrecioMinimo.HasValue || s.PrecioMaximo.HasValue) total++;
        if (s.RecamarasMin.HasValue || s.RecamarasMax.HasValue) total++;
        if (s.BanosMin.HasValue || s.BanosMax.HasValue) total++;
        if (s.TerrenoMinM2.HasValue || s.ConstruccionMinM2.HasValue) total++;
        if (s.CocheraMinAutos.HasValue) total++;
        if (s.AceptaMascotas.HasValue || s.Amueblado.HasValue || s.UnaPlanta.HasValue || s.CasetaVigilancia.HasValue) total++;
        if (s.ModalidadesPago.Count > 0) total++;

        return total;
    }
}
