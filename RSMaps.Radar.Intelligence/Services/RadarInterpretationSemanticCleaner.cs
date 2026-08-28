using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarInterpretationSemanticCleaner
{
    private static readonly HashSet<string> DescriptoresQueNoSonZona = new(StringComparer.OrdinalIgnoreCase)
    {
        "privado",
        "fracc privado",
        "fraccionamiento privado",
        "residencial privado",
        "fraccionamiento cerrado"
    };

    public static RadarInterpretationResult Limpiar(RadarInterpretationResult resultado)
    {
        foreach (var solicitud in resultado.Solicitudes)
        {
            solicitud.SubtiposPropiedad = solicitud.SubtiposPropiedad
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            solicitud.Zonas = solicitud.Zonas
                .Where(x => !EsDescriptorGenerico(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return resultado;
    }

    private static bool EsDescriptorGenerico(string zona)
    {
        var normalizada = RadarInterpretationNormalizer.NormalizarTexto(zona);
        return DescriptoresQueNoSonZona.Contains(normalizada);
    }
}
