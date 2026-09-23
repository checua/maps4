using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class AutoRecomendacionEspecificidadRegression
{
    private const string MotivoEspecificidad = "ESPECIFICIDAD_INSUFICIENTE";

    public static async Task RunAsync()
    {
        await VerificarMatrizAsync();
        await VerificarScoreIndependienteAsync();
        await VerificarFailClosedAsync();
        await VerificarEmpatesAsync();
        await VerificarPropagacionCentralAsync();
        VerificarDeliverySeguro();
        Console.WriteLine("AUTO_RECOMENDACION_ESPECIFICIDAD_REGRESSION_OK");
    }

    private static async Task VerificarMatrizAsync()
    {
        await CasoAsync("operacion-tipo", R(), automatica: false);
        await CasoAsync("zona", R(zona: true), automatica: false);
        await CasoAsync("precio", R(precio: true), automatica: false);
        await CasoAsync("multiples-campos-precio-una-categoria", R(precioCompleto: true), automatica: false);
        await CasoAsync("zona-precio", R(zona: true, precio: true), automatica: true);
        await CasoAsync("zona-recamaras", R(zona: true, recamaras: true), automatica: true);
        await CasoAsync("precio-superficie", R(precio: true, superficie: true), automatica: true);
        await CasoAsync("zona-banos", R(zona: true, banos: true), automatica: true);
        await CasoAsync("recamaras-banos-sin-ancla", R(recamaras: true, banos: true), automatica: false);
        await CasoAsync("booleanos-no-cuentan", R(booleanos: true), automatica: false);
        await CasoCentralAsync("requisitos-no-cuentan", zona: false, automatica: false);
        await CasoCentralAsync("zona-requisitos-no-cuentan", zona: true, automatica: false);
        await CasoAsync("zona-subtipo", R(zona: true, subtipo: true), automatica: true);
        await CasoAsync("precio-cochera", R(precio: true, cochera: true), automatica: true);
        await CasoAsync("zona-maximos-no-evaluados", R(zona: true, maximosHabitacionales: true), automatica: false);
    }

    private static async Task VerificarScoreIndependienteAsync()
    {
        RadarMatchingResultado bajaEspecificidad = await UnicoAsync(R());
        Exigir(bajaEspecificidad.Puntuacion == 100 && !bajaEspecificidad.EsRecomendacionAutomatica,
            "El score 100 con baja especificidad no se conservó como alternativa.");

        RadarMatchingResultado altaConScoreBajo = await UnicoAsync(
            R(zona: true, precioObjetivoLejano: true));
        Exigir(altaConScoreBajo.Puntuacion is >= 55 and < 85,
            $"El caso específico de score bajo no quedó como candidato. Score={altaConScoreBajo.Puntuacion}.");
        Exigir(!altaConScoreBajo.EsRecomendacionAutomatica &&
               altaConScoreBajo.MotivosNoRecomendacion.Any(x => x.Contains("Puntuación insuficiente", StringComparison.Ordinal)),
            "La especificidad alta eludió el umbral de 85%.");

        RadarMatchingResultado alta = await UnicoAsync(R(zona: true, precio: true));
        Exigir(alta.Puntuacion >= 85 && alta.EsRecomendacionAutomatica,
            "La solicitud específica con coincidencia alta fue bloqueada.");
    }

    private static async Task VerificarFailClosedAsync()
    {
        RadarMatchingRequest request = R(zona: true, precio: true);
        request.RestriccionesDurasNoVerificables = ["QA: NO VERIFICABLE"];
        RadarMatchingResponse resultado = await Servicio([Inmueble(1, 2_000_000)]).CompararAsync("qa@example.invalid", request);
        Exigir(resultado.TotalCandidatos == 0 && resultado.Resultados.Count == 0,
            "La nueva política rompió el fail-closed de restricciones no verificables.");
    }

    private static async Task VerificarEmpatesAsync()
    {
        List<InventarioInmuebleViewModel> inventario =
        [
            Inmueble(1, 3_000_000),
            Inmueble(2, 1_000_000),
            Inmueble(3, 2_000_000)
        ];

        RadarMatchingResponse pocoEspecifica = await Servicio(inventario).CompararAsync("qa@example.invalid", R(max: 2));
        Exigir(pocoEspecifica.TotalCandidatos == 3 && pocoEspecifica.Resultados.Count == 2,
            "MaxResultados o TotalCandidatos cambió con baja especificidad.");
        Exigir(pocoEspecifica.Resultados.All(x => !x.EsRecomendacionAutomatica) &&
               pocoEspecifica.Resultados.Select(x => x.IdInmueble).SequenceEqual([2, 3]),
            "El empate de alternativas no conservó el orden por precio.");

        RadarMatchingResponse especifica = await Servicio(inventario).CompararAsync(
            "qa@example.invalid", R(zona: true, precioMinimo: true, max: 2));
        Exigir(especifica.Resultados.All(x => x.EsRecomendacionAutomatica) &&
               especifica.Resultados.Select(x => x.IdInmueble).SequenceEqual([2, 3]),
            "El empate específico no conservó recomendación y orden por precio.");
    }

    private static async Task VerificarPropagacionCentralAsync()
    {
        SolicitudInmobiliaria solicitud = S(zona: false, requisitos: true);
        var intelligence = new IntelligenceFake(new RadarInterpretationResult
        {
            Motor = "QA",
            Solicitudes = [solicitud]
        });
        var processing = new RadarCentralProcessingService(
            intelligence,
            Servicio([Inmueble(77, 2_000_000)]));
        RadarInterpretationResult resultado = await processing.ProcesarAsync(
            "qa@example.invalid",
            new RadarMessage
            {
                MessageId = "AUTO-SPEC-QA",
                ChatOrigen = "RADAR-LAB",
                TextoOriginal = "Busco casa en venta"
            });

        SolicitudInmobiliaria procesada = resultado.Solicitudes.Single();
        Exigir(procesada.Accionabilidad?.Estado == RadarSolicitudAccionabilidadEstado.Accionable,
            "Operación + tipo dejó de ser accionable.");
        Exigir(procesada.MejorCoincidencia == 100 && procesada.IdInmuebleCoincidente == 77,
            "La mejor alternativa no se propagó al resultado central.");
        Exigir(!procesada.TieneRecomendacionAutomatica,
            "El resultado central convirtió la alternativa en recomendación.");
        Exigir(procesada.MatchingResumen?.Contains("ALTERNATIVAS PARA REVISAR", StringComparison.Ordinal) == true,
            "El resumen central no conservó la alternativa.");
    }

    private static void VerificarDeliverySeguro()
    {
        var solicitud = new SolicitudInmobiliaria
        {
            TieneRecomendacionAutomatica = false,
            IdInmuebleCoincidente = 77,
            MejorCoincidencia = 100
        };
        RadarDeliveryDecision decision = RadarDeliveryFlowDecision.Evaluar(
            solicitud,
            RadarMatchingClientResult.ResultadoConfirmado("ALTERNATIVA QA"),
            permitirDeliveryAlternativas: false);

        Exigir(decision.Disposicion == RadarDeliveryDisposition.AlternativaRevision &&
               !decision.DebePrepararDelivery &&
               decision.DebeTerminalAck &&
               decision.DisposicionTerminal == "ALTERNATIVA_PARA_REVISION",
            "La alternativa poco específica no conservó la política segura de Delivery.");
    }

    private static async Task CasoAsync(string nombre, RadarMatchingRequest request, bool automatica)
    {
        RadarMatchingResultado resultado = await UnicoAsync(request);
        Exigir(resultado.EsRecomendacionAutomatica == automatica,
            $"{nombre}: recomendación esperada={automatica}, real={resultado.EsRecomendacionAutomatica}.");
        Exigir(resultado.MotivosNoRecomendacion.Contains(MotivoEspecificidad) == !automatica,
            $"{nombre}: motivo de especificidad inesperado.");
    }

    private static async Task CasoCentralAsync(string nombre, bool zona, bool automatica)
    {
        SolicitudInmobiliaria solicitud = S(zona, requisitos: true);
        RadarMatchingRequest request = new()
        {
            Operacion = solicitud.Operacion,
            TiposPropiedad = [.. solicitud.TiposPropiedad],
            Zonas = [.. solicitud.Zonas],
            MaxResultados = 10
        };
        await CasoAsync(nombre, request, automatica);
    }

    private static async Task<RadarMatchingResultado> UnicoAsync(RadarMatchingRequest request)
    {
        RadarMatchingResponse response = await Servicio([Inmueble(1, 2_000_000)]).CompararAsync(
            "qa@example.invalid", request);
        Exigir(response.TotalCandidatos == 1 && response.Resultados.Count == 1,
            "El caso esperaba exactamente un candidato.");
        return response.Resultados.Single();
    }

    private static RadarMatchingService Servicio(List<InventarioInmuebleViewModel> inventario) =>
        new(new InventarioFake(inventario));

    private static RadarMatchingRequest R(
        bool zona = false,
        bool precio = false,
        bool precioCompleto = false,
        bool precioMinimo = false,
        bool precioObjetivoLejano = false,
        bool subtipo = false,
        bool recamaras = false,
        bool banos = false,
        bool superficie = false,
        bool cochera = false,
        bool booleanos = false,
        bool maximosHabitacionales = false,
        int max = 10) =>
        new()
        {
            Operacion = "Venta",
            TiposPropiedad = ["Casa"],
            Zonas = zona ? ["Centro"] : [],
            PrecioMaximo = precio || precioCompleto ? 2_000_000 : null,
            PrecioMinimo = precioMinimo || precioCompleto ? 500_000 : null,
            PrecioObjetivo = precioObjetivoLejano ? 5_000_000 : precioCompleto ? 2_000_000 : null,
            SubtiposPropiedad = subtipo ? ["Residencial"] : [],
            RecamarasMin = recamaras ? 3 : null,
            RecamarasMax = maximosHabitacionales ? 4 : null,
            BanosMin = banos ? 2 : null,
            BanosMax = maximosHabitacionales ? 3 : null,
            TerrenoMinM2 = superficie ? 150 : null,
            ConstruccionMinM2 = superficie ? 120 : null,
            CocheraMinAutos = cochera ? 2 : null,
            AceptaMascotas = booleanos ? true : null,
            Amueblado = booleanos ? false : null,
            UnaPlanta = booleanos ? true : null,
            CasetaVigilancia = booleanos ? true : null,
            MaxResultados = max
        };

    private static SolicitudInmobiliaria S(bool zona, bool requisitos) =>
        new()
        {
            Operacion = "Venta",
            TiposPropiedad = ["Casa"],
            Zonas = zona ? ["Centro"] : [],
            RequisitosAdicionales = requisitos ? "REQUISITO_QA_NO_EVALUABLE" : null
        };

    private static InventarioInmuebleViewModel Inmueble(int id, double precio) =>
        new()
        {
            IdInmueble = id,
            EstadoCodigo = "PUBLICADO",
            TipoNombre = "Casa",
            Observaciones = "Casa en venta residencial",
            ZonaPrincipalNombre = "Centro",
            Precio = precio,
            Recamaras = 3,
            BanosCompletos = 2,
            Estacionamientos = 2,
            Terreno = 180,
            Construccion = 150,
            Niveles = 1,
            AmenidadesCsv = "MASCOTAS,VIGILANCIA_24H"
        };

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class InventarioFake(List<InventarioInmuebleViewModel> inventario)
        : IInventarioRepository
    {
        public Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo) =>
            Task.FromResult(inventario);

        public Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor) =>
            throw new NotSupportedException();

        public Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo) =>
            throw new NotSupportedException();

        public Task CambiarEstadoOVisibilidadAsync(int idInmueble, string correo, string? estadoNuevo, string? visibilidadNueva, string? motivo) =>
            throw new NotSupportedException();

        public Task CerrarOperacionAsync(int idInmueble, string correo, string tipoOperacion, decimal precioCierre, DateTime fechaCierreUtc, string? notasCierre) =>
            throw new NotSupportedException();
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
}
