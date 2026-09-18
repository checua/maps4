using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public enum RadarDeliveryDisposition
{
    Retry,
    SinCoincidencia,
    Recomendacion,
    AlternativaRevision
}

public sealed record RadarDeliveryDecision(
    RadarDeliveryDisposition Disposicion,
    bool DebePrepararDelivery,
    bool DebeTerminalAck,
    string? DisposicionTerminal,
    string Motivo);

public static class RadarDeliveryFlowDecision
{
    public const string AlternativeDeliveryEnvironmentVariable =
        "RADAR_ALLOW_ALTERNATIVE_DELIVERY";

    public static bool AlternativasDeliveryHabilitadas()
    {
        string? valor = Environment.GetEnvironmentVariable(
            AlternativeDeliveryEnvironmentVariable)?.Trim();

        return string.Equals(valor, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static RadarDeliveryDecision Evaluar(
        SolicitudInmobiliaria solicitud,
        RadarMatchingClientResult matching,
        bool permitirDeliveryAlternativas)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(matching);

        if (RadarMatchingFlowDecision.RequiereReintento(matching))
        {
            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.Retry,
                DebePrepararDelivery: false,
                DebeTerminalAck: false,
                DisposicionTerminal: null,
                Motivo: "Matching no confirmado; se conserva retry.");
        }

        bool tieneId = solicitud.IdInmuebleCoincidente.HasValue;
        bool tienePuntuacion = solicitud.MejorCoincidencia.HasValue;

        if (!tieneId && !tienePuntuacion)
        {
            if (solicitud.TieneRecomendacionAutomatica)
                return EstadoIncoherente("Recomendación automática sin coincidencia estructural.");

            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.SinCoincidencia,
                DebePrepararDelivery: false,
                DebeTerminalAck: true,
                DisposicionTerminal: "SIN_COINCIDENCIA_UTIL",
                Motivo: "No existe inmueble con identificador y puntuación útil válidos.");
        }

        if (!tieneId || !tienePuntuacion ||
            solicitud.IdInmuebleCoincidente <= 0 ||
            solicitud.MejorCoincidencia is < 55 or > 100)
        {
            return EstadoIncoherente(
                "La coincidencia confirmada contiene identificador o puntuación inválidos.");
        }

        if (solicitud.TieneRecomendacionAutomatica &&
            solicitud.MejorCoincidencia < 85)
        {
            return EstadoIncoherente(
                "La recomendación automática no alcanza el umbral mínimo de 85%.");
        }

        if (solicitud.TieneRecomendacionAutomatica)
        {
            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.Recomendacion,
                DebePrepararDelivery: true,
                DebeTerminalAck: false,
                DisposicionTerminal: null,
                Motivo: "Recomendación automática válida; Delivery permitido.");
        }

        if (permitirDeliveryAlternativas)
        {
            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.AlternativaRevision,
                DebePrepararDelivery: true,
                DebeTerminalAck: false,
                DisposicionTerminal: null,
                Motivo: "Alternativa útil y Delivery de alternativas habilitado explícitamente.");
        }

        return new RadarDeliveryDecision(
            RadarDeliveryDisposition.AlternativaRevision,
            DebePrepararDelivery: false,
            DebeTerminalAck: true,
            DisposicionTerminal: "ALTERNATIVA_PARA_REVISION",
            Motivo: "Alternativa útil conservada sin Delivery por política segura.");
    }

    public static string? ResolverDisposicionTerminalGlobal(
        IEnumerable<RadarDeliveryDecision> decisiones,
        bool bloqueoSafeLab,
        bool huboDeliveryCompletado)
    {
        ArgumentNullException.ThrowIfNull(decisiones);
        List<RadarDeliveryDecision> materializadas = decisiones.ToList();

        if (bloqueoSafeLab)
            return "SAFE_LAB_BLOQUEADO";

        if (materializadas.Any(x => x.Disposicion == RadarDeliveryDisposition.Retry))
            return null;

        if (huboDeliveryCompletado)
            return "ALERTA_ENTREGADA";

        if (materializadas.Any(x =>
                x.DebeTerminalAck &&
                string.Equals(
                    x.DisposicionTerminal,
                    "ALTERNATIVA_PARA_REVISION",
                    StringComparison.Ordinal)))
        {
            return "ALTERNATIVA_PARA_REVISION";
        }

        if (materializadas.Count > 0 && materializadas.All(x =>
                x.DebeTerminalAck &&
                string.Equals(
                    x.DisposicionTerminal,
                    "SIN_COINCIDENCIA_UTIL",
                    StringComparison.Ordinal)))
        {
            return "SIN_COINCIDENCIA_UTIL";
        }

        return null;
    }

    private static RadarDeliveryDecision EstadoIncoherente(string motivo) =>
        new(
            RadarDeliveryDisposition.Retry,
            DebePrepararDelivery: false,
            DebeTerminalAck: false,
            DisposicionTerminal: null,
            Motivo: motivo);
}
