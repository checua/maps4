using maps4.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RSMaps.Radar.Listener.Services;
using System.Reflection;
using System.Text;

internal static class DeepLinksRegression
{
    public static void Run()
    {
        VerificarRutas();
        VerificarMaterializacionEndpoints();
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
}
