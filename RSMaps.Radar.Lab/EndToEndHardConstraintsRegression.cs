using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class EndToEndHardConstraintsRegression
{
    public static async Task RunAsync(IRadarInterpreter interpreter)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine(" RADAR E2E HARD-CONSTRAINTS REGRESSION");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        const string texto =
            "BUSCO CASA / DEPARTAMENTO EN RENTA. Máximo $13,000. " +
            "Sin amueblar. NO orillas. Lo más nuevo posible.";

        var mensaje = new RadarMessage
        {
            MessageId = "regression-184-no-orillas",
            ChatOrigen = "RADAR-LAB-E2E",
            TextoOriginal = texto,
            DetectadoEn = DateTime.Now
        };

        var inventario = new List<InventarioInmuebleViewModel>
        {
            new()
            {
                IdInmueble = 184,
                TipoNombre = "Casa en Renta",
                Precio = 15000,
                EstadoCodigo = "PUBLICADO"
            },
            new()
            {
                IdInmueble = 130,
                TipoNombre = "Casa en Renta",
                Precio = 13000,
                EstadoCodigo = "PUBLICADO"
            },
            new()
            {
                IdInmueble = 999,
                TipoNombre = "Casa en Renta",
                Precio = null,
                EstadoCodigo = "PUBLICADO"
            }
        };

        var intelligence =
            new InterpreterCentralAdapter(interpreter);

        var matching =
            new RadarMatchingService(
                new FakeInventarioRepository(inventario));

        var processing =
            new RadarCentralProcessingService(
                intelligence,
                matching);

        RadarInterpretationResult resultado =
            await processing.ProcesarAsync(
                "radar-lab@test.local",
                mensaje);

        Verificar(
            resultado.Solicitudes.Count == 1,
            $"Se esperaba 1 solicitud. Real: {resultado.Solicitudes.Count}");

        SolicitudInmobiliaria solicitud =
            resultado.Solicitudes.Single();

        Console.WriteLine($"Motor: {resultado.Motor}");
        Console.WriteLine($"Operación: {solicitud.Operacion}");
        Console.WriteLine($"Tipos: {string.Join(" | ", solicitud.TiposPropiedad)}");
        Console.WriteLine($"Precio máximo: {solicitud.PrecioMaximo:C0}");
        Console.WriteLine($"Amueblado: {solicitud.Amueblado}");
        Console.WriteLine(
            $"Restricciones duras: {string.Join(" | ", solicitud.RestriccionesDurasNoVerificables)}");
        Console.WriteLine($"Requisitos: {solicitud.RequisitosAdicionales ?? "-"}");
        Console.WriteLine();
        Console.WriteLine("MATCHING:");
        Console.WriteLine(solicitud.MatchingResumen ?? "(sin resumen)");
        Console.WriteLine();

        Verificar(
            solicitud.PrecioMaximo == 13000m,
            $"Precio máximo incorrecto: {solicitud.PrecioMaximo}");

        Verificar(
            solicitud.Amueblado == false,
            "La solicitud debe conservar Sin amueblar.");

        Verificar(
            solicitud.RestriccionesDurasNoVerificables.Any(
                x => string.Equals(
                    x,
                    "NO ORILLAS",
                    StringComparison.OrdinalIgnoreCase)),
            "NO ORILLAS no llegó al procesamiento central.");

        Verificar(
            solicitud.RestriccionesDurasNoVerificables.Any(
                x => string.Equals(
                    x,
                    "SIN AMUEBLAR",
                    StringComparison.OrdinalIgnoreCase)),
            "SIN AMUEBLAR no llegó al procesamiento central.");

        Verificar(
            solicitud.RestriccionesDurasNoVerificables.Count == 2,
            $"Se esperaban exactamente 2 restricciones duras. Real: {solicitud.RestriccionesDurasNoVerificables.Count}");

        Verificar(
            solicitud.RequisitosAdicionales?.Contains(
                "nuevo",
                StringComparison.OrdinalIgnoreCase) == true,
            "La preferencia blanda 'Lo más nuevo posible' debe conservarse en RequisitosAdicionales.");

        Verificar(
            solicitud.IdInmuebleCoincidente is null,
            $"No debe existir inmueble coincidente. Real: #{solicitud.IdInmuebleCoincidente}");

        Verificar(
            !solicitud.MejorCoincidencia.HasValue,
            $"No debe existir puntuación de coincidencia. Real: {solicitud.MejorCoincidencia}");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Sin coincidencias útiles actuales",
                StringComparison.OrdinalIgnoreCase) == true,
            "El resumen debe indicar que no existen coincidencias útiles.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Inventario evaluado: 3",
                StringComparison.OrdinalIgnoreCase) == true,
            "El matching debe haber evaluado los 3 inmuebles simulados.");

        Verificar(
            solicitud.MatchingResumen?.Contains(
                "Inmueble #184",
                StringComparison.OrdinalIgnoreCase) != true,
            "#184 jamás debe aparecer en el resumen.");

        Console.WriteLine("✅ Mensaje interpretado");
        Console.WriteLine("✅ Precio máximo preservado");
        Console.WriteLine("✅ NO ORILLAS preservado");
        Console.WriteLine("✅ Matching ejecutado");
        Console.WriteLine("✅ #184 no fue sugerido");
        Console.WriteLine("✅ Candidatos útiles: 0");
        Console.WriteLine("✅ NO ALERT");
        Console.WriteLine();
        Console.WriteLine("✅ REGRESION E2E SUPERADA");
    }

    private static void Verificar(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class InterpreterCentralAdapter
        : IRadarCentralIntelligenceService
    {
        private readonly IRadarInterpreter _interpreter;

        public InterpreterCentralAdapter(
            IRadarInterpreter interpreter)
        {
            _interpreter = interpreter;
        }

        public bool Configurada => true;

        public Task<RadarInterpretationResult> InterpretarAsync(
            RadarMessage mensaje,
            CancellationToken cancellationToken = default)
        {
            return _interpreter.InterpretarAsync(
                mensaje,
                cancellationToken);
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