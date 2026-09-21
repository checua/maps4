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
        RadarValidationResult global = ValidarCoberturaGlobal(resultado, mensaje);
        validacion.Problemas.AddRange(global.Problemas);

        for (var i = 0; i < resultado.Solicitudes.Count; i++)
        {
            RadarValidationResult individual = ValidarSolicitud(
                resultado.Solicitudes[i],
                mensaje,
                i);
            validacion.Problemas.AddRange(individual.Problemas);
            validacion.Advertencias.AddRange(individual.Advertencias);
        }

        return validacion;
    }

    public static RadarValidationResult ValidarCoberturaGlobal(
        RadarInterpretationResult resultado,
        RadarMessage mensaje)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(mensaje);

        var validacion = new RadarValidationResult();
        string texto = RadarInterpretationNormalizer.NormalizarTexto(mensaje.TextoOriginal);
        int solicitudesEsperadas = EstimarSolicitudesExplicitas(texto);
        if (solicitudesEsperadas >= 2 && resultado.Solicitudes.Count < solicitudesEsperadas)
        {
            validacion.Problemas.Add(
                $"El mensaje parece contener {solicitudesEsperadas} solicitudes explícitas y sólo se extrajeron {resultado.Solicitudes.Count}.");
        }

        return validacion;
    }

    public static RadarValidationResult ValidarSolicitud(
        SolicitudInmobiliaria solicitud,
        RadarMessage mensaje,
        int indice = 0)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(mensaje);

        var validacion = new RadarValidationResult();
        string prefijo = $"Solicitud #{indice + 1}";

        if (solicitud.PrecioMinimo.HasValue && solicitud.PrecioMaximo.HasValue &&
            solicitud.PrecioMinimo.Value > solicitud.PrecioMaximo.Value)
            validacion.Problemas.Add($"{prefijo}: precio mínimo mayor que precio máximo.");

        if (solicitud.RecamarasMin.HasValue && solicitud.RecamarasMax.HasValue &&
            solicitud.RecamarasMin.Value > solicitud.RecamarasMax.Value)
            validacion.Problemas.Add($"{prefijo}: rango de recámaras inválido.");

        if (solicitud.BanosMin.HasValue && solicitud.BanosMax.HasValue &&
            solicitud.BanosMin.Value > solicitud.BanosMax.Value)
            validacion.Problemas.Add($"{prefijo}: rango de baños inválido.");

        RadarValidationResult estructural = ValidarEstructuraSolicitud(
            solicitud,
            mensaje,
            indice);
        validacion.Problemas.AddRange(estructural.Problemas);

        int criterios = RadarSolicitudCriteriosFuertes.Contar(solicitud);
        if (criterios < 2)
        {
            validacion.Advertencias.Add(
                $"{prefijo}: DATOS_INSUFICIENTES para un matching confiable ({criterios} criterio(s) fuerte(s)).");
        }

        return validacion;
    }

    public static bool InterpretacionEstructuralValida(
        SolicitudInmobiliaria solicitud,
        RadarMessage mensaje,
        int indice = 0) =>
        ValidarEstructuraSolicitud(solicitud, mensaje, indice).EsValida;

    private static RadarValidationResult ValidarEstructuraSolicitud(
        SolicitudInmobiliaria solicitud,
        RadarMessage mensaje,
        int indice)
    {
        var validacion = new RadarValidationResult();
        string texto = RadarInterpretationNormalizer.NormalizarTexto(mensaje.TextoOriginal);
        string prefijo = $"Solicitud #{indice + 1}";

        foreach (string tipo in solicitud.TiposPropiedad)
        {
            if (!TiposCanonicos.Contains(tipo))
                validacion.Problemas.Add($"{prefijo}: tipo de propiedad no canónico '{tipo}'.");
        }

        if (!string.IsNullOrWhiteSpace(solicitud.TipoFraccionamiento) &&
            !string.Equals(solicitud.TipoFraccionamiento, "Privado", StringComparison.OrdinalIgnoreCase))
        {
            validacion.Problemas.Add(
                $"{prefijo}: tipo de fraccionamiento no canónico '{solicitud.TipoFraccionamiento}'.");
        }

        foreach (string zona in solicitud.Zonas)
        {
            if (!ZonaRespaldadaPorMensaje(zona, texto))
            {
                validacion.Problemas.Add(
                    $"{prefijo}: la zona '{zona}' no está suficientemente respaldada por el mensaje original.");
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

}
