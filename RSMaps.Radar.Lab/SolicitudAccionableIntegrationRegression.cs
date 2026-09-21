using maps4.Models;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
using System.Text.Json;

internal static class SolicitudAccionableIntegrationRegression
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync()
    {
        await VerificarBackendPorSolicitudAsync();
        await VerificarInterpretacionInvalidaAsync();
        await VerificarInterpretacionGlobalInvalidaAsync();
        VerificarContratoJsonLegacyYNuevo();
        VerificarListenerYRecovery();
        VerificarAgregacionMultiple();
        Console.WriteLine("SOLICITUD_ACCIONABLE_INTEGRATION_REGRESSION_OK");
    }

    private static async Task VerificarBackendPorSolicitudAsync()
    {
        SolicitudInmobiliaria accionable = S(op: true, tipo: true);
        SolicitudInmobiliaria necesitaDatos = S(op: true);
        SolicitudInmobiliaria contradictoria = S(op: true, tipo: true, contradiccion: true);
        var matching = new MatchingFake();
        RadarInterpretationResult resultado = await ProcesarAsync(
            [accionable, necesitaDatos, contradictoria],
            matching);

        Exigir(matching.Llamadas == 1, "Matching debe ejecutarse sólo para la solicitud accionable.");
        Exigir(Estado(resultado, 0) == RadarSolicitudAccionabilidadEstado.Accionable,
            "Operación + tipo no quedó accionable.");
        Exigir(!string.IsNullOrWhiteSpace(resultado.Solicitudes[0].MatchingResumen),
            "La solicitud accionable no conservó Matching durable.");
        Exigir(Estado(resultado, 1) == RadarSolicitudAccionabilidadEstado.NecesitaMasDatos,
            "La solicitud incompleta no quedó NecesitaMasDatos.");
        ExigirSinMatching(resultado.Solicitudes[1]);
        Exigir(Estado(resultado, 2) == RadarSolicitudAccionabilidadEstado.Inconsistente &&
               resultado.Solicitudes[2].Accionabilidad!.Motivos.Contains(
                   RadarSolicitudAccionabilidadMotivo.DatosContradictorios),
            "La contradicción no quedó persistida como inconsistente.");
        ExigirSinMatching(resultado.Solicitudes[2]);

        var matchingNulo = new MatchingFake();
        RadarInterpretationResult sinAccionables = await ProcesarAsync(
            [S(op: true), S(tipo: true)],
            matchingNulo);
        Exigir(matchingNulo.Llamadas == 0, "Matching se ejecutó para solicitudes NecesitaMasDatos.");
        Exigir(sinAccionables.Solicitudes.All(x =>
                x.Accionabilidad?.Estado == RadarSolicitudAccionabilidadEstado.NecesitaMasDatos),
            "Las solicitudes incompletas se contaminaron entre sí.");
    }

    private static async Task VerificarInterpretacionInvalidaAsync()
    {
        var matching = new MatchingFake();
        bool fallo = false;
        try
        {
            await ProcesarAsync(
                [S(op: true, tipo: true), S(op: true, tipo: true, tipoNombre: "Tipo no canónico")],
                matching);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("reintentarse", StringComparison.OrdinalIgnoreCase))
        {
            fallo = true;
        }

        Exigir(fallo, "La interpretación inválida no provocó fallo reintentable central.");
        Exigir(matching.Llamadas == 0,
            "Matching parcial se ejecutó antes de rechazar una interpretación inválida.");
    }

    private static async Task VerificarInterpretacionGlobalInvalidaAsync()
    {
        var matching = new MatchingFake();
        var mensaje = new RadarMessage
        {
            MessageId = "INTEGRATION-QA-GLOBAL",
            ChatOrigen = "RADAR-LAB",
            TextoOriginal = "Casa en venta y terreno en renta"
        };
        var intelligence = new IntelligenceFake(new RadarInterpretationResult
        {
            Motor = "QA",
            Solicitudes = [S(op: true, tipo: true)]
        });
        var processing = new RadarCentralProcessingService(intelligence, matching);
        bool fallo = false;
        try
        {
            await processing.ProcesarAsync("qa@example.invalid", mensaje);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("global incompleta", StringComparison.OrdinalIgnoreCase))
        {
            fallo = true;
        }

        Exigir(fallo, "La interpretación global incompleta no provocó retry central.");
        Exigir(matching.Llamadas == 0,
            "Matching se ejecutó antes de rechazar una interpretación global incompleta.");
    }

    private static void VerificarContratoJsonLegacyYNuevo()
    {
        const string legacyJson = "{\"operacion\":\"Venta\",\"tiposPropiedad\":[\"Casa\"]}";
        SolicitudInmobiliaria legacy = JsonSerializer.Deserialize<SolicitudInmobiliaria>(
            legacyJson,
            WebJson) ?? throw new InvalidOperationException("No se deserializó JSON legacy.");
        Exigir(legacy.Accionabilidad is null,
            "JSON legacy obtuvo un default accidental de accionabilidad.");

        SolicitudInmobiliaria nueva = S(op: true, tipo: true);
        nueva.Accionabilidad = RadarSolicitudAccionabilidadEvaluator.Evaluar(
            nueva,
            RadarSolicitudAccionabilidadPolitica.OperacionYTipo);
        string jsonNuevo = JsonSerializer.Serialize(nueva, WebJson);
        Exigir(jsonNuevo.Contains("\"accionabilidad\"", StringComparison.Ordinal),
            "JSON nuevo no contiene accionabilidad.");
        SolicitudInmobiliaria reconstruida = JsonSerializer.Deserialize<SolicitudInmobiliaria>(
            jsonNuevo,
            WebJson) ?? throw new InvalidOperationException("No se deserializó JSON nuevo.");
        Exigir(reconstruida.Accionabilidad?.Estado == RadarSolicitudAccionabilidadEstado.Accionable,
            "JSON nuevo perdió la decisión persistida.");
    }

    private static void VerificarListenerYRecovery()
    {
        SolicitudInmobiliaria legacy = S(op: true, tipo: true);
        Exigir(RadarDeliveryFlowDecision.EvaluarAntesDeMatching(legacy) is null,
            "El contrato legacy dejó de usar el flujo histórico.");

        SolicitudInmobiliaria accionable = S(op: true, tipo: true);
        accionable.Accionabilidad = Decision(RadarSolicitudAccionabilidadEstado.Accionable);
        accionable.MatchingResumen = "MATCHING_DURABLE_PRUEBA";
        Exigir(RadarDeliveryFlowDecision.EvaluarAntesDeMatching(accionable) is null,
            "Una solicitud nueva accionable con Matching durable fue bloqueada.");

        accionable.MatchingResumen = null;
        RadarDeliveryDecision sinMatching = RadarDeliveryFlowDecision.EvaluarAntesDeMatching(accionable)!;
        Exigir(sinMatching.Disposicion == RadarDeliveryDisposition.Retry && !sinMatching.DebeTerminalAck,
            "Una solicitud accionable sin Matching durable cayó en fallback en vez de retry.");

        SolicitudInmobiliaria necesita = S(op: true);
        necesita.Accionabilidad = Decision(
            RadarSolicitudAccionabilidadEstado.NecesitaMasDatos,
            RadarSolicitudAccionabilidadMotivo.SinTipoInmueble);
        RadarDeliveryDecision decisionNecesita = RadarDeliveryFlowDecision.EvaluarAntesDeMatching(necesita)!;
        Exigir(decisionNecesita.DisposicionTerminal == "NECESITA_MAS_DATOS" &&
               !decisionNecesita.DebePrepararDelivery,
            "NecesitaMasDatos no obtuvo ACK terminal específico sin Delivery.");

        SolicitudInmobiliaria contradictoria = S(op: true, tipo: true, contradiccion: true);
        contradictoria.Accionabilidad = Decision(
            RadarSolicitudAccionabilidadEstado.Inconsistente,
            RadarSolicitudAccionabilidadMotivo.DatosContradictorios);
        RadarDeliveryDecision decisionContradictoria =
            RadarDeliveryFlowDecision.EvaluarAntesDeMatching(contradictoria)!;
        Exigir(decisionContradictoria.DisposicionTerminal == "DATOS_CONTRADICTORIOS" &&
               !decisionContradictoria.DebePrepararDelivery,
            "DatosContradictorios no obtuvo ACK terminal específico sin Delivery.");

        SolicitudInmobiliaria invalida = S(op: true, tipo: true);
        invalida.Accionabilidad = Decision(
            RadarSolicitudAccionabilidadEstado.Inconsistente,
            RadarSolicitudAccionabilidadMotivo.InterpretacionInvalida);
        RadarDeliveryDecision decisionInvalida = RadarDeliveryFlowDecision.EvaluarAntesDeMatching(invalida)!;
        Exigir(decisionInvalida.Disposicion == RadarDeliveryDisposition.Retry &&
               !decisionInvalida.DebeTerminalAck,
            "InterpretacionInvalida recibió un ACK terminal silencioso.");

        accionable.MatchingResumen = "SIN_COINCIDENCIAS_DURABLE";
        accionable.MejorCoincidencia = null;
        accionable.IdInmuebleCoincidente = null;
        RadarDeliveryDecision cero = RadarDeliveryFlowDecision.Evaluar(
            accionable,
            RadarMatchingClientResult.ResultadoConfirmado(accionable.MatchingResumen),
            permitirDeliveryAlternativas: false);
        Exigir(cero.DisposicionTerminal == "SIN_COINCIDENCIA_UTIL",
            "Un Matching ejecutado con cero coincidencias perdió su semántica histórica.");
    }

    private static void VerificarAgregacionMultiple()
    {
        RadarDeliveryDecision necesita = Terminal(
            RadarDeliveryDisposition.NecesitaMasDatos,
            "NECESITA_MAS_DATOS");
        RadarDeliveryDecision contradiccion = Terminal(
            RadarDeliveryDisposition.DatosContradictorios,
            "DATOS_CONTRADICTORIOS");
        RadarDeliveryDecision cero = Terminal(
            RadarDeliveryDisposition.SinCoincidencia,
            "SIN_COINCIDENCIA_UTIL");

        Exigir(RadarDeliveryFlowDecision.ResolverDisposicionTerminalGlobal(
                [necesita, cero], false, false) == "NECESITA_MAS_DATOS",
            "La agregación no preservó NecesitaMasDatos junto a cero coincidencias.");
        Exigir(RadarDeliveryFlowDecision.ResolverDisposicionTerminalGlobal(
                [contradiccion, necesita], false, false) == "DATOS_CONTRADICTORIOS",
            "La agregación no priorizó datos contradictorios.");
        Exigir(RadarDeliveryFlowDecision.ResolverDisposicionTerminalGlobal(
                [necesita], false, true) == "ALERTA_ENTREGADA",
            "Una entrega completada perdió la disposición histórica.");
    }

    private static async Task<RadarInterpretationResult> ProcesarAsync(
        List<SolicitudInmobiliaria> solicitudes,
        MatchingFake matching)
    {
        var mensaje = new RadarMessage
        {
            MessageId = "INTEGRATION-QA",
            ChatOrigen = "RADAR-LAB",
            TextoOriginal = "Solicitud inmobiliaria sintética"
        };
        var intelligence = new IntelligenceFake(new RadarInterpretationResult
        {
            Motor = "QA",
            Solicitudes = solicitudes
        });
        var processing = new RadarCentralProcessingService(intelligence, matching);
        return await processing.ProcesarAsync("qa@example.invalid", mensaje);
    }

    private static SolicitudInmobiliaria S(
        bool op = false,
        bool tipo = false,
        bool contradiccion = false,
        string tipoNombre = "Casa") =>
        new()
        {
            Operacion = op ? "Venta" : null,
            TiposPropiedad = tipo ? [tipoNombre] : [],
            PrecioMinimo = contradiccion ? 2_000_000 : null,
            PrecioMaximo = contradiccion ? 1_000_000 : null
        };

    private static RadarSolicitudAccionabilidadEstado? Estado(
        RadarInterpretationResult resultado,
        int indice) =>
        resultado.Solicitudes[indice].Accionabilidad?.Estado;

    private static RadarSolicitudAccionabilidadDecision Decision(
        RadarSolicitudAccionabilidadEstado estado,
        params RadarSolicitudAccionabilidadMotivo[] motivos) =>
        new(estado, motivos, 0, []);

    private static RadarDeliveryDecision Terminal(
        RadarDeliveryDisposition disposicion,
        string terminal) =>
        new(disposicion, false, true, terminal, "QA");

    private static void ExigirSinMatching(SolicitudInmobiliaria solicitud)
    {
        Exigir(solicitud.MatchingResumen is null &&
               solicitud.MejorCoincidencia is null &&
               solicitud.IdInmuebleCoincidente is null &&
               !solicitud.TieneRecomendacionAutomatica,
            "Una solicitud sin Matching contiene datos que parecen resultado real.");
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class IntelligenceFake(RadarInterpretationResult resultado)
        : IRadarCentralIntelligenceService
    {
        public bool Configurada => true;

        public Task<RadarInterpretationResult> InterpretarAsync(
            RadarMessage mensaje,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(resultado);
    }

    private sealed class MatchingFake : IRadarMatchingService
    {
        public int Llamadas { get; private set; }

        public Task<RadarMatchingResponse> CompararAsync(
            string correo,
            RadarMatchingRequest solicitud)
        {
            Llamadas++;
            return Task.FromResult(new RadarMatchingResponse
            {
                TotalInventarioEvaluado = 1,
                TotalCandidatos = 1,
                Resultados =
                [
                    new RadarMatchingResultado
                    {
                        IdInmueble = 501,
                        Puntuacion = 90,
                        EsRecomendacionAutomatica = true,
                        TipoNombre = "Casa"
                    }
                ]
            });
        }
    }
}
