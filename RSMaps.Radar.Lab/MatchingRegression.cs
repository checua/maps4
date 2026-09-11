using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;

internal static class MatchingRegression
{
    public static async Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("     RADAR MATCHING REGRESSION");
        Console.WriteLine("==============================================");
        Console.WriteLine("Caso real: máximo $13,000 vs inmueble #184 $15,000");
        Console.WriteLine();

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

        var repository = new FakeInventarioRepository(inventario);
        var service = new RadarMatchingService(repository);

        var solicitud = new RadarMatchingRequest
        {
            Operacion = "Renta",
            TiposPropiedad = new List<string> { "Casa", "Departamento" },
            PrecioMaximo = 13000m,
            MaxResultados = 10
        };

        RadarMatchingResponse resultado =
            await service.CompararAsync("radar-lab@test.local", solicitud);

        Verificar(
            resultado.TotalInventarioEvaluado == 3,
            $"Inventario evaluado esperado: 3. Real: {resultado.TotalInventarioEvaluado}");

        Verificar(
            resultado.TotalCandidatos == 1,
            $"Candidatos esperados: 1. Real: {resultado.TotalCandidatos}");

        Verificar(
            resultado.Resultados.Count == 1,
            $"Resultados esperados: 1. Real: {resultado.Resultados.Count}");

        Verificar(
            resultado.Resultados.Single().IdInmueble == 130,
            $"El único candidato debe ser #130. Real: #{resultado.Resultados.Single().IdInmueble}");

        Verificar(
            resultado.Resultados.All(x => x.IdInmueble != 184),
            "ERROR: #184 no debe aparecer porque $15,000 supera el máximo $13,000.");

        Verificar(
            resultado.Resultados.All(x => x.IdInmueble != 999),
            "ERROR: #999 no debe aparecer porque no tiene precio verificable.");

        Console.WriteLine("✅ #184 ($15,000): DESCARTADO");
        Console.WriteLine("✅ #130 ($13,000): ACEPTADO");
        Console.WriteLine("✅ #999 (sin precio): DESCARTADO");
        Console.WriteLine();
        Console.WriteLine("✅ REGRESION DE PRECIO SUPERADA");

        Console.WriteLine();
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine("REGRESION FAIL-CLOSED: NO ORILLAS");
        Console.WriteLine("----------------------------------------------");

        var solicitudNoOrillas = new RadarMatchingRequest
        {
            Operacion = "Renta",
            TiposPropiedad = new List<string> { "Casa", "Departamento" },
            PrecioMaximo = 13000m,
            RestriccionesDurasNoVerificables = new List<string>
            {
                "NO ORILLAS"
            },
            MaxResultados = 10
        };

        RadarMatchingResponse resultadoNoOrillas =
            await service.CompararAsync(
                "radar-lab@test.local",
                solicitudNoOrillas);

        Verificar(
            resultadoNoOrillas.TotalInventarioEvaluado == 3,
            $"Inventario evaluado esperado: 3. Real: {resultadoNoOrillas.TotalInventarioEvaluado}");

        Verificar(
            resultadoNoOrillas.TotalCandidatos == 0,
            $"Con una restricción dura no verificable deben existir 0 candidatos. Real: {resultadoNoOrillas.TotalCandidatos}");

        Verificar(
            resultadoNoOrillas.Resultados.Count == 0,
            $"Con NO ORILLAS no debe devolverse ningún inmueble. Real: {resultadoNoOrillas.Resultados.Count}");

        Console.WriteLine("✅ Restricción: NO ORILLAS");
        Console.WriteLine("✅ Candidatos: 0");
        Console.WriteLine("✅ NO ALERT");
        Console.WriteLine("✅ REGRESION FAIL-CLOSED SUPERADA");
    }

    private static void Verificar(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class FakeInventarioRepository : IInventarioRepository
    {
        private readonly List<InventarioInmuebleViewModel> _inventario;

        public FakeInventarioRepository(List<InventarioInmuebleViewModel> inventario)
        {
            _inventario = inventario;
        }

        public Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo) =>
            Task.FromResult(_inventario);

        public Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor) =>
            Task.FromResult(_inventario);

        public Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo) =>
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