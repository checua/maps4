using Microsoft.Playwright;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("      RSMaps Radar v0.6.1");
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
Console.WriteLine("Esperando que cargue la lista de chats...");

await page.Locator("[data-testid='chat-list']").WaitForAsync(
    new LocatorWaitForOptions { Timeout = 60_000 });

Console.WriteLine();
Console.WriteLine("Chats configurados para Radar:");
foreach (var chat in RadarSettings.ChatsMonitoreados)
    Console.WriteLine($"  • {chat}");

var idsConocidosPorChat = new Dictionary<string, HashSet<string>>(
    StringComparer.OrdinalIgnoreCase);

var ultimoPreviewPorChat = new Dictionary<string, string>(
    StringComparer.OrdinalIgnoreCase);

Console.WriteLine();
Console.WriteLine("Inicializando chats...");

foreach (var chat in RadarSettings.ChatsMonitoreados)
{
    var abierto = await AbrirChat(page, chat);

    if (!abierto)
    {
        Console.WriteLine($"⚠ No pude localizar '{chat}' ni en la lista ni mediante búsqueda.");
        continue;
    }

    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await RegistrarMensajesExistentes(page, ids);
    idsConocidosPorChat[chat] = ids;

    var fila = await BuscarFilaChatVisible(page, chat);
    ultimoPreviewPorChat[chat] = fila is null
        ? string.Empty
        : await ObtenerPreview(fila);

    Console.WriteLine($"✓ {chat}: {ids.Count} mensajes actuales registrados.");
}

Console.WriteLine();
Console.WriteLine("==================================");
Console.WriteLine("          RADAR ACTIVO");
Console.WriteLine("==================================");
Console.WriteLine("Monitoreando automáticamente los chats configurados.");
Console.WriteLine("Si un chat no está visible, Radar lo buscará por nombre.");
Console.WriteLine("CTRL+C para terminar.");
Console.WriteLine();

while (true)
{
    try
    {
        foreach (var chat in RadarSettings.ChatsMonitoreados)
        {
            var fila = await BuscarFilaChatVisible(page, chat);

            // Si no está visible, lo buscamos por nombre y procesamos por ID.
            if (fila is null)
            {
                var abierto = await AbrirChat(page, chat);
                if (!abierto)
                    continue;

                if (!idsConocidosPorChat.TryGetValue(chat, out var idsNoVisible))
                {
                    idsNoVisible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    idsConocidosPorChat[chat] = idsNoVisible;
                }

                await ProcesarMensajesNuevos(page, chat, idsNoVisible);

                var filaDespues = await BuscarFilaChatVisible(page, chat);
                if (filaDespues is not null)
                    ultimoPreviewPorChat[chat] = await ObtenerPreview(filaDespues);

                continue;
            }

            var previewActual = await ObtenerPreview(fila);

            if (!ultimoPreviewPorChat.TryGetValue(chat, out var previewAnterior))
            {
                ultimoPreviewPorChat[chat] = previewActual;
                continue;
            }

            if (string.Equals(previewActual, previewAnterior, StringComparison.Ordinal))
                continue;

            ultimoPreviewPorChat[chat] = previewActual;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Actividad detectada en: {chat}");

            await fila.ClickAsync();
            await EsperarChatAbierto(page, chat);

            if (!idsConocidosPorChat.TryGetValue(chat, out var idsConocidos))
            {
                idsConocidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                idsConocidosPorChat[chat] = idsConocidos;
            }

            await ProcesarMensajesNuevos(page, chat, idsConocidos);
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

    await Task.Delay(RadarSettings.IntervaloRevisionMs);
}

static async Task<bool> AbrirChat(IPage page, string nombreChat)
{
    // 1) Camino rápido: chat visible en la lista.
    var fila = await BuscarFilaChatVisible(page, nombreChat);
    if (fila is not null)
    {
        await fila.ClickAsync();
        await EsperarChatAbierto(page, nombreChat);
        return true;
    }

    // 2) Fallback: buscador de WhatsApp.
    var searchContainer = page.Locator("[data-testid='chat-list-search-container']");
    if (await searchContainer.CountAsync() == 0)
        return false;

    var input = searchContainer.Locator("[contenteditable='true']").First;
    if (await input.CountAsync() == 0)
        input = searchContainer.Locator("[role='textbox']").First;

    if (await input.CountAsync() == 0)
        return false;

    await input.ClickAsync();
    await input.FillAsync(nombreChat);
    await Task.Delay(RadarSettings.EsperaBusquedaMs);

    fila = await BuscarFilaChatVisible(page, nombreChat);

    if (fila is null)
    {
        await input.FillAsync(string.Empty);
        await page.Keyboard.PressAsync("Escape");
        return false;
    }

    await fila.ClickAsync();
    await EsperarChatAbierto(page, nombreChat);

    // Limpiamos el buscador para devolver la lista a su estado normal.
    try
    {
        if (await input.IsVisibleAsync())
            await input.FillAsync(string.Empty);

        await page.Keyboard.PressAsync("Escape");
    }
    catch
    {
        // La interfaz puede reconstruir el buscador al abrir el chat.
    }

    await Task.Delay(250);
    return true;
}

static async Task<ILocator?> BuscarFilaChatVisible(IPage page, string nombreChat)
{
    var filas = page.Locator("[data-testid^='list-item-'][role='row']");
    var count = await filas.CountAsync();

    for (var i = 0; i < count; i++)
    {
        var fila = filas.Nth(i);
        var titulo = fila.Locator("[data-testid='cell-frame-title']");

        if (await titulo.CountAsync() == 0)
            continue;

        var textoTitulo = (await titulo.InnerTextAsync()).Trim();

        if (string.Equals(textoTitulo, nombreChat, StringComparison.OrdinalIgnoreCase))
            return fila;
    }

    return null;
}

static async Task<string> ObtenerPreview(ILocator fila)
{
    var preview = fila.Locator("[data-testid='cell-frame-secondary']");
    if (await preview.CountAsync() == 0)
        return string.Empty;

    return (await preview.InnerTextAsync()).Trim();
}

static async Task EsperarChatAbierto(IPage page, string chat)
{
    var title = page.Locator("[data-testid='conversation-info-header-chat-title']");
    await title.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

    for (var intento = 0; intento < 30; intento++)
    {
        var actual = (await title.InnerTextAsync()).Trim();
        if (string.Equals(actual, chat, StringComparison.OrdinalIgnoreCase))
            return;

        await Task.Delay(200);
    }

    throw new InvalidOperationException($"No se pudo confirmar la apertura del chat '{chat}'.");
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

static async Task ProcesarMensajesNuevos(
    IPage page,
    string chat,
    HashSet<string> knownIds)
{
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

        var (autor, telefono) = await ExtraerRemitente(message);
        var classification = ClasificarMensaje(text);

        if (classification != TipoMensaje.Demanda)
        {
            Console.WriteLine($"  ↳ Nuevo mensaje ignorado ({classification}) en {chat}.");
            continue;
        }

        var solicitud = ExtractorInmobiliario.Extraer(text, chat, id);
        solicitud.Autor = autor;
        solicitud.Telefono = telefono;

        MostrarSolicitud(solicitud);
    }
}

static async Task<(string? Autor, string? Telefono)> ExtraerRemitente(ILocator message)
{
    try
    {
        var fila = message.Locator("xpath=ancestor::*[@role='row'][1]");
        if (await fila.CountAsync() == 0)
            return (null, null);

        var perfil = fila.Locator("[data-testid='group-chat-profile-picture']");
        if (await perfil.CountAsync() == 0)
            return (null, ExtraerTelefono(await fila.InnerTextAsync()));

        var aria = await perfil.First.GetAttributeAsync("aria-label") ?? string.Empty;
        var limpio = Regex.Replace(
            aria,
            @"^Abrir los detalles del chat para\s+(?:Quizá\s+)?",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();

        var telefono = ExtraerTelefono(limpio);
        var autor = limpio;

        if (!string.IsNullOrWhiteSpace(telefono))
            autor = autor.Replace(telefono, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        autor = Regex.Replace(autor, @"\s+", " ").Trim();

        return (
            string.IsNullOrWhiteSpace(autor) ? null : autor,
            telefono);
    }
    catch
    {
        return (null, null);
    }
}

static string? ExtraerTelefono(string texto)
{
    var match = Regex.Match(
        texto,
        @"\+?\d{2,3}(?:\s+\d{1,3}){3,6}|\+?\d[\d\s-]{8,}\d");

    return match.Success ? Regex.Replace(match.Value, @"\s+", " ").Trim() : null;
}

static TipoMensaje ClasificarMensaje(string texto)
{
    var text = Normalizar(texto);

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

    string[] ofertaFuerte =
    {
        "ofrezco", "vendo", "rento", "se vende", "se renta", "pongo a su disposicion",
        "pongo a la disposicion", "tenemos a la venta", "tenemos en venta",
        "tenemos a la renta", "tenemos en renta", "propiedad en preventa",
        "casa en preventa", "casa en venta", "departamento en renta",
        "terreno en venta", "local en renta", "bodega en renta",
        "tenemos disponible", "tengo disponible"
    };

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

    var demandaDebil = new[]
    {
        "tendran", "tendras", "alguna propiedad", "alguna casa",
        "algun terreno", "alguna bodega", "algun local"
    };

    return demandaDebil.Any(text.Contains)
        ? TipoMensaje.Demanda
        : TipoMensaje.Otro;
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

static void MostrarSolicitud(SolicitudInmobiliaria solicitud)
{
    Console.WriteLine();
    Console.WriteLine("==============================================");
    Console.WriteLine("🔥 SOLICITUD INMOBILIARIA");
    Console.WriteLine("==============================================");
    Console.WriteLine($"Chat:          {solicitud.ChatOrigen}");
    Console.WriteLine($"Autor:         {solicitud.Autor ?? "-"}");
    Console.WriteLine($"Teléfono:      {solicitud.Telefono ?? "-"}");
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
