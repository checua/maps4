using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class PaymentHardConstraintsRegression
{
    public static async Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine(" RADAR PAYMENT HARD-CONSTRAINTS REGRESSION");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        var casos = new[]
        {
            new Caso(
                "infonavit",
                "Infonavit",
                "Busco casa en venta hasta $2,000,000, Infonavit.",
                "PAGO: INFONAVIT",
                false),

            new Caso(
                "fovissste",
                "Fovissste",
                "Busco casa en venta hasta $2,000,000, Fovissste.",
                "PAGO: FOVISSSTE",
                false),

            new Caso(
                "banjercito",
                "Banjercito",
                "Busco casa en venta hasta $2,000,000, Banjercito.",
                "PAGO: BANJERCITO",
                false),

            new Caso(
                "credito-bancario",
                "Crédito bancario",
                "Busco casa en venta hasta $2,000,000 con crédito bancario.",
                "PAGO: CRÉDITO BANCARIO",
                false),

            new Caso(
                "credito-hipotecario",
                "Crédito hipotecario",
                "Busco casa en venta hasta $2,000,000 con crédito hipotecario.",
                "PAGO: CRÉDITO HIPOTECARIO",
                false),

            new Caso(
                "contado",
                "Contado",
                "Busco casa en venta hasta $2,000,000, pago de contado.",
                null,
                true),

            new Caso(
                "sin-modalidad",
                null,
                "Busco casa en venta hasta $2,000,000.",
                null,
                true)
        };

        foreach (Caso caso in casos)
        {
            await EjecutarCasoAsync(caso);
        }

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine(" ✅ REGRESION DE FORMAS DE PAGO SUPERADA");
        Console.WriteLine("==============================================");
    }

    private static async Task EjecutarCasoAsync(Caso caso)
    {
        var inventario = new List<InventarioInmuebleViewModel>
        {
            new()
            {
                IdInmueble = 501,
                TipoNombre = "Casa en Venta",
                Precio = 1_500_000,
                EstadoCodigo = "PUBLICADO"
            }
        };

        var intelligence =
            new FakeCentralIntelligence(caso.Modalidad);

        var matching =
            new RadarMatchingService(
                new FakeInventarioRepository(inventario));

        var processing =
            new RadarCentralProcessingService(
                intelligence,
                matching);

        var mensaje = new RadarMessage
        {
            MessageId = $"payment-regression-{caso.Id}",
            ChatOrigen = "RADAR-PAYMENT-REGRESSION",
            TextoOriginal = caso.Mensaje,
            DetectadoEn = DateTime.Now
        };

        RadarInterpretationResult resultado =
            await processing.ProcesarAsync(
                "radar-payment@test.local",
                mensaje);

        Verificar(
            resultado.Solicitudes.Count == 1,
            $"{caso.Id}: se esperaba exactamente una solicitud.");

        SolicitudInmobiliaria solicitud =
            resultado.Solicitudes.Single();

        bool tieneRestriccionEsperada =
            caso.RestriccionEsperada is not null &&
            solicitud.RestriccionesDurasNoVerificables.Any(
                x => string.Equals(
                    x,
                    caso.RestriccionEsperada,
                    StringComparison.OrdinalIgnoreCase));

        if (caso.RestriccionEsperada is not null)
        {
            Verificar(
                tieneRestriccionEsperada,
                $"{caso.Id}: falta '{caso.RestriccionEsperada}'.");
        }
        else
        {
            Verificar(
                solicitud.RestriccionesDurasNoVerificables.Count == 0,
                $"{caso.Id}: no debería crear restricciones duras.");
        }

        if (!string.IsNullOrWhiteSpace(caso.Modalidad))
        {
            Verificar(
                solicitud.ModalidadesPago.Any(
                    x => string.Equals(
                        x,
                        caso.Modalidad,
                        StringComparison.OrdinalIgnoreCase)),
                $"{caso.Id}: la modalidad debe conservarse.");
        }
        else
        {
            Verificar(
                solicitud.ModalidadesPago.Count == 0,
                $"{caso.Id}: no debería inventar modalidad de pago.");
        }

        if (caso.EsperaMatch)
        {
            Verificar(
                solicitud.IdInmuebleCoincidente == 501,
                $"{caso.Id}: debería conservar el matching normal.");

            Verificar(
                solicitud.MatchingResumen?.Contains(
                    "Inmueble #501",
                    StringComparison.OrdinalIgnoreCase) == true,
                $"{caso.Id}: el resumen debería incluir #501.");
        }
        else
        {
            Verificar(
                solicitud.IdInmuebleCoincidente is null,
                $"{caso.Id}: una modalidad no verificable no debe producir match.");

            Verificar(
                solicitud.MatchingResumen?.Contains(
                    "Sin coincidencias útiles actuales",
                    StringComparison.OrdinalIgnoreCase) == true,
                $"{caso.Id}: debería operar fail-closed.");
        }

        string modalidad =
            solicitud.ModalidadesPago.Count == 0
                ? "-"
                : string.Join(" | ", solicitud.ModalidadesPago);

        string restricciones =
            solicitud.RestriccionesDurasNoVerificables.Count == 0
                ? "-"
                : string.Join(
                    " | ",
                    solicitud.RestriccionesDurasNoVerificables);

        string match =
            solicitud.IdInmuebleCoincidente.HasValue
                ? $"#{solicitud.IdInmuebleCoincidente}"
                : "NO MATCH";

        Console.WriteLine(
            $"✅ {caso.Id,-21} " +
            $"Pago={modalidad,-20} " +
            $"Hard={restricciones,-30} " +
            $"Resultado={match}");
    }

    private static void Verificar(
        bool condicion,
        string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed record Caso(
        string Id,
        string? Modalidad,
        string Mensaje,
        string? RestriccionEsperada,
        bool EsperaMatch);

    private sealed class FakeCentralIntelligence
        : IRadarCentralIntelligenceService
    {
        private readonly string? _modalidad;

        public FakeCentralIntelligence(
            string? modalidad)
        {
            _modalidad = modalidad;
        }

        public bool Configurada => true;

        public Task<RadarInterpretationResult> InterpretarAsync(
            RadarMessage mensaje,
            CancellationToken cancellationToken = default)
        {
            var solicitud = new SolicitudInmobiliaria
            {
                ChatOrigen = mensaje.ChatOrigen,
                MessageId = mensaje.MessageId,
                MensajeOriginal = mensaje.TextoOriginal,
                DetectadoEn = mensaje.DetectadoEn,
                Operacion = "Venta",
                TiposPropiedad = ["Casa"],
                PrecioMaximo = 2_000_000m,
                ModalidadesPago =
                    string.IsNullOrWhiteSpace(_modalidad)
                        ? []
                        : [_modalidad]
            };

            var resultado = new RadarInterpretationResult
            {
                Motor = "PAYMENT_REGRESSION",
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