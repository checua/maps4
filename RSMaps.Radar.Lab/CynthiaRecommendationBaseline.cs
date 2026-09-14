using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
using System.Text.RegularExpressions;

internal static class CynthiaRecommendationBaseline
{
    public static async Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine(" CASO CYNTHIA - CALIDAD DE RECOMENDACION");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        const string mensajeOriginal =
            "Buenas noches busco casa de 2,000,000 efectivo " +
            "que sea de una planta con 3 recamaras";

        var inventario = new List<InventarioInmuebleViewModel>
        {
            new()
            {
                IdInmueble = 187,
                TipoNombre = "Casa en Venta",
                Precio = 1_750_000,
                Recamaras = 2,
                BanosCompletos = 2,
                Niveles = null,
                EstadoCodigo = "PUBLICADO"
            },

            new()
            {
                IdInmueble = 165,
                TipoNombre = "Casa en Renta",
                Precio = 15_000,
                EstadoCodigo = "PUBLICADO"
            },

            new()
            {
                IdInmueble = 132,
                TipoNombre = "Casa en Venta",
                Precio = 650_000,
                EstadoCodigo = "PUBLICADO"
            }
        };

        var intelligence =
            new FakeCentralIntelligence();

        var matching =
            new RadarMatchingService(
                new FakeInventarioRepository(inventario));

        var processing =
            new RadarCentralProcessingService(
                intelligence,
                matching);

        var mensaje = new RadarMessage
        {
            MessageId = "CYNTHIA-REGRESSION",
            ChatOrigen = "INVENTARIOS Y PROSPECTOS",
            Autor = "Cynthia Alvarado",
            TextoOriginal = mensajeOriginal,
            DetectadoEn = DateTime.Now
        };

        RadarInterpretationResult resultado =
            await processing.ProcesarAsync(
                "cynthia@test.local",
                mensaje);

        Verificar(
            resultado.Solicitudes.Count == 1,
            "Debe existir exactamente una solicitud.");

        SolicitudInmobiliaria solicitud =
            resultado.Solicitudes.Single();

        Console.WriteLine("INTERPRETACION FINAL");
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"Operación       : {solicitud.Operacion ?? "-"}");
        Console.WriteLine($"Tipo            : {string.Join(" | ", solicitud.TiposPropiedad)}");
        Console.WriteLine($"Precio máximo   : {solicitud.PrecioMaximo:N0}");
        Console.WriteLine($"Precio objetivo : {solicitud.PrecioObjetivo:N0}");
        Console.WriteLine($"Recámaras mín.  : {solicitud.RecamarasMin}");
        Console.WriteLine($"Una planta      : {solicitud.UnaPlanta}");
        Console.WriteLine($"Pago/crédito    : {string.Join(" | ", solicitud.ModalidadesPago)}");
        Console.WriteLine(
            $"Recomendación auto: {(solicitud.TieneRecomendacionAutomatica ? "SÍ" : "NO")}");
        Console.WriteLine();

        Console.WriteLine("MATCHING");
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine(solicitud.MatchingResumen);
        Console.WriteLine();

        int score187 =
            ExtraerPuntuacion(
                solicitud.MatchingResumen,
                187);

        int score132 =
            ExtraerPuntuacion(
                solicitud.MatchingResumen,
                132);

        Console.WriteLine(
            $"SCORES: #187={score187}% | #132={score132}%");

        Console.WriteLine();

        Verificar(
            string.Equals(
                solicitud.Operacion,
                "Venta",
                StringComparison.OrdinalIgnoreCase),
            "Contado debe inferir Venta.");

        Verificar(
            solicitud.PrecioObjetivo == 2_000_000m,
            $"Precio objetivo incorrecto: {solicitud.PrecioObjetivo}");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Inmueble #165",
                StringComparison.OrdinalIgnoreCase) != true,
            "#165 Casa en Renta jamás debe aparecer.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "ALTERNATIVAS PARA REVISAR",
                StringComparison.OrdinalIgnoreCase) == true,
            "El resumen de Cynthia debe identificarse como alternativas para revisar.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "RECOMENDACIONES",
                StringComparison.OrdinalIgnoreCase) != true,
            "Cynthia no debe mostrar sección de recomendaciones.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Tiene 2 recámaras",
                StringComparison.OrdinalIgnoreCase) == true,
            "El resumen debe explicar el incumplimiento de recámaras de #187.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Precio alejado",
                StringComparison.OrdinalIgnoreCase) == true,
            "El resumen debe explicar el alejamiento de precio de #132.");
        Verificar(
            score187 > score132,
            "#187 debe superar a #132.");

        Verificar(
            score187 >= 85,
            "#187 debe conservar score alto.");

        Verificar(
            score132 < 85,
            "#132 debe quedar debajo de score alto.");

        RadarMatchingResponse clasificacion =
            await matching.CompararAsync(
                "cynthia@test.local",
                new RadarMatchingRequest
                {
                    Operacion = "Venta",
                    TiposPropiedad = ["Casa"],
                    PrecioMaximo = 2_000_000m,
                    PrecioObjetivo = 2_000_000m,
                    RecamarasMin = 3,
                    UnaPlanta = true,
                    MaxResultados = 10
                });

        RadarMatchingResultado inmueble187 =
            clasificacion.Resultados.Single(
                x => x.IdInmueble == 187);

        RadarMatchingResultado inmueble132 =
            clasificacion.Resultados.Single(
                x => x.IdInmueble == 132);

        Console.WriteLine("CLASIFICACION COMERCIAL");
        Console.WriteLine("----------------------------------------------");

        MostrarClasificacion(inmueble187);
        MostrarClasificacion(inmueble132);

        Console.WriteLine();

        Verificar(
            !solicitud.TieneRecomendacionAutomatica,
            "Cynthia no debe tener recomendación automática.");

        Verificar(
            !inmueble187.EsRecomendacionAutomatica,
            "#187 debe ser ALTERNATIVA.");

        Verificar(
            inmueble187.MotivosNoRecomendacion.Any(
                x => x.Contains(
                    "recámaras",
                    StringComparison.OrdinalIgnoreCase)),
            "#187 debe advertir recámaras.");

        Verificar(
            inmueble187.MotivosNoRecomendacion.Any(
                x => x.Contains(
                    "una planta",
                    StringComparison.OrdinalIgnoreCase)),
            "#187 debe advertir una planta no confirmada.");

        Verificar(
            !inmueble132.EsRecomendacionAutomatica,
            "#132 debe ser ALTERNATIVA.");

        Verificar(
            inmueble132.MotivosNoRecomendacion.Any(
                x => x.Contains(
                    "Precio alejado",
                    StringComparison.OrdinalIgnoreCase)),
            "#132 debe advertir alejamiento del precio objetivo.");

        Console.WriteLine("ENCABEZADO ALERTA CYNTHIA");
        Console.WriteLine("----------------------------------------------");

        string encabezadoCynthia =
            RadarAlertPresentation.ConstruirEncabezado(
                solicitud);

        Console.WriteLine(encabezadoCynthia);
        Console.WriteLine();

        Verificar(
            encabezadoCynthia.Contains(
                "ALTERNATIVAS PARA REVISAR",
                StringComparison.OrdinalIgnoreCase),
            "Cynthia debe generar encabezado de alternativas.");

        Verificar(
            !encabezadoCynthia.Contains(
                "COINCIDENCIA",
                StringComparison.OrdinalIgnoreCase),
            "Una alternativa no debe presentarse como COINCIDENCIA.");
        Console.WriteLine("CONTROL POSITIVO");
        Console.WriteLine("----------------------------------------------");

        var matchingIdeal =
            new RadarMatchingService(
                new FakeInventarioRepository(
                    new List<InventarioInmuebleViewModel>
                    {
                        new()
                        {
                            IdInmueble = 900,
                            TipoNombre = "Casa en Venta",
                            Precio = 1_900_000,
                            Recamaras = 3,
                            BanosCompletos = 2,
                            Niveles = 1,
                            EstadoCodigo = "PUBLICADO"
                        }
                    }));

        RadarMatchingResponse ideal =
            await matchingIdeal.CompararAsync(
                "cynthia@test.local",
                new RadarMatchingRequest
                {
                    Operacion = "Venta",
                    TiposPropiedad = ["Casa"],
                    PrecioMaximo = 2_000_000m,
                    PrecioObjetivo = 2_000_000m,
                    RecamarasMin = 3,
                    UnaPlanta = true
                });

        RadarMatchingResultado recomendado =
            ideal.Resultados.Single();

        MostrarClasificacion(recomendado);

        Verificar(
            recomendado.EsRecomendacionAutomatica,
            "#900 debe ser RECOMENDADA.");

        Verificar(
            recomendado.MotivosNoRecomendacion.Count == 0,
            "#900 no debe tener motivos de exclusión.");
        var solicitudRecomendada =
            new SolicitudInmobiliaria
            {
                TieneRecomendacionAutomatica = true,
                MejorCoincidencia = recomendado.Puntuacion
            };

        string encabezadoRecomendado =
            RadarAlertPresentation.ConstruirEncabezado(
                solicitudRecomendada);

        Console.WriteLine(encabezadoRecomendado);

        Verificar(
            encabezadoRecomendado.Contains(
                "RECOMENDACIÓN",
                StringComparison.OrdinalIgnoreCase),
            "#900 debe generar encabezado de recomendación.");

        Verificar(
            encabezadoRecomendado.Contains(
                "100%",
                StringComparison.OrdinalIgnoreCase),
            "El encabezado recomendado debe mostrar su score.");

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("CONTROL OPERACION INDETERMINADA");
        Console.WriteLine("----------------------------------------------");

        RadarMatchingResponse operacionIndeterminada =
            await matchingIdeal.CompararAsync(
                "cynthia@test.local",
                new RadarMatchingRequest
                {
                    Operacion = null,
                    TiposPropiedad = ["Casa"],
                    PrecioMaximo = 2_000_000m,
                    PrecioObjetivo = 2_000_000m,
                    RecamarasMin = 3,
                    UnaPlanta = true
                });

        RadarMatchingResultado sinOperacion =
            operacionIndeterminada.Resultados.Single();

        MostrarClasificacion(sinOperacion);

        Verificar(
            !sinOperacion.EsRecomendacionAutomatica,
            "Una operación indeterminada no puede generar recomendación automática.");

        Verificar(
            sinOperacion.MotivosNoRecomendacion.Any(
                x => x.Contains(
                    "Operación no determinada",
                    StringComparison.OrdinalIgnoreCase)),
            "Debe explicar que la operación no está determinada.");

        Console.WriteLine(
            "✅ Operación indeterminada conserva candidato como ALTERNATIVA");
        Console.WriteLine();
        Console.WriteLine("VERIFICACIONES");
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine("✅ Contado infiere Venta");
        Console.WriteLine("✅ #165 Renta descartada");
        Console.WriteLine("✅ Precio objetivo diferencia #187 de #132");
        Console.WriteLine("✅ #187 = ALTERNATIVA");
        Console.WriteLine("✅ #132 = ALTERNATIVA");
        Console.WriteLine("✅ #900 = RECOMENDADA");
        Console.WriteLine();
        Console.WriteLine("✅ REGRESION CYNTHIA - CALIDAD SUPERADA");
    }

    private static void MostrarClasificacion(
        RadarMatchingResultado inmueble)
    {
        Console.WriteLine(
            $"#{inmueble.IdInmueble}: {inmueble.Puntuacion}% · " +
            $"{(inmueble.EsRecomendacionAutomatica ? "RECOMENDADA" : "ALTERNATIVA")}");

        foreach (string motivo in inmueble.MotivosNoRecomendacion)
            Console.WriteLine($"  ⚠ {motivo}");
    }

    private static int ExtraerPuntuacion(
        string? resumen,
        int idInmueble)
    {
        Match match = Regex.Match(
            resumen ?? string.Empty,
            $@"(?m)(?<score>\d+)% · Inmueble #{idInmueble}\b");

        if (!match.Success ||
            !int.TryParse(
                match.Groups["score"].Value,
                out int score))
        {
            throw new InvalidOperationException(
                $"No se pudo obtener score de #{idInmueble}.");
        }

        return score;
    }

    private static void Verificar(
        bool condicion,
        string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class FakeCentralIntelligence
        : IRadarCentralIntelligenceService
    {
        public bool Configurada => true;

        public Task<RadarInterpretationResult> InterpretarAsync(
            RadarMessage mensaje,
            CancellationToken cancellationToken = default)
        {
            var solicitud = new SolicitudInmobiliaria
            {
                ChatOrigen = mensaje.ChatOrigen,
                Autor = mensaje.Autor,
                MessageId = mensaje.MessageId,
                MensajeOriginal = mensaje.TextoOriginal,
                DetectadoEn = mensaje.DetectadoEn,

                Operacion = null,
                TiposPropiedad = ["Casa"],
                PrecioMinimo = null,
                PrecioMaximo = 2_000_000m,
                RecamarasMin = 3,
                UnaPlanta = true,
                ModalidadesPago = ["Contado"]
            };

            var resultado = new RadarInterpretationResult
            {
                Motor = "CYNTHIA-REGRESSION",
                Solicitudes = [solicitud]
            };

            return Task.FromResult(
                RadarInterpretationNormalizer.Normalizar(
                    resultado,
                    mensaje));
        }
    }

    private sealed class FakeInventarioRepository
        : IInventarioRepository
    {
        private readonly List<InventarioInmuebleViewModel> _inventario;

        public FakeInventarioRepository(
            List<InventarioInmuebleViewModel> inventario)
        {
            _inventario = inventario;
        }

        public Task<List<InventarioInmuebleViewModel>>
            ListarAutorizadosAsync(string correo) =>
            Task.FromResult(_inventario);

        public Task<List<InventarioInmuebleViewModel>>
            ListarAsync(int idCuenta, int idAsesor) =>
            Task.FromResult(_inventario);

        public Task<InventarioAutorizacionContexto?>
            ObtenerContextoAutorizacionAsync(string correo) =>
            Task.FromResult<InventarioAutorizacionContexto?>(null);

        public Task CambiarEstadoOVisibilidadAsync(
            int idInmueble,
            string correo,
            string? estadoNuevo,
            string? visibilidadNueva,
            string? motivo) =>
            Task.CompletedTask;

        public Task CerrarOperacionAsync(
            int idInmueble,
            string correo,
            string tipoOperacion,
            decimal precioCierre,
            DateTime fechaCierreUtc,
            string? notasCierre) =>
            Task.CompletedTask;
    }
}