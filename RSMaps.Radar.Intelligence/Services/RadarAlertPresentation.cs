using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarAlertPresentation
{
    public static string ConstruirEncabezado(
        SolicitudInmobiliaria solicitud)
    {
        if (!solicitud.TieneRecomendacionAutomatica)
        {
            return "⚠ RSMAPS RADAR · ALTERNATIVAS PARA REVISAR";
        }

        string coincidencia =
            solicitud.MejorCoincidencia.HasValue
                ? $"{Math.Round(
                    solicitud.MejorCoincidencia.Value,
                    MidpointRounding.AwayFromZero):0}%"
                : "confirmada";

        return
            $"🔥 RSMAPS RADAR · RECOMENDACIÓN {coincidencia}";
    }

    public static string ConstruirTituloResultados(
        SolicitudInmobiliaria solicitud)
    {
        return solicitud.TieneRecomendacionAutomatica
            ? "✅ RESULTADOS RSMAPS"
            : "⚠ RESULTADOS RSMAPS";
    }
}