using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;

internal static class TipoFraccionamientoIntegrationRegression
{
    public static async Task RunAsync()
    {
        VerificarUi();
        VerificarMapping();
        VerificarIntegracionRepositorioYSuperficiePublica();
        await VerificarMatchingAsync();

        Console.WriteLine("UI_SELECT_TRIESTADO_OK");
        Console.WriteLine("MAPPING_TIPO_FRACCIONAMIENTO_OK");
        Console.WriteLine("AMENIDADES_NO_INFIEREN_TIPO_OK");
        Console.WriteLine("SIN_CRITERIO_COMPORTAMIENTO_PRESERVADO_OK");
        Console.WriteLine("CASO_HISTORICO_TIPO_FRACCIONAMIENTO_OK");
        Console.WriteLine("TIPO_FRACCIONAMIENTO_INTEGRATION_REGRESSION_OK");
    }

    private static void VerificarUi()
    {
        string raiz = EncontrarRaizRepositorio();
        string vista = File.ReadAllText(Path.Combine(raiz, "Views", "Borrador", "Editar.cshtml"));

        Exigir(vista.Contains("<select asp-for=\"TipoFraccionamientoCodigo\">", StringComparison.Ordinal),
            "La UI debe enviar siempre la clave del campo mediante select.");
        Exigir(vista.Contains("<option value=\"\">Sin especificar</option>", StringComparison.Ordinal),
            "Falta la opcion explicita Sin especificar.");
        Exigir(vista.Contains("<option value=\"PRIVADO\">Privado / cerrado</option>", StringComparison.Ordinal),
            "Falta la opcion PRIVADO.");
        Exigir(vista.Contains("<option value=\"ABIERTO\">No privado / abierto</option>", StringComparison.Ordinal),
            "Falta la opcion ABIERTO.");
        Exigir(!vista.Contains("type=\"checkbox\" asp-for=\"TipoFraccionamientoCodigo\"", StringComparison.Ordinal),
            "El dato triestado no debe representarse como checkbox.");
    }

    private static void VerificarMapping()
    {
        Exigir(TipoFraccionamientoCodigos.MapearAContratoRadar("PRIVADO") == "Privado",
            "PRIVADO no se mapeo al contrato RADAR.");
        Exigir(TipoFraccionamientoCodigos.MapearAContratoRadar("ABIERTO") == "NoPrivado",
            "ABIERTO no se mapeo al contrato RADAR.");
        Exigir(TipoFraccionamientoCodigos.MapearAContratoRadar(null) is null,
            "NULL debe permanecer desconocido.");

        foreach (string invalido in new[] { "Privado", "privado", "CERRADO", "ACCESO_CONTROLADO", "VIGILANCIA_24H", "CASETA", "residencial", "cerrada" })
            Exigir(TipoFraccionamientoCodigos.MapearAContratoRadar(invalido) is null,
                $"El valor no autoritativo '{invalido}' no debe inferir tipo.");
    }

    private static void VerificarIntegracionRepositorioYSuperficiePublica()
    {
        string raiz = EncontrarRaizRepositorio();
        string repositorio = File.ReadAllText(Path.Combine(raiz, "Repositorios", "Implementacion", "InventarioRepository.cs"));
        Exigir(repositorio.Contains("i.TipoFraccionamientoCodigo", StringComparison.Ordinal),
            "El inventario no lee la fuente autoritativa.");
        Exigir(repositorio.Contains("TipoFraccionamientoCodigos.MapearAContratoRadar(tipoFraccionamientoCodigo)", StringComparison.Ordinal),
            "El inventario no aplica el mapping explicito.");
        Exigir(!repositorio.Contains("MapearAContratoRadar(inmueble.AmenidadesCsv", StringComparison.Ordinal),
            "Las amenidades no deben alimentar el tipo de fraccionamiento.");

        string inventarioController = File.ReadAllText(Path.Combine(raiz, "Controllers", "InventarioController.cs"));
        string homeController = File.ReadAllText(Path.Combine(raiz, "Controllers", "HomeController.cs"));
        Exigir(!inventarioController.Contains("inmueble.TipoFraccionamiento", StringComparison.Ordinal),
            "El endpoint de inventario expuso accidentalmente el dato.");
        Exigir(!homeController.Contains("TipoFraccionamiento", StringComparison.Ordinal),
            "La superficie publica expuso accidentalmente el dato.");
    }

    private static async Task VerificarMatchingAsync()
    {
        List<InventarioInmuebleViewModel> inventario =
        [
            Inmueble(187, 1_750_000, "Privado"),
            Inmueble(116, 1_400_000, "NoPrivado"),
            Inmueble(142, 1_395_000, null)
        ];
        RadarMatchingService matching = new(new FakeInventarioRepository(inventario));

        RadarMatchingRequest privada = new()
        {
            Operacion = "Venta",
            TiposPropiedad = ["Casa"],
            PrecioMaximo = 2_000_000m,
            PrecioObjetivo = 2_000_000m,
            TipoFraccionamiento = "Privado",
            MaxResultados = 10
        };

        RadarMatchingResponse resultadoPrivado = await matching.CompararAsync("integration@test.local", privada);
        RadarMatchingResultado privado = Obtener(resultadoPrivado, 187);
        Exigir(privado.EsRecomendacionAutomatica, "PRIVADO confirmado debe poder recomendarse.");
        Exigir(resultadoPrivado.Resultados.All(x => x.IdInmueble != 116), "ABIERTO debe descartarse.");
        RadarMatchingResultado sinDato = Obtener(resultadoPrivado, 142);
        Exigir(!sinDato.EsRecomendacionAutomatica, "NULL debe operar fail-closed.");
        Exigir(sinDato.MotivosNoRecomendacion.Any(x => x.Contains("verificar este requisito", StringComparison.OrdinalIgnoreCase)),
            "NULL debe explicar que el requisito no es verificable.");

        RadarMatchingService matchingNeutral = new(new FakeInventarioRepository(
        [
            Inmueble(701, 1_750_000, "Privado"),
            Inmueble(702, 1_750_000, "NoPrivado"),
            Inmueble(703, 1_750_000, null)
        ]));
        RadarMatchingResponse sinCriterio = await matchingNeutral.CompararAsync(
            "integration@test.local",
            new RadarMatchingRequest
            {
                Operacion = "Venta",
                TiposPropiedad = ["Casa"],
                PrecioMaximo = 2_000_000m,
                PrecioObjetivo = 2_000_000m,
                MaxResultados = 10
            });

        Exigir(sinCriterio.Resultados.Select(x => x.IdInmueble).Order().SequenceEqual(new[] { 701, 702, 703 }),
            "Sin criterio, el tipo de fraccionamiento no debe filtrar candidatos.");
        Exigir(sinCriterio.Resultados.Select(x => x.Puntuacion).Distinct().Count() == 1,
            "Sin criterio, el tipo de fraccionamiento no debe premiar ni penalizar.");
    }

    private static InventarioInmuebleViewModel Inmueble(int id, double precio, string? tipo) => new()
    {
        IdInmueble = id,
        TipoNombre = "Casa en Venta",
        Precio = precio,
        EstadoCodigo = "PUBLICADO",
        TipoFraccionamiento = tipo
    };

    private static RadarMatchingResultado Obtener(RadarMatchingResponse respuesta, int id) =>
        respuesta.Resultados.SingleOrDefault(x => x.IdInmueble == id)
        ?? throw new InvalidOperationException($"No se encontro el candidato #{id}.");

    private static string EncontrarRaizRepositorio()
    {
        DirectoryInfo? actual = new(AppContext.BaseDirectory);
        while (actual is not null)
        {
            if (File.Exists(Path.Combine(actual.FullName, "maps4.csproj")))
                return actual.FullName;
            actual = actual.Parent;
        }

        throw new InvalidOperationException("No se encontro la raiz del repositorio.");
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
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
