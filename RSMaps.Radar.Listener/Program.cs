using Microsoft.Playwright;
using System.Text;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("       RSMaps Radar v0.5");
Console.WriteLine("==================================");
Console.WriteLine();

var userDataDir = Path.Combine(AppContext.BaseDirectory, "WhatsAppProfile");

using var playwright = await Playwright.CreateAsync();

var context = await playwright.Chromium.LaunchPersistentContextAsync(
    userDataDir,
    new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = false,
        ViewportSize = null
    });

var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

await page.GotoAsync(
    "https://web.whatsapp.com",
    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

Console.WriteLine("WhatsApp Web abierto.");
Console.WriteLine();
Console.WriteLine("Abre manualmente un chat o grupo de prueba.");
Console.WriteLine("Cuando esté abierto, vuelve aquí y presiona ENTER.");
Console.ReadLine();

var titleLocator = page.Locator("[data-testid='conversation-info-header-chat-title']");
var chatName = (await titleLocator.InnerTextAsync()).Trim();

Console.WriteLine();
Console.WriteLine($"Chat seleccionado: {chatName}");
Console.WriteLine("Inicializando Radar...");

var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await RegistrarMensajesExistentes(page, knownIds);

Console.WriteLine($"Mensajes existentes registrados: {knownIds.Count}");
Console.WriteLine();
Console.WriteLine("==================================");
Console.WriteLine("          RADAR ACTIVO");
Console.WriteLine("==================================");
Console.WriteLine();
Console.WriteLine("Esperando mensajes nuevos...");
Console.WriteLine("CTRL+C para terminar.");
Console.WriteLine();

while (true)
{
    try
    {
        var currentChat = (await titleLocator.InnerTextAsync()).Trim();

        if (!string.Equals(currentChat, chatName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine($"[CAMBIO DE CHAT] {chatName} -> {currentChat}");

            chatName = currentChat;
            knownIds.Clear();
            await RegistrarMensajesExistentes(page, knownIds);

            Console.WriteLine($"Radar inicializado en: {chatName}");
            Console.WriteLine($"Mensajes existentes: {knownIds.Count}");
            Console.WriteLine();
        }

        var messages = page.Locator("[data-testid^='conv-msg-'][data-id]");
        var count = await messages.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var message = messages.Nth(i);
            var id = await message.GetAttributeAsync("data-id");

            if (string.IsNullOrWhiteSpace(id) || !knownIds.Add(id))
                continue;

            var text = (await message.InnerTextAsync()).Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var classification = ClasificarMensaje(text);

            if (classification == TipoMensaje.Demanda)
            {
                var solicitud = ExtractorInmobiliario.Extraer(text, chatName, id);
                MostrarSolicitud(solicitud);
            }
            else
            {
                MostrarResultado(chatName, id, text, classification);
            }
        }
    }
    catch (PlaywrightException ex)
    {
        Console.WriteLine($"[PLAYWRIGHT] {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RADAR] {ex.Message}");
    }

    await Task.Delay(2000);
}

static async Task RegistrarMensajesExistentes(IPage page, HashSet<string> knownIds)
{
    var messages = page.Locator("[data-testid^='conv-msg-'][data-id]");
    var count = await messages.CountAsync();

    for (var i = 0; i < count; i++)
    {
        var id = await messages.Nth(i).GetAttributeAsync("data-id");
        if (!string.IsNullOrWhiteSpace(id))
            knownIds.Add(id);
    }
}

static TipoMensaje ClasificarMensaje(string texto)
{
    var text = Normalizar(texto);

    // Señales fuertes: expresan intención de conseguir una propiedad.
    string[] demandaFuerte =
    {
        "busco", "buscando", "ando buscando", "estoy buscando", "estamos buscando",
        "sigo en busqueda", "aun sigo en busqueda", "solicito para cliente",
        "solicito renta", "solicito casa", "solicito terreno", "solicito departamento",
        "necesito", "necesitamos", "requiero", "requerimos", "cliente busca",
        "mi cliente busca", "para un cliente", "para cliente", "alguien tendra",
        "alguien traera", "algun compañero tiene", "alguien tiene",
        "me pudiera compartir", "me pueden compartir opciones", "agradezco sus opciones",
        "recibo propuesta", "recibo propuestas"
    };

    // Señales de oferta/publicación de inventario.
    string[] ofertaFuerte =
    {
        "ofrezco", "vendo", "rento", "se vende", "se renta", "pongo a su disposicion",
        "pongo a la disposicion", "tenemos a la venta", "tenemos en venta",
        "tenemos a la renta", "tenemos en renta", "propiedad en preventa",
        "casa en preventa", "casa en venta", "departamento en renta",
        "terreno en venta", "local en renta", "bodega en renta",
        "tenemos disponible", "tengo disponible"
    };

    // Evita falsos positivos como "solicitar la licencia inmobiliaria".
    string[] exclusionesDemanda =
    {
        "solicitar la licencia", "solicitar licencia", "solicitar informacion",
        "solicitar constancia", "solicitar informes"
    };

    if (exclusionesDemanda.Any(text.Contains) && !demandaFuerte.Any(text.Contains))
        return TipoMensaje.Otro;

    if (demandaFuerte.Any(text.Contains))
        return TipoMensaje.Demanda;

    if (ofertaFuerte.Any(text.Contains))
        return TipoMensaje.Oferta;

    // Señales débiles solo se usan si no hubo una señal fuerte anterior.
    var demandaDebil = new[] { "tendran", "tendras", "alguna propiedad", "alguna casa", "algun terreno", "alguna bodega", "algun local" };
    if (demandaDebil.Any(text.Contains))
        return TipoMensaje.Demanda;

    return TipoMensaje.Otro;
}

static string Normalizar(string texto)
{
    return texto
        .ToLowerInvariant()
        .Replace("á", "a")
        .Replace("é", "e")
        .Replace("í", "i")
        .Replace("ó", "o")
        .Replace("ú", "u")
        .Replace("ü", "u")
        .Replace("ñ", "n");
}

static void MostrarResultado(string chat, string id, string texto, TipoMensaje tipo)
{
    Console.WriteLine();
    Console.WriteLine("==============================================");
    Console.WriteLine(tipo switch
    {
        TipoMensaje.Oferta => "🏠 OFERTA DE PROPIEDAD",
        TipoMensaje.Otro => "⚪ OTRO / RUIDO",
        _ => "🔥 SOLICITUD INMOBILIARIA DETECTADA"
    });
    Console.WriteLine("==============================================");
    Console.WriteLine($"Chat: {chat}");
    Console.WriteLine($"Tipo: {tipo}");
    Console.WriteLine($"ID: {id}");
    Console.WriteLine();
    Console.WriteLine(texto);
    Console.WriteLine();
    Console.WriteLine($"Detectado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
    Console.WriteLine("==============================================");
    Console.WriteLine();
}

static void MostrarSolicitud(SolicitudInmobiliaria solicitud)
{
    Console.WriteLine();
    Console.WriteLine("==============================================");
    Console.WriteLine("🔥 SOLICITUD INMOBILIARIA");
    Console.WriteLine("==============================================");

    Console.WriteLine($"Chat:          {solicitud.ChatOrigen}");
    Console.WriteLine($"Operación:     {solicitud.Operacion ?? "No determinada"}");
    Console.WriteLine($"Tipos:         {MostrarLista(solicitud.TiposPropiedad)}");
    Console.WriteLine($"Zonas:         {MostrarLista(solicitud.Zonas)}");
    Console.WriteLine($"Precio mín.:   {MostrarDinero(solicitud.PrecioMinimo)}");
    Console.WriteLine($"Precio máx.:   {MostrarDinero(solicitud.PrecioMaximo)}");
    Console.WriteLine($"Recámaras:     {MostrarRango(solicitud.RecamarasMin, solicitud.RecamarasMax)}");
    Console.WriteLine($"Baños:         {MostrarRango(solicitud.BanosMin, solicitud.BanosMax)}");
    Console.WriteLine($"Terreno mín.:  {MostrarMetros(solicitud.TerrenoMinM2)}");
    Console.WriteLine($"Construcc. mín:{MostrarMetros(solicitud.ConstruccionMinM2, true)}");
    Console.WriteLine($"Mascotas:      {MostrarBooleano(solicitud.AceptaMascotas)}");
    Console.WriteLine($"Amueblado:     {MostrarBooleano(solicitud.Amueblado)}");
    Console.WriteLine($"Una planta:    {MostrarBooleano(solicitud.UnaPlanta)}");
    Console.WriteLine($"Vigilancia:    {MostrarBooleano(solicitud.CasetaVigilancia)}");
    Console.WriteLine($"Cochera mín.:  {(solicitud.CocheraMinAutos?.ToString() ?? "-")}");
    Console.WriteLine($"Pago/crédito:  {MostrarLista(solicitud.ModalidadesPago)}");

    Console.WriteLine();
    Console.WriteLine("MENSAJE ORIGINAL:");
    Console.WriteLine(solicitud.MensajeOriginal);
    Console.WriteLine();
    Console.WriteLine($"ID: {solicitud.MessageId}");
    Console.WriteLine("==============================================");
    Console.WriteLine();
}

static string MostrarLista(IEnumerable<string> valores)
{
    var lista = valores.ToList();
    return lista.Count == 0 ? "-" : string.Join(" | ", lista);
}

static string MostrarDinero(decimal? valor) => valor.HasValue ? valor.Value.ToString("C0") : "-";

static string MostrarRango(int? min, int? max)
{
    if (!min.HasValue && !max.HasValue) return "-";
    if (min == max) return min?.ToString() ?? "-";
    return $"{min?.ToString() ?? "-"} a {max?.ToString() ?? "-"}";
}

static string MostrarMetros(decimal? valor, bool espacioInicial = false)
{
    var resultado = valor.HasValue ? $"{valor:0.##} m²" : "-";
    return espacioInicial ? $" {resultado}" : resultado;
}

static string MostrarBooleano(bool? valor)
{
    return valor switch
    {
        true => "Sí",
        false => "No",
        null => "-"
    };
}

enum TipoMensaje
{
    Demanda,
    Oferta,
    Otro
}
