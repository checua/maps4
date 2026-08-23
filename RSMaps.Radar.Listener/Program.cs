using Microsoft.Playwright;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("       RSMaps Radar v0.3");
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

// -----------------------------------------------------
// Registrar mensajes existentes
// -----------------------------------------------------

var knownIds = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase);

await RegistrarMensajesExistentes(
    page,
    knownIds);

Console.WriteLine(
    $"Mensajes existentes registrados: {knownIds.Count}");

Console.WriteLine();
Console.WriteLine("==================================");
Console.WriteLine("          RADAR ACTIVO");
Console.WriteLine("==================================");
Console.WriteLine();
Console.WriteLine("Esperando mensajes nuevos...");
Console.WriteLine("CTRL+C para terminar.");
Console.WriteLine();

// -----------------------------------------------------
// Listener
// -----------------------------------------------------

while (true)
{
    try
    {
        var currentChat =
            (await titleLocator.InnerTextAsync()).Trim();

        // Si cambiamos manualmente de conversación,
        // Radar toma los mensajes actuales como punto inicial.
        if (!string.Equals(
                currentChat,
                chatName,
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"[CAMBIO DE CHAT] {chatName} -> {currentChat}");

            chatName = currentChat;

            knownIds.Clear();

            await RegistrarMensajesExistentes(
                page,
                knownIds);

            Console.WriteLine(
                $"Radar inicializado en: {chatName}");

            Console.WriteLine(
                $"Mensajes existentes: {knownIds.Count}");

            Console.WriteLine();
        }

        var messages = page.Locator(
            "[data-testid^='conv-msg-'][data-id]");

        var count = await messages.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var message = messages.Nth(i);

            var id =
                await message.GetAttributeAsync("data-id");

            if (string.IsNullOrWhiteSpace(id))
                continue;

            // Si Add devuelve false, ya conocíamos el mensaje.
            if (!knownIds.Add(id))
                continue;

            var text =
                (await message.InnerTextAsync()).Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var classification =
                ClasificarMensaje(text);

            MostrarResultado(
                chatName,
                id,
                text,
                classification);
        }
    }
    catch (PlaywrightException ex)
    {
        Console.WriteLine(
            $"[PLAYWRIGHT] {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"[RADAR] {ex.Message}");
    }

    await Task.Delay(2000);
}


// =====================================================
// FUNCIONES
// =====================================================

static async Task RegistrarMensajesExistentes(
    IPage page,
    HashSet<string> knownIds)
{
    var messages = page.Locator(
        "[data-testid^='conv-msg-'][data-id]");

    var count = await messages.CountAsync();

    for (var i = 0; i < count; i++)
    {
        var id =
            await messages.Nth(i)
                .GetAttributeAsync("data-id");

        if (!string.IsNullOrWhiteSpace(id))
            knownIds.Add(id);
    }
}


static TipoMensaje ClasificarMensaje(string texto)
{
    var text = Normalizar(texto);

    // ---------------------------------------------
    // Expresiones que indican DEMANDA
    // ---------------------------------------------

    string[] demanda =
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
        "algún terreno",
        "algun terreno",
        "alguna bodega",
        "algún local",
        "algun local"
    };

    // ---------------------------------------------
    // Expresiones que indican OFERTA
    // ---------------------------------------------

    string[] oferta =
    {
        "vendo",
        "se vende",
        "en venta",
        "rento",
        "se renta",
        "en renta",
        "ofrezco",
        "ofrecemos",
        "tenemos disponible",
        "tengo disponible",
        "propiedad disponible",
        "casa disponible",
        "departamento disponible",
        "terreno disponible"
    };

    var puntosDemanda =
        demanda.Count(text.Contains);

    var puntosOferta =
        oferta.Count(text.Contains);

    if (puntosDemanda > puntosOferta &&
        puntosDemanda > 0)
    {
        return TipoMensaje.Demanda;
    }

    if (puntosOferta > puntosDemanda &&
        puntosOferta > 0)
    {
        return TipoMensaje.Oferta;
    }

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


static void MostrarResultado(
    string chat,
    string id,
    string texto,
    TipoMensaje tipo)
{
    Console.WriteLine();
    Console.WriteLine(
        "==============================================");

    switch (tipo)
    {
        case TipoMensaje.Demanda:

            Console.WriteLine(
                "🔥 SOLICITUD INMOBILIARIA DETECTADA");

            break;

        case TipoMensaje.Oferta:

            Console.WriteLine(
                "🏠 OFERTA DE PROPIEDAD");

            break;

        default:

            Console.WriteLine(
                "⚪ OTRO / RUIDO");

            break;
    }

    Console.WriteLine(
        "==============================================");

    Console.WriteLine($"Chat: {chat}");
    Console.WriteLine($"Tipo: {tipo}");
    Console.WriteLine($"ID: {id}");
    Console.WriteLine();

    Console.WriteLine(texto);

    Console.WriteLine();
    Console.WriteLine(
        $"Detectado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

    Console.WriteLine(
        "==============================================");

    Console.WriteLine();
}


enum TipoMensaje
{
    Demanda,
    Oferta,
    Otro
}