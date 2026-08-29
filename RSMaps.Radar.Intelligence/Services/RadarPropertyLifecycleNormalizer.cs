using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

/// <summary>
/// Normaliza la condición y la etapa comercial/constructiva solicitadas.
/// Mantiene esta semántica separada del tipo/subtipo del inmueble.
/// </summary>
public static class RadarPropertyLifecycleNormalizer
{
    public static void Aplicar(
        RadarInterpretationResult resultado,
        RadarMessage mensaje)
    {
        foreach (var solicitud in resultado.Solicitudes)
        {
            solicitud.CondicionInmueble = NormalizarCondicion(solicitud.CondicionInmueble);
            solicitud.EtapaInmueble = NormalizarEtapa(solicitud.EtapaInmueble);
        }

        // El mensaje completo sólo es un respaldo determinista seguro cuando hay
        // una única solicitud. Con varias solicitudes dejar que la IA segmente evita
        // trasladar la condición de una propiedad a otra.
        if (resultado.Solicitudes.Count != 1)
            return;

        var unica = resultado.Solicitudes[0];
        string texto = RadarInterpretationNormalizer.NormalizarTexto(mensaje.TextoOriginal);

        unica.CondicionInmueble ??= InferirCondicion(texto);
        unica.EtapaInmueble ??= InferirEtapa(texto);
    }

    private static string? NormalizarCondicion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        string n = RadarInterpretationNormalizer.NormalizarTexto(valor);

        if (ContieneAlguno(n, "remodelada", "remodelado", "renovada", "renovado", "rehabilitada", "rehabilitado"))
            return "Remodelada";

        if (ContieneAlguno(n, "usada", "usado", "segunda mano"))
            return "Usada";

        if (ContieneAlguno(n, "nueva", "nuevo", "para estrenar", "a estrenar", "sin habitar", "sin estrenar"))
            return "Nueva";

        return valor.Trim();
    }

    private static string? NormalizarEtapa(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        string n = RadarInterpretationNormalizer.NormalizarTexto(valor);

        if (ContieneAlguno(n, "preventa", "pre venta"))
            return "Preventa";

        if (ContieneAlguno(n, "en construccion", "construccion", "en obra"))
            return "En construcción";

        if (ContieneAlguno(n, "terminada", "terminado", "lista para entrega", "listo para entrega", "entrega inmediata"))
            return "Terminada";

        return valor.Trim();
    }

    private static string? InferirCondicion(string texto)
    {
        if (Regex.IsMatch(texto, @"\b(?:para|a) estrenar\b|\bsin (?:habitar|estrenar)\b"))
            return "Nueva";

        const string inmueble = @"(?:casa|departamento|depto|apartamento|propiedad|inmueble|vivienda|local|bodega|oficina)";

        if (Regex.IsMatch(
                texto,
                $@"\b{inmueble}\b(?:\s+[a-z0-9]+){{0,4}}\s+nuev[oa]\b|\bnuev[oa]\s+{inmueble}\b"))
        {
            return "Nueva";
        }

        if (Regex.IsMatch(
                texto,
                $@"\b{inmueble}\b(?:\s+[a-z0-9]+){{0,4}}\s+(?:remodelad[oa]|renovad[oa]|rehabilitad[oa])\b|\b(?:remodelad[oa]|renovad[oa]|rehabilitad[oa])\s+{inmueble}\b"))
        {
            return "Remodelada";
        }

        if (Regex.IsMatch(
                texto,
                $@"\b{inmueble}\b(?:\s+[a-z0-9]+){{0,4}}\s+usad[oa]\b|\busad[oa]\s+{inmueble}\b|\b{inmueble}\s+de\s+segunda\s+mano\b"))
        {
            return "Usada";
        }

        return null;
    }

    private static string? InferirEtapa(string texto)
    {
        if (Regex.IsMatch(texto, @"\bpre\s*venta\b"))
            return "Preventa";

        if (Regex.IsMatch(texto, @"\ben\s+construccion\b|\ben\s+obra\b"))
            return "En construcción";

        if (Regex.IsMatch(
                texto,
                @"\b(?:propiedad|inmueble|casa|departamento|depto|vivienda)\b(?:\s+[a-z0-9]+){0,4}\s+terminad[oa]\b|\b(?:lista|listo)\s+para\s+entrega\b|\bentrega\s+inmediata\b"))
        {
            return "Terminada";
        }

        return null;
    }

    private static bool ContieneAlguno(string texto, params string[] valores) =>
        valores.Any(texto.Contains);
}
