using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarInterpretationNormalizer
{
    private static readonly HashSet<string> ZonasGenericas = new(StringComparer.OrdinalIgnoreCase)
    {
        "cerca",
        "zona cercana",
        "zonas cercanas",
        "alrededores",
        "cercanias",
        "ese rumbo",
        "por ese rumbo",
        "no muy retiradas",
        "no muy retirada"
    };

    private static readonly string[] MarcadoresRespuestaOferta =
    [
        "si aun busca",
        "si todavia busca",
        "si sigue buscando",
        "tengo opciones",
        "tengo opcion",
        "tengo una opcion",
        "cuento con",
        "puedo ofrecer",
        "le ofrezco",
        "te ofrezco"
    ];

    public static RadarInterpretationResult Normalizar(
        RadarInterpretationResult resultado,
        RadarMessage mensaje)
    {
        foreach (var solicitud in resultado.Solicitudes)
        {
            solicitud.Operacion = NormalizarOperacion(solicitud.Operacion);
            solicitud.TiposPropiedad = NormalizarTipos(solicitud.TiposPropiedad);
            solicitud.Zonas = NormalizarZonas(solicitud.Zonas, mensaje.TextoOriginal);
            solicitud.ModalidadesPago = NormalizarModalidadesPago(solicitud.ModalidadesPago);

            NormalizarPrecioExactoComoMaximo(solicitud, mensaje.TextoOriginal);
            NormalizarRecamaras(solicitud, mensaje.TextoOriginal);
        }

        return resultado;
    }

    private static string? NormalizarOperacion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var n = NormalizarTexto(valor);
        if (ContieneAlguno(n, "renta", "rentar", "arrendamiento", "alquiler"))
            return "Renta";

        if (ContieneAlguno(n, "venta", "compra", "comprar", "adquirir"))
            return "Venta";

        return valor.Trim();
    }

    private static List<string> NormalizarTipos(IEnumerable<string> tipos)
    {
        var resultado = new List<string>();

        foreach (var tipo in tipos)
        {
            if (string.IsNullOrWhiteSpace(tipo))
                continue;

            var n = NormalizarTexto(tipo);
            var canonico =
                ContieneAlguno(n, "casa", "residencia", "vivienda") ? "Casa" :
                ContieneAlguno(n, "departamento", "depto", "apartamento") ? "Departamento" :
                ContieneAlguno(n, "terreno", "lote") ? "Terreno" :
                ContieneAlguno(n, "local", "comercial") ? "Local" :
                ContieneAlguno(n, "bodega", "nave") ? "Bodega" :
                ContieneAlguno(n, "oficina", "despacho") ? "Oficina" :
                ContieneAlguno(n, "rancho", "quinta") ? "Rancho" :
                ContieneAlguno(n, "edificio") ? "Edificio" :
                tipo.Trim();

            if (!resultado.Contains(canonico, StringComparer.OrdinalIgnoreCase))
                resultado.Add(canonico);
        }

        return resultado;
    }

    private static List<string> NormalizarZonas(
        IEnumerable<string> zonas,
        string mensajeOriginal)
    {
        var resultado = new List<string>();
        var mensajeNormalizado = NormalizarTexto(mensajeOriginal);
        var inicioRespuesta = BuscarInicioRespuestaOferta(mensajeNormalizado);

        foreach (var zonaOriginal in zonas)
        {
            var zona = LimpiarZona(zonaOriginal);
            if (string.IsNullOrWhiteSpace(zona))
                continue;

            var zonaNormalizada = NormalizarTexto(zona);
            if (EsZonaGenerica(zonaNormalizada))
                continue;

            if (inicioRespuesta >= 0)
            {
                var posicionZona = mensajeNormalizado.IndexOf(zonaNormalizada, StringComparison.OrdinalIgnoreCase);
                var posicionPrevia = mensajeNormalizado[..inicioRespuesta]
                    .IndexOf(zonaNormalizada, StringComparison.OrdinalIgnoreCase);

                if (posicionZona >= inicioRespuesta && posicionPrevia < 0)
                    continue;
            }

            if (!resultado.Contains(zona, StringComparer.OrdinalIgnoreCase))
                resultado.Add(zona);
        }

        return resultado;
    }

    private static List<string> NormalizarModalidadesPago(IEnumerable<string> modalidades)
    {
        var resultado = new List<string>();

        foreach (var modalidad in modalidades)
        {
            if (string.IsNullOrWhiteSpace(modalidad))
                continue;

            var n = NormalizarTexto(modalidad);
            string canonica;

            if (n.Contains("infonavit"))
                canonica = "Infonavit";
            else if (n.Contains("fovissste"))
                canonica = "Fovissste";
            else if (n.Contains("banjercito"))
                canonica = "Banjercito";
            else if (n.Contains("contado") || n.Contains("efectivo"))
                canonica = "Contado";
            else if (n.Contains("bancario"))
                canonica = "Crédito bancario";
            else if (n.Contains("hipotecario"))
                canonica = "Crédito hipotecario";
            else
                canonica = modalidad.Trim();

            if (!resultado.Contains(canonica, StringComparer.OrdinalIgnoreCase))
                resultado.Add(canonica);
        }

        return resultado;
    }

    private static void NormalizarPrecioExactoComoMaximo(
        SolicitudInmobiliaria solicitud,
        string mensajeOriginal)
    {
        if (!solicitud.PrecioMinimo.HasValue ||
            !solicitud.PrecioMaximo.HasValue ||
            solicitud.PrecioMinimo.Value != solicitud.PrecioMaximo.Value)
        {
            return;
        }

        var texto = QuitarDiacriticos(mensajeOriginal).ToLowerInvariant();
        var tieneRangoExplicito = Regex.IsMatch(
            texto,
            @"\b(?:entre|desde)\b.*?\b(?:y|hasta)\b|\$?\s*\d[\d\s,\.]*\s+(?:a|hasta)\s+\$?\s*\d[\d\s,\.]*",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!tieneRangoExplicito)
            solicitud.PrecioMinimo = null;
    }

    private static void NormalizarRecamaras(
        SolicitudInmobiliaria solicitud,
        string mensajeOriginal)
    {
        if (!solicitud.RecamarasMin.HasValue && !solicitud.RecamarasMax.HasValue)
            return;

        var texto = NormalizarTexto(mensajeOriginal);
        var hayCriterioTotal = Regex.IsMatch(
            texto,
            @"\b\d+\s*(?:a|hasta|-)?\s*\d*\s*(?:recamaras?|habitaciones?|cuartos?)\b",
            RegexOptions.IgnoreCase);

        var soloPlantaBaja = Regex.IsMatch(
            texto,
            @"\brecamara\s+en\s+planta\s+baja\b|\brecamaras?\s+en\s+planta\s+baja\b",
            RegexOptions.IgnoreCase);

        if (!hayCriterioTotal && soloPlantaBaja)
        {
            solicitud.RecamarasMin = null;
            solicitud.RecamarasMax = null;
        }
    }

    private static int BuscarInicioRespuestaOferta(string mensajeNormalizado)
    {
        var indices = MarcadoresRespuestaOferta
            .Select(x => mensajeNormalizado.IndexOf(x, StringComparison.OrdinalIgnoreCase))
            .Where(x => x >= 0)
            .ToList();

        return indices.Count == 0 ? -1 : indices.Min();
    }

    private static bool EsZonaGenerica(string zonaNormalizada)
    {
        if (ZonasGenericas.Contains(zonaNormalizada))
            return true;

        return zonaNormalizada.StartsWith("zonas cercanas ", StringComparison.OrdinalIgnoreCase)
            || zonaNormalizada.StartsWith("zona cercana ", StringComparison.OrdinalIgnoreCase);
    }

    private static string LimpiarZona(string valor) =>
        Regex.Replace(valor ?? string.Empty, @"\s+", " ")
            .Trim(' ', ',', ';', ':', '-', '.');

    internal static string NormalizarTexto(string texto)
    {
        var sinDiacriticos = QuitarDiacriticos(texto).ToLowerInvariant();
        return Regex.Replace(sinDiacriticos, @"[^a-z0-9]+", " ").Trim();
    }

    private static string QuitarDiacriticos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);

        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContieneAlguno(string texto, params string[] valores) =>
        valores.Any(texto.Contains);
}
