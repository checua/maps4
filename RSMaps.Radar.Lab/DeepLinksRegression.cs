using maps4.Controllers;
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RSMaps.Radar.Listener.Services;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

internal static class DeepLinksRegression
{
    public static void Run()
    {
        VerificarRutas();
        VerificarMaterializacionEndpoints();
        VerificarVistaInventario().GetAwaiter().GetResult();
        VerificarFocoMapaLegacy().GetAwaiter().GetResult();
        VerificarFormatoRadar();
        Console.WriteLine("DEEP_LINKS_REGRESSION_OK");
    }

    private static void VerificarRutas()
    {
        MethodInfo inventario = typeof(InventarioController).GetMethod(
            nameof(InventarioController.InventarioDeepLink),
            [typeof(int)]) ?? throw new InvalidOperationException("No se encontró Inventario.InventarioDeepLink.");
        MethodInfo index = typeof(InventarioController).GetMethod(
            nameof(InventarioController.Index),
            [typeof(int?)]) ?? throw new InvalidOperationException("No se encontró Inventario.Index.");
        MethodInfo mapa = typeof(InventarioController).GetMethod(
            nameof(InventarioController.Mapa),
            [typeof(int)]) ?? throw new InvalidOperationException("No se encontró Home.Mapa.");

        Exigir(TieneRuta(inventario, "/i/{inmuebleId:int:min(1)}", "InventarioDeepLink"),
            "Falta la ruta corta autorizada /i/{id}.");
        Exigir(TieneRuta(mapa, "/m/{inmuebleId:int:min(1)}", "MapaDeepLink"),
            "Falta la ruta corta autorizada /m/{id}.");
        Exigir(index.GetCustomAttributes<HttpGetAttribute>().Any(x => x.Template is null),
            "Inventario.Index debe conservar su ruta GET convencional.");
        Exigir(!MezclaRoutingAtributoYConvencional(index) &&
               !MezclaRoutingAtributoYConvencional(inventario),
            "Una acción no debe mezclar routing por atributo y routing convencional.");
        Exigir(typeof(InventarioController).IsDefined(typeof(AuthorizeAttribute), inherit: true),
            "Inventario debe permanecer protegido por autorización.");
        Exigir(typeof(InventarioController).IsDefined(typeof(AuthorizeAttribute), inherit: true),
            "El mapa privado debe heredar autorización del controlador.");
    }

    private static void VerificarMaterializacionEndpoints()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddControllersWithViews()
            .AddApplicationPart(typeof(InventarioController).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        ActionDescriptorCollection descriptors = provider
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors;

        ControllerActionDescriptor inventario = descriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Single(x => x.ControllerTypeInfo.AsType() == typeof(InventarioController) &&
                         x.MethodInfo.Name == nameof(InventarioController.InventarioDeepLink));

        Exigir(
            string.Equals(
                inventario.AttributeRouteInfo?.Template,
                "i/{inmuebleId:int:min(1)}",
                StringComparison.Ordinal) &&
            string.Equals(
                inventario.AttributeRouteInfo?.Name,
                "InventarioDeepLink",
                StringComparison.Ordinal),
            "La materialización MVC no conservó InventarioDeepLink.");

        Console.WriteLine("MVC_ENDPOINT_MATERIALIZATION_REGRESSION_OK");
    }

    private static async Task VerificarVistaInventario()
    {
        const int idInmueble = 109;
        var controller = new InventarioController(new InventarioRepositoryControlado(idInmueble))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(ClaimTypes.Name, "qa@ejemplo.test")],
                            "regression"))
                }
            }
        };

        ViewResult index = ExigirVista(await controller.Index(idInmueble), "Inventario.Index");
        ViewResult deepLink = ExigirVista(
            await controller.InventarioDeepLink(idInmueble),
            "Inventario.InventarioDeepLink");

        Exigir(string.Equals(index.ViewName, "Index", StringComparison.Ordinal),
            "Inventario.Index debe resolver explícitamente la vista Index.");
        Exigir(string.Equals(deepLink.ViewName, "Index", StringComparison.Ordinal),
            "InventarioDeepLink debe resolver explícitamente la vista Index.");
        Exigir(ContieneSoloInmueble(index, idInmueble) && ContieneSoloInmueble(deepLink, idInmueble),
            "Ambas acciones deben reutilizar la lógica que filtra el inventario autorizado.");

        Console.WriteLine("INVENTARIO_DEEP_LINK_VIEW_REGRESSION_OK");
    }

    private static ViewResult ExigirVista(IActionResult resultado, string accion) =>
        resultado as ViewResult ??
        throw new InvalidOperationException($"{accion} no produjo un ViewResult.");

    private static bool ContieneSoloInmueble(ViewResult vista, int idInmueble) =>
        vista.Model is InventarioIndexViewModel modelo &&
        modelo.Inmuebles.Count == 1 &&
        modelo.Inmuebles[0].IdInmueble == idInmueble;

    private static async Task VerificarFocoMapaLegacy()
    {
        const int idInmueble = 109;
        var controller = new InventarioController(new InventarioRepositoryControlado(idInmueble))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(ClaimTypes.Name, "qa@ejemplo.test")],
                            "regression"))
                }
            }
        };

        OkObjectResult endpoint = await controller.GetInmuebleAutorizadoById(idInmueble) as OkObjectResult ??
            throw new InvalidOperationException("El endpoint autorizado no devolvió HTTP 200.");
        JsonElement payload = JsonSerializer.SerializeToElement(endpoint.Value);
        Exigir(payload.ValueKind == JsonValueKind.Array && payload.GetArrayLength() == 1,
            "El endpoint autorizado no devolvió una colección de un inmueble.");

        JsonElement inmueble = payload[0];
        Exigir(inmueble.GetProperty("IdInmueble").GetInt32() == idInmueble,
            "El endpoint autorizado no conservó el inmueble objetivo.");
        Exigir(inmueble.GetProperty("Lat").GetDecimal() == 20.187357m &&
               inmueble.GetProperty("Lng").GetDecimal() == -87.468782m,
            "El endpoint autorizado no devolvió coordenadas utilizables.");
        Exigir(inmueble.GetProperty("IdTipo").GetInt32() == 2,
            "El endpoint autorizado no devolvió el tipo requerido para el marker.");

        string raiz = EncontrarRaizRepositorio();
        string indexJs = File.ReadAllText(Path.Combine(raiz, "wwwroot", "js", "index.js"));
        string mapFocusJs = File.ReadAllText(Path.Combine(raiz, "wwwroot", "js", "map-focus-fix.js"));

        Exigir(indexJs.Contains(
                "? '/Inventario/GetInmuebleAutorizadoById'",
                StringComparison.Ordinal),
            "La URL legacy de Inventario no reutiliza el endpoint autorizado de /m/{id}.");
        Exigir(indexJs.Contains(
                "loadInmueble(queryParams.inmuebleId, inventoryEndpoint);",
                StringComparison.Ordinal),
            "La URL legacy no entrega explícitamente el endpoint autorizado a loadInmueble.");
        Exigir(!indexJs.Contains("/Inmueble/GetInmueblePrivadoById", StringComparison.Ordinal),
            "El mapa legacy todavía depende del endpoint antiguo limitado al propietario.");
        Exigir(indexJs.Contains(
                "Array.isArray(inmueble) && inmueble.length > 0",
                StringComparison.Ordinal),
            "loadInmueble vuelve a permitir acceso a inmueble[0] sin validar la colección.");
        Exigir(mapFocusJs.Contains("map.setZoom(17);", StringComparison.Ordinal),
            "El foco explícito dejó de aplicar zoom 17.");
        Exigir(mapFocusJs.Contains(
                "currentMap.setCenter = function () { };",
                StringComparison.Ordinal),
            "El foco explícito dejó de protegerse contra recenter tardío por geolocalización.");

        Console.WriteLine("LEGACY_MAP_FOCUS_REGRESSION_OK");
    }

    private static string EncontrarRaizRepositorio()
    {
        DirectoryInfo? directorio = new(Directory.GetCurrentDirectory());
        while (directorio != null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "maps4.csproj")))
                return directorio.FullName;

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio RSMaps.");
    }

    private static void VerificarFormatoRadar()
    {
        string baseUrl = RadarPropertyDeepLinks.NormalizarBaseUrl(
            " https://ejemplo.test/rsmaps/ ",
            "https://fallback.invalid");
        var sb = new StringBuilder();
        RadarPropertyDeepLinks.Agregar(sb, 187, baseUrl);
        string salida = sb.ToString();

        Exigir(baseUrl == "https://ejemplo.test/rsmaps", "La URL base no se normalizó.");
        Exigir(salida.Contains("📋 Inventario: https://ejemplo.test/rsmaps/i/187", StringComparison.Ordinal),
            "Falta el enlace estructurado a Inventario.");
        Exigir(salida.Contains("📍 Mapa: https://ejemplo.test/rsmaps/m/187", StringComparison.Ordinal),
            "Falta el enlace estructurado al mapa.");
        Exigir(!salida.Contains("//i/", StringComparison.Ordinal) &&
               !salida.Contains("//m/", StringComparison.Ordinal),
            "La URL normalizada no debe contener doble slash antes de la ruta.");

        foreach (int id in new[] { 155, 165, 135 })
        {
            var resultado = new StringBuilder();
            RadarPropertyDeepLinks.Agregar(resultado, id, baseUrl);
            string texto = resultado.ToString();
            Exigir(texto.Contains($"/i/{id}", StringComparison.Ordinal),
                $"Falta el enlace de Inventario para #{id}.");
            Exigir(texto.Contains($"/m/{id}", StringComparison.Ordinal),
                $"Falta el enlace de Mapa para #{id}.");
        }

        Exigir(
            RadarPropertyDeepLinks.NormalizarBaseUrl(null, "https://fallback.test/") ==
            "https://fallback.test",
            "La URL base vacía debe usar el fallback seguro sin slash final.");

        var invalido = new StringBuilder();
        RadarPropertyDeepLinks.Agregar(invalido, 0, baseUrl);
        Exigir(invalido.Length == 0, "No deben generarse enlaces para IDs inválidos.");
    }

    private static bool TieneRuta(MethodInfo metodo, string plantilla, string nombre) =>
        metodo.GetCustomAttributes<HttpGetAttribute>()
            .Any(x => string.Equals(x.Template, plantilla, StringComparison.Ordinal) &&
                      string.Equals(x.Name, nombre, StringComparison.Ordinal));

    private static bool MezclaRoutingAtributoYConvencional(MethodInfo metodo)
    {
        HttpGetAttribute[] atributos = metodo.GetCustomAttributes<HttpGetAttribute>().ToArray();
        return atributos.Any(x => x.Template is null) &&
               atributos.Any(x => x.Template is not null);
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private sealed class InventarioRepositoryControlado(int idInmueble) : IInventarioRepository
    {
        private readonly List<InventarioInmuebleViewModel> _inmuebles =
        [
            new()
            {
                IdInmueble = idInmueble,
                IdCuenta = 1,
                IdAsesor = 7,
                TipoNombre = "Casa en Venta",
                Lat = 20.187357m,
                Lng = -87.468782m,
                IdTipo = 2
            },
            new()
            {
                IdInmueble = idInmueble + 1,
                IdCuenta = 1,
                IdAsesor = 7,
                TipoNombre = "Casa en Venta"
            }
        ];

        public Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor) =>
            Task.FromResult(_inmuebles);

        public Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo) =>
            Task.FromResult(_inmuebles);

        public Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo) =>
            Task.FromResult<InventarioAutorizacionContexto?>(new()
            {
                IdCuenta = 1,
                CuentaNombre = "Cuenta QA",
                IdAsesor = 7,
                RolCodigo = "PROPIETARIO"
            });

        public Task CambiarEstadoOVisibilidadAsync(
            int idInmueble,
            string correo,
            string? estadoNuevo,
            string? visibilidadNueva,
            string? motivo) => throw new NotSupportedException();

        public Task CerrarOperacionAsync(
            int idInmueble,
            string correo,
            string tipoOperacion,
            decimal precioCierre,
            DateTime fechaCierreUtc,
            string? notasCierre) => throw new NotSupportedException();
    }
}
