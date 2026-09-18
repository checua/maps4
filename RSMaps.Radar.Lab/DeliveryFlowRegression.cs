using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class DeliveryFlowRegression
{
    public static Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("REGRESION POLITICA DE DELIVERY RADAR");
        Console.WriteLine("==============================================");

        RadarMatchingClientResult confirmado =
            RadarMatchingClientResult.ResultadoConfirmado(
                "COINCIDENCIAS RSMAPS\nResultado confirmado.");

        RadarDeliveryDecision recomendacion = Evaluar(
            recomendado: true,
            idInmueble: 900,
            puntuacion: 100,
            confirmado,
            permitirAlternativas: false);
        Verificar(recomendacion.Disposicion == RadarDeliveryDisposition.Recomendacion,
            "A: debe clasificarse como recomendación.");
        Verificar(recomendacion.DebePrepararDelivery,
            "A: una recomendación automática válida debe preparar Delivery.");

        RadarDeliveryDecision cynthiaSegura = Evaluar(
            recomendado: false,
            idInmueble: 187,
            puntuacion: 91,
            confirmado,
            permitirAlternativas: false);
        Verificar(!cynthiaSegura.DebePrepararDelivery,
            "B: Cynthia no debe preparar Delivery con política segura.");
        Verificar(cynthiaSegura.DebeTerminalAck &&
                  cynthiaSegura.DisposicionTerminal == "ALTERNATIVA_PARA_REVISION",
            "B: Cynthia debe terminar con ALTERNATIVA_PARA_REVISION.");

        RadarDeliveryDecision cynthiaHabilitada = Evaluar(
            recomendado: false,
            idInmueble: 187,
            puntuacion: 91,
            confirmado,
            permitirAlternativas: true);
        Verificar(cynthiaHabilitada.DebePrepararDelivery,
            "C: Cynthia debe preparar Delivery cuando las alternativas están habilitadas.");

        RadarDeliveryDecision alternativaCien = Evaluar(
            recomendado: false,
            idInmueble: 114,
            puntuacion: 100,
            confirmado,
            permitirAlternativas: false);
        Verificar(!alternativaCien.DebePrepararDelivery &&
                  alternativaCien.Disposicion == RadarDeliveryDisposition.AlternativaRevision,
            "D: una alternativa 100% no debe preparar Delivery por defecto.");

        RadarDeliveryDecision sinCandidato = Evaluar(
            recomendado: false,
            idInmueble: null,
            puntuacion: null,
            confirmado,
            permitirAlternativas: false);
        Verificar(!sinCandidato.DebePrepararDelivery &&
                  sinCandidato.DebeTerminalAck &&
                  sinCandidato.DisposicionTerminal == "SIN_COINCIDENCIA_UTIL",
            "E: cero candidatos debe terminar sin Delivery.");

        RadarDeliveryDecision transitorio = Evaluar(
            recomendado: false,
            idInmueble: 187,
            puntuacion: 91,
            RadarMatchingClientResult.TemporalmenteNoDisponible("Timeout"),
            permitirAlternativas: false);
        Verificar(transitorio.Disposicion == RadarDeliveryDisposition.Retry &&
                  !transitorio.DebePrepararDelivery &&
                  !transitorio.DebeTerminalAck,
            "F: matching transitorio debe conservar retry sin Delivery ni ACK.");

        RadarDeliveryDecision idSinScore = Evaluar(
            recomendado: true,
            idInmueble: 900,
            puntuacion: null,
            confirmado,
            permitirAlternativas: false);
        VerificarRetry(idSinScore, "G: ID sin score");

        RadarDeliveryDecision scoreSinId = Evaluar(
            recomendado: true,
            idInmueble: null,
            puntuacion: 100,
            confirmado,
            permitirAlternativas: false);
        VerificarRetry(scoreSinId, "H: score sin ID");

        VerificarRetry(Evaluar(false, 0, 91, confirmado, false), "I: ID cero");
        VerificarRetry(Evaluar(false, -1, 91, confirmado, false), "J: ID negativo");
        VerificarRetry(Evaluar(false, 187, 101, confirmado, false), "K: score mayor a 100");
        VerificarRetry(Evaluar(false, 187, -1, confirmado, false), "L: score negativo");
        VerificarRetry(Evaluar(false, 187, 54, confirmado, false), "M: score inferior a 55");
        VerificarRetry(Evaluar(true, null, null, confirmado, false),
            "N: recomendación automática sin estructura");
        VerificarRetry(Evaluar(true, 187, 84, confirmado, false),
            "O: recomendación automática debajo de 85");

        RadarDeliveryDecision[] multiple =
        [
            recomendacion,
            cynthiaSegura
        ];
        Verificar(multiple.Count(x => x.DebePrepararDelivery) == 1,
            "P: una recomendación y una alternativa deben producir un solo Delivery.");

        RadarDeliveryDecision incoherente = Evaluar(
            false, 187, null, confirmado, false);
        VerificarAgregacionSinAck([cynthiaSegura, incoherente], false,
            "Q: alternativa + incoherente");
        VerificarAgregacionSinAck([sinCandidato, incoherente], false,
            "R: sin coincidencia + incoherente");
        VerificarAgregacionSinAck([recomendacion, incoherente], true,
            "S: recomendación + incoherente");

        VerificarFlagConfiguracion();

        Console.WriteLine("A OK · recomendación automática prepara Delivery");
        Console.WriteLine("B OK · Cynthia segura termina ALTERNATIVA_PARA_REVISION");
        Console.WriteLine("C OK · Cynthia con alternativas habilitadas prepara Delivery");
        Console.WriteLine("D-S OK · alternativas, terminales, retry e inconsistencias protegidos");
        Console.WriteLine("DELIVERY_FLOW_REGRESSION_OK");
        return Task.CompletedTask;
    }

    private static void VerificarRetry(
        RadarDeliveryDecision decision,
        string escenario)
    {
        Verificar(decision.Disposicion == RadarDeliveryDisposition.Retry &&
                  !decision.DebePrepararDelivery &&
                  !decision.DebeTerminalAck &&
                  decision.DisposicionTerminal is null,
            $"{escenario} debe conservar retry sin Delivery ni ACK.");
    }

    private static void VerificarAgregacionSinAck(
        IEnumerable<RadarDeliveryDecision> decisiones,
        bool huboDeliveryCompletado,
        string escenario)
    {
        string? disposicion =
            RadarDeliveryFlowDecision.ResolverDisposicionTerminalGlobal(
                decisiones,
                bloqueoSafeLab: false,
                huboDeliveryCompletado);
        Verificar(disposicion is null,
            $"{escenario} no debe producir ACK terminal global.");
    }

    private static RadarDeliveryDecision Evaluar(
        bool recomendado,
        int? idInmueble,
        double? puntuacion,
        RadarMatchingClientResult matching,
        bool permitirAlternativas)
    {
        var solicitud = new SolicitudInmobiliaria
        {
            TieneRecomendacionAutomatica = recomendado,
            IdInmuebleCoincidente = idInmueble,
            MejorCoincidencia = puntuacion
        };

        return RadarDeliveryFlowDecision.Evaluar(
            solicitud,
            matching,
            permitirAlternativas);
    }

    private static void VerificarFlagConfiguracion()
    {
        const string variable =
            RadarDeliveryFlowDecision.AlternativeDeliveryEnvironmentVariable;
        string? anterior = Environment.GetEnvironmentVariable(variable);

        try
        {
            Environment.SetEnvironmentVariable(variable, null);
            Verificar(!RadarDeliveryFlowDecision.AlternativasDeliveryHabilitadas(),
                "El flag ausente debe ser false.");

            Environment.SetEnvironmentVariable(variable, "0");
            Verificar(!RadarDeliveryFlowDecision.AlternativasDeliveryHabilitadas(),
                "El flag 0 debe ser false.");

            Environment.SetEnvironmentVariable(variable, "false");
            Verificar(!RadarDeliveryFlowDecision.AlternativasDeliveryHabilitadas(),
                "El flag false debe ser false.");

            Environment.SetEnvironmentVariable(variable, "1");
            Verificar(RadarDeliveryFlowDecision.AlternativasDeliveryHabilitadas(),
                "El flag 1 debe ser true.");

            Environment.SetEnvironmentVariable(variable, "true");
            Verificar(RadarDeliveryFlowDecision.AlternativasDeliveryHabilitadas(),
                "El flag true debe ser true.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, anterior);
        }
    }

    private static void Verificar(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }
}
