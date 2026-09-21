using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public enum RadarDeliveryDisposition
{
    Retry,
    SinCoincidencia,
    Recomendacion,
    AlternativaRevision,
    NecesitaMasDatos,
    DatosContradictorios
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

    public static RadarDeliveryDecision? EvaluarAntesDeMatching(
        SolicitudInmobiliaria solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        RadarSolicitudAccionabilidadDecision? accionabilidad = solicitud.Accionabilidad;
        if (accionabilidad is null)
            return null;

        if (accionabilidad.Estado == RadarSolicitudAccionabilidadEstado.Accionable)
        {
            return string.IsNullOrWhiteSpace(solicitud.MatchingResumen)
                ? EstadoIncoherente("Solicitud accionable sin resultado durable de Matching.")
                : null;
        }

        if (accionabilidad.Estado == RadarSolicitudAccionabilidadEstado.NecesitaMasDatos)
        {
            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.NecesitaMasDatos,
                DebePrepararDelivery: false,
                DebeTerminalAck: true,
                DisposicionTerminal: "NECESITA_MAS_DATOS",
                Motivo: "Solicitud confirmada pero requiere operación y/o tipo antes de Matching.");
        }

        if (accionabilidad.Motivos.Contains(RadarSolicitudAccionabilidadMotivo.InterpretacionInvalida))
            return EstadoIncoherente("Interpretación inválida llegó al downstream; se conserva retry.");

        if (accionabilidad.Motivos.Contains(RadarSolicitudAccionabilidadMotivo.DatosContradictorios))
        {
            return new RadarDeliveryDecision(
                RadarDeliveryDisposition.DatosContradictorios,
                DebePrepararDelivery: false,
                DebeTerminalAck: true,
                DisposicionTerminal: "DATOS_CONTRADICTORIOS",
                Motivo: "La solicitud contiene datos contradictorios; Matching no fue ejecutado.");
        }

        return EstadoIncoherente("Estado de accionabilidad inconsistente sin motivo reconocido.");
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
                x.Disposicion == RadarDeliveryDisposition.DatosContradictorios))
        {
            return "DATOS_CONTRADICTORIOS";
        }

        if (materializadas.Any(x =>
                x.DebeTerminalAck &&
                x.Disposicion == RadarDeliveryDisposition.NecesitaMasDatos))
        {
            return "NECESITA_MAS_DATOS";
        }

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
