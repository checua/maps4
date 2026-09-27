using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class FraccPrivadoRegression
{
    private const string NoVerificable =
        "no contiene información suficiente para verificar este requisito";

    public static async Task RunAsync()
    {
        Console.WriteLine("FRACC_PRIVADO_01");

        foreach (string expresion in new[]
        {
            "fracc privado",
            "fracc. privado",
            "fracc privada",
            "fracc. privada",
            "fraccionamiento privado",
            "fraccionamiento privada",
            "fraccionamiento cerrado",
            "fraccionamiento cerrada"
        })
        {
            SolicitudInmobiliaria normalizada = Normalizar(expresion);
            Exigir(normalizada.TipoFraccionamiento == "Privado",
                $"'{expresion}' no se normalizó como Privado.");
        }

        foreach (string expresionNegada in new[]
        {
            "no quiero fracc privado",
            "no quiero fraccionamiento privado",
            "que no sea fracc privado",
            "que no sea fraccionamiento privado",
            "sin fraccionamiento privado",
            "prefiero que no esté en fraccionamiento privado",
            "fraccionamiento abierto",
            "no privado"
        })
        {
            SolicitudInmobiliaria negada = Normalizar(
                expresionNegada,
                tipoFraccionamientoInicial: "Privado");
            Exigir(negada.TipoFraccionamiento != "Privado",
                $"La negación '{expresionNegada}' se convirtió en Privado.");
        }

        var porOrden = new SolicitudInmobiliaria
        {
            Zonas = ["fracc privado"]
        };
        RadarInterpretationResult resultadoOrden = RadarInterpretationNormalizer.Normalizar(
            new RadarInterpretationResult { Solicitudes = [porOrden] },
            Mensaje("casa en venta, máximo 2 millones, fracc privado, contado"));
        Exigir(resultadoOrden.Solicitudes.Single().TipoFraccionamiento == "Privado" &&
               resultadoOrden.Solicitudes.Single().Zonas.Count == 0,
            "El criterio no sobrevivió a la limpieza de zonas genéricas.");

        var candidatoDefault = new InventarioInmuebleViewModel();
        Exigir(candidatoDefault.TipoFraccionamiento is null,
            "El valor default del inventario debe ser null/SinDato.");

        SolicitudInmobiliaria solicitud = Normalizar(
            "Busco casa en venta de 2 millones en fracc privado, pago de contado.");
        solicitud.Operacion = "Venta";
        solicitud.TiposPropiedad = ["Casa"];
        solicitud.PrecioMaximo = 2_000_000m;
        solicitud.PrecioObjetivo = 2_000_000m;

        var inventario = new List<InventarioInmuebleViewModel>
        {
            Inmueble(601, 1_750_000, "Privado"),
            Inmueble(602, 1_750_000, "Abierto"),
            Inmueble(603, 1_750_000, "NoPrivado"),
            Inmueble(604, 1_750_000, "SinDato"),
            Inmueble(187, 1_750_000, null),
            Inmueble(116, 1_400_000, null),
            Inmueble(142, 1_395_000, null)
        };

        var matching = new RadarMatchingService(new FakeInventarioRepository(inventario));
        var processing = new RadarCentralProcessingService(
            new FakeCentralIntelligence(solicitud),
            matching);

        RadarInterpretationResult procesado = await processing.ProcesarAsync(
            "fracc-privado@test.local",
            Mensaje("caso-integracion"));

        SolicitudInmobiliaria final = procesado.Solicitudes.Single();
        Exigir(final.TipoFraccionamiento == "Privado",
            "TipoFraccionamiento no sobrevivió al procesamiento central.");
        Exigir(final.TieneRecomendacionAutomatica && final.IdInmuebleCoincidente == 601,
            "CrearMatchingRequest omitió o alteró TipoFraccionamiento.");

        RadarMatchingResponse resultado = await matching.CompararAsync(
            "fracc-privado@test.local",
            new RadarMatchingRequest
            {
                Operacion = final.Operacion,
                TiposPropiedad = [.. final.TiposPropiedad],
                PrecioMaximo = final.PrecioMaximo,
                PrecioObjetivo = final.PrecioObjetivo,
                TipoFraccionamiento = final.TipoFraccionamiento,
                MaxResultados = 10
            });

        RadarMatchingResultado privado = Obtener(resultado, 601);
        Exigir(privado.EsRecomendacionAutomatica,
            "Un candidato estructurado Privado debería poder recomendarse.");
        Exigir(privado.Coincidencias.Any(x => x.Contains("Fraccionamiento privado", StringComparison.OrdinalIgnoreCase)),
            "Falta la coincidencia explicable de fraccionamiento privado.");

        RadarMatchingResultado casoA = Obtener(
            await matching.CompararAsync(
                "fracc-privado@test.local",
                new RadarMatchingRequest
                {
                    Operacion = "Venta",
                    TiposPropiedad = ["Casa"],
                    PrecioMaximo = 2_000_000m,
                    MaxResultados = 10
                }),
            601);
        Exigir(!casoA.EsRecomendacionAutomatica,
            "Venta + Casa + Precio no debe bastar para auto-recomendación.");

        RadarMatchingResultado casoC = Obtener(
            await matching.CompararAsync(
                "fracc-privado@test.local",
                new RadarMatchingRequest
                {
                    Operacion = "Venta",
                    TiposPropiedad = ["Casa"],
                    TipoFraccionamiento = "Privado",
                    MaxResultados = 10
                }),
            601);
        Exigir(!casoC.EsRecomendacionAutomatica,
            "Venta + Casa + Privado sin zona/precio no debe bastar para auto-recomendación.");

        Exigir(resultado.Resultados.All(x => x.IdInmueble is not (602 or 603)),
            "Un candidato explícitamente Abierto/NoPrivado debe descartarse.");

        RadarMatchingResultado sinDatoExplicito = Obtener(resultado, 604);
        Exigir(!sinDatoExplicito.EsRecomendacionAutomatica,
            "El valor explícito SinDato no debe ser recomendación automática.");
        Exigir(sinDatoExplicito.MotivosNoRecomendacion.Any(x => x.Contains(NoVerificable, StringComparison.OrdinalIgnoreCase)),
            "SinDato no explica la no verificabilidad.");

        foreach (int id in new[] { 187, 116, 142 })
        {
            RadarMatchingResultado sinDato = Obtener(resultado, id);
            Exigir(!sinDato.EsRecomendacionAutomatica,
                $"#{id} sin dato no debe ser recomendación automática.");
            Exigir(sinDato.MotivosNoRecomendacion.Any(x => x.Contains(NoVerificable, StringComparison.OrdinalIgnoreCase)),
                $"#{id} no explica la no verificabilidad.");
            Exigir(sinDato.Diferencias.Any(x => x.Contains("No consta", StringComparison.OrdinalIgnoreCase)),
                $"#{id} no registra la diferencia como no verificable.");
        }
        Exigir(Obtener(resultado, 187).Puntuacion >= 85 &&
               !Obtener(resultado, 187).EsRecomendacionAutomatica,
            "SinDato debe bloquear recomendación incluso con score visualmente alto.");

        int[] scoresHistoricos = [97, 92, 85];
        int[] scoresActuales = new[] { 187, 116, 142 }
            .Select(id => Obtener(resultado, id).Puntuacion)
            .ToArray();
        Exigir(!scoresActuales.SequenceEqual(scoresHistoricos),
            "El caso histórico reprodujo scores que ignoraban fraccionamiento privado.");

        SolicitudInmobiliaria historica = Normalizar(
            "Casa en venta, máximo $2,000,000, fracc privado, contado.");
        historica.Operacion = "Venta";
        historica.TiposPropiedad = ["Casa"];
        historica.PrecioMaximo = 2_000_000m;
        historica.PrecioObjetivo = 2_000_000m;

        var processingHistorico = new RadarCentralProcessingService(
            new FakeCentralIntelligence(historica),
            new RadarMatchingService(new FakeInventarioRepository(
            [
                Inmueble(187, 1_750_000, null),
                Inmueble(116, 1_400_000, null),
                Inmueble(142, 1_395_000, null)
            ])));
        SolicitudInmobiliaria resultadoHistorico = (await processingHistorico.ProcesarAsync(
            "fracc-privado@test.local",
            Mensaje("FRACC_PRIVADO_01_HISTORICO"))).Solicitudes.Single();
        Exigir(!resultadoHistorico.TieneRecomendacionAutomatica,
            "El caso productivo no debe producir recomendaciones automáticas.");
        Exigir(resultadoHistorico.MatchingResumen?.Contains(
            "ALTERNATIVAS PARA REVISAR", StringComparison.Ordinal) == true,
            "El caso productivo no quedó claramente separado como alternativa.");
        Exigir(resultadoHistorico.MatchingResumen?.Contains(
            "no contiene información suficiente", StringComparison.OrdinalIgnoreCase) == true,
            "La salida productiva no explica la no verificabilidad.");
        Exigir(resultadoHistorico.MatchingResumen?.Contains(
            "✅ RECOMENDACIONES", StringComparison.Ordinal) != true,
            "El caso productivo se presentó bajo recomendaciones.");

        RadarDeliveryDecision deliverySeguro = RadarDeliveryFlowDecision.Evaluar(
            resultadoHistorico,
            RadarMatchingClientResult.ResultadoConfirmado(resultadoHistorico.MatchingResumen!),
            permitirDeliveryAlternativas: false);
        Exigir(!deliverySeguro.DebePrepararDelivery && deliverySeguro.DebeTerminalAck &&
               deliverySeguro.Disposicion == RadarDeliveryDisposition.AlternativaRevision,
            "Una alternativa SinDato intentó preparar Delivery con la política segura.");
        Exigir(RadarAlertPresentation.ConstruirEncabezado(resultadoHistorico).Contains(
            "ALTERNATIVAS PARA REVISAR", StringComparison.Ordinal),
            "La presentación downstream convirtió la alternativa en recomendación.");

        Console.WriteLine("SALIDA_PRODUCTIVA_SIMULADA_BEGIN");
        Console.WriteLine(resultadoHistorico.MatchingResumen);
        Console.WriteLine("SALIDA_PRODUCTIVA_SIMULADA_END");

        Console.WriteLine("NORMALIZACION_VARIANTES_OK");
        Console.WriteLine("NEGACION_NO_SE_CONVIERTE_EN_PRIVADO_OK");
        Console.WriteLine("ESTRUCTURADO_ANTES_DE_LIMPIEZA_ZONAS_OK");
        Console.WriteLine("DEFAULT_FAIL_CLOSED_OK");
        Console.WriteLine("PROPAGACION_TIPO_FRACCIONAMIENTO_OK");
        Console.WriteLine("PRIVADO_CUMPLE_OK");
        Console.WriteLine("ESPECIFICIDAD_A_B_C_OK");
        Console.WriteLine("NO_PRIVADO_DESCARTA_OK");
        Console.WriteLine("SIN_DATO_FAIL_CLOSED_OK");
        Console.WriteLine("EXPLICABILIDAD_NO_VERIFICABLE_OK");
        Console.WriteLine("HISTORICO_187_116_142_PROTEGIDO_OK");
        Console.WriteLine("DELIVERY_ALTERNATIVA_SEGURA_OK");
        Console.WriteLine("FRACC_PRIVADO_01_REGRESSION_OK");
    }

    private static SolicitudInmobiliaria Normalizar(
        string texto,
        string? tipoFraccionamientoInicial = null)
    {
        var solicitud = new SolicitudInmobiliaria
        {
            TipoFraccionamiento = tipoFraccionamientoInicial
        };
        var resultado = new RadarInterpretationResult { Solicitudes = [solicitud] };
        return RadarInterpretationNormalizer.Normalizar(resultado, Mensaje(texto))
            .Solicitudes.Single();
    }

    private static RadarMessage Mensaje(string texto) => new()
    {
        MessageId = "FRACC_PRIVADO_01",
        ChatOrigen = "RADAR-LAB",
        TextoOriginal = texto,
        DetectadoEn = DateTime.UtcNow
    };

    private static InventarioInmuebleViewModel Inmueble(
        int id,
        double precio,
        string? tipoFraccionamiento) => new()
    {
        IdInmueble = id,
        TipoNombre = "Casa en Venta",
        Precio = precio,
        EstadoCodigo = "PUBLICADO",
        TipoFraccionamiento = tipoFraccionamiento
    };

    private static RadarMatchingResultado Obtener(RadarMatchingResponse resultado, int id) =>
        resultado.Resultados.SingleOrDefault(x => x.IdInmueble == id)
        ?? throw new InvalidOperationException($"No se encontró el candidato #{id}.");

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class FakeCentralIntelligence(SolicitudInmobiliaria solicitud)
        : IRadarCentralIntelligenceService
    {
        public bool Configurada => true;

        public Task<RadarInterpretationResult> InterpretarAsync(
            RadarMessage mensaje,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RadarInterpretationResult
            {
                Motor = "FRACC_PRIVADO_REGRESSION",
                Solicitudes = [solicitud]
            });
    }

    private sealed class FakeInventarioRepository(List<InventarioInmuebleViewModel> inventario)
        : IInventarioRepository
    {
        public Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo) =>
            Task.FromResult(inventario);

        public Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor) =>
            Task.FromResult(inventario);

        public Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo) =>
            Task.FromResult<InventarioAutorizacionContexto?>(null);

        public Task CambiarEstadoOVisibilidadAsync(int idInmueble, string correo, string? estadoNuevo,
            string? visibilidadNueva, string? motivo) => Task.CompletedTask;

        public Task CerrarOperacionAsync(int idInmueble, string correo, string tipoOperacion,
            decimal precioCierre, DateTime fechaCierreUtc, string? notasCierre) => Task.CompletedTask;
    }
}
