using Microsoft.Playwright;
using System.Text;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("       RSMaps Radar v0.4.1");
Console.WriteLine("==================================");
Console.WriteLine();

var userDataDir = Path.Combine(
    AppContext.BaseDirectory,
    "WhatsAppProfile");

using var playwright = await Playwright.CreateAsync();

var context = await playwright.Chromium.LaunchPersistentContextAsync(
    userDataDir,
    new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = false,
        ViewportSize = null
    });

var page = context.Pages.FirstOrDefault()
           ?? await context.NewPageAsync();

await page.GotoAsync(
    "https://web.whatsapp.com",
    new PageGotoOptions
    {
        WaitUntil = WaitUntilState.DOMContentLoaded
    });

Console.WriteLine("WhatsApp Web abierto.");
Console.WriteLine();
Console.WriteLine("Abre manualmente un chat o grupo de prueba.");
Console.WriteLine("Cuando esté abierto, vuelve aquí y presiona ENTER.");

Console.ReadLine();

var titleLocator = page.Locator(
    "[data-testid='conversation-info-header-chat-title']");

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

    // Las expresiones de intención tienen prioridad sobre palabras
    // descriptivas como "en renta" o "en venta".
    string[] intencionesDemanda =
    {
        "busco",
        "buscando",
        "estoy buscando",
        "estamos buscando",
        "solicito",
        "solicitamos",
        "necesito",
        "necesitamos",
        "requiero",
        "requerimos",
        "cliente busca",
        "mi cliente busca",
        "para cliente",
        "algun compañero tiene",
        "alguien tiene",
        "tendran",
        "tendras",
        "alguna propiedad",
        "alguna casa",
        "algun terreno",
        "alguna bodega",
        "algun local"
    };

    string[] intencionesOferta =
    {
        "vendo",
        "se vende",
        "rento",
        "se renta",
        "ofrezco",
        "ofrecemos",
        "tenemos disponible",
        "tengo disponible",
        "propiedad disponible",
        "casa disponible",
        "departamento disponible",
        "terreno disponible"
    };

    if (intencionesDemanda.Any(text.Contains))
        return TipoMensaje.Demanda;

    if (intencionesOferta.Any(text.Contains))
        return TipoMensaje.Oferta;

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
    Console.WriteLine($"Chat:         {solicitud.ChatOrigen}");
    Console.WriteLine($"Operación:    {solicitud.Operacion ?? "No determinada"}");
    Console.WriteLine($"Tipo:         {solicitud.TipoPropiedad ?? "No determinado"}");
    Console.WriteLine($"Zona:         {solicitud.Zona ?? "No determinada"}");
    Console.WriteLine($"Recámaras:    {solicitud.Recamaras?.ToString() ?? "-"}");
    Console.WriteLine($"Baños:        {solicitud.Banos?.ToString() ?? "-"}");
    Console.WriteLine($"Presupuesto:  {(solicitud.PrecioMaximo.HasValue ? solicitud.PrecioMaximo.Value.ToString("C0") : "-")}");
    Console.WriteLine($"Terreno:      {(solicitud.TerrenoM2.HasValue ? $"{solicitud.TerrenoM2} m²" : "-")}");
    Console.WriteLine($"Construcción: {(solicitud.ConstruccionM2.HasValue ? $"{solicitud.ConstruccionM2} m²" : "-")}");
    Console.WriteLine($"Mascotas:     {MostrarBooleano(solicitud.AceptaMascotas)}");
    Console.WriteLine($"Amueblado:    {MostrarBooleano(solicitud.Amueblado)}");
    Console.WriteLine();
    Console.WriteLine("MENSAJE ORIGINAL:");
    Console.WriteLine(solicitud.MensajeOriginal);
    Console.WriteLine();
    Console.WriteLine($"ID: {solicitud.MessageId}");
    Console.WriteLine("==============================================");
    Console.WriteLine();
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
