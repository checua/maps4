using Microsoft.Playwright;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("      RSMaps Radar v0.7.3-stable");
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
Console.WriteLine($"Chat de alertas: {AlertSettings.ChatDestino}");

var idsConocidosPorChat = new Dictionary<string, HashSet<string>>(
    StringComparer.OrdinalIgnoreCase);

Console.WriteLine();
Console.WriteLine("Inicializando chats...");

foreach (var chat in RadarSettings.ChatsMonitoreados)
{
    if (!await AbrirChat(page, chat))
    {
        Console.WriteLine($"⚠ No pude localizar '{chat}' ni con sus términos alternativos.");
        continue;
    }

    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await EstabilizarMensajesChat(page, ids);
    idsConocidosPorChat[chat] = ids;
    Console.WriteLine($"✓ {chat}: {ids.Count} mensajes actuales registrados.");
}

Console.WriteLine();
Console.WriteLine("Estabilizando historial visible...");

for (var ronda = 1; ronda <= 3; ronda++)
{
    foreach (var chat in idsConocidosPorChat.Keys.ToList())
    {
        if (!await AbrirChat(page, chat))
            continue;
        await EstabilizarMensajesChat(page, idsConocidosPorChat[chat]);
    }
}

Console.WriteLine();
Console.WriteLine("==================================");
Console.WriteLine("          RADAR ACTIVO");
Console.WriteLine("==================================");
Console.WriteLine("Las solicitudes nuevas se enviarán a Propiedades.");
Console.WriteLine("Después del envío se intentará marcar Propiedades como no leído.");
Console.WriteLine("CTRL+C para terminar.");
Console.WriteLine();

while (true)
{
    try
    {
        foreach (var chat in RadarSettings.ChatsMonitoreados)
        {
            if (!await AbrirChat(page, chat))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ No pude abrir {chat}.");
                continue;
            }

            if (!idsConocidosPorChat.TryGetValue(chat, out var idsConocidos))
            {
                idsConocidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                idsConocidosPorChat[chat] = idsConocidos;
                await EstabilizarMensajesChat(page, idsConocidos);
                continue;
            }

            var resultado = await ProcesarMensajesNuevos(page, chat, idsConocidos);

            if (resultado.Revisados > 0)
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {chat}: {resultado.Revisados} mensaje(s) nuevo(s), {resultado.Solicitudes.Count} solicitud(es).");

            foreach (var solicitud in resultado.Solicitudes)
            {
                MostrarSolicitud(solicitud);
                var envio = await EnviarAlerta(page, solicitud);

                if (!envio.Enviada)
                {
                    Console.WriteLine($"  ⚠ No pude enviar la alerta a '{AlertSettings.ChatDestino}'. Etapa: {envio.Detalle}");
                }
                else if (envio.MarcadoNoLeido)
                {
                    Console.WriteLine($"  📤 Alerta enviada a '{AlertSettings.ChatDestino}' y chat marcado como no leído.");
                }
                else
                {
                    Console.WriteLine($"  📤 Alerta enviada a '{AlertSettings.ChatDestino}'. ⚠ No pude marcar el chat como no leído.");
                }
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

    await Task.Delay(RadarSettings.IntervaloRevisionMs);
}

static async Task<bool> AbrirChat(IPage page, string nombreChat)
{
    if (await ClickPorTextoExacto(page, nombreChat) && await EsperarChatAbierto(page, nombreChat))
    {
        Console.WriteLine($"  ↳ {nombreChat}: abierto desde lista visible.");
        return true;
    }

    await LimpiarBusqueda(page);
    var input = await ObtenerInputBusqueda(page);
    if (input is null)
        return false;

    foreach (var termino in RadarSettings.ObtenerTerminosBusqueda(nombreChat))
    {
        try
        {
            Console.WriteLine($"  ↳ Buscando '{nombreChat}' con: {termino}");
            input = await ObtenerInputBusqueda(page) ?? input;
            await input.ClickAsync();
            await input.FillAsync(string.Empty);
            await input.FillAsync(termino);
            await Task.Delay(RadarSettings.EsperaBusquedaMs);

            if (!await ClickPorTextoExacto(page, nombreChat))
            {
                Console.WriteLine("     resultado visible no identificado.");
                continue;
            }

            if (await EsperarChatAbierto(page, nombreChat))
            {
                Console.WriteLine("     chat confirmado abierto.");
                await LimpiarBusqueda(page);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     fallo de navegación: {ex.Message}");
        }
    }

    await LimpiarBusqueda(page);
    return false;
}

static async Task<bool> ClickPorTextoExacto(IPage page, string nombreChat)
{
    foreach (var candidato in CandidatosTitulo(nombreChat))
    {
        var exacto = page.GetByText(candidato, new PageGetByTextOptions { Exact = true });
        var count = await exacto.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var item = exacto.Nth(i);
            try
            {
                if (!await item.IsVisibleAsync())
                    continue;
                await item.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                return true;
            }
            catch
            {
                try
                {
                    var contenedor = item.Locator("xpath=ancestor::*[@tabindex='0' or @tabindex='-1' or @role='button' or @role='listitem' or @role='row'][1]");
                    if (await contenedor.CountAsync() > 0)
                    {
                        await contenedor.First.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                        return true;
                    }
                }
                catch { }
            }
        }
    }

    var titulos = page.Locator("[data-testid='cell-frame-title']");
    for (var i = 0; i < await titulos.CountAsync(); i++)
    {
        var titulo = titulos.Nth(i);
        try
        {
            if (!await titulo.IsVisibleAsync())
                continue;
            if (!EsMismoChat((await titulo.InnerTextAsync()).Trim(), nombreChat))
                continue;
            await titulo.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
            return true;
        }
        catch { }
    }

    return false;
}

static IEnumerable<string> CandidatosTitulo(string nombreChat)
{
    yield return nombreChat;
    if (nombreChat.Contains("(Tú)", StringComparison.OrdinalIgnoreCase))
        yield return nombreChat.Replace("(Tú)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    if (nombreChat.Contains("(Tu)", StringComparison.OrdinalIgnoreCase))
        yield return nombreChat.Replace("(Tu)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
}

static async Task<ILocator?> ObtenerInputBusqueda(IPage page)
{
    var searchContainer = page.Locator("[data-testid='chat-list-search-container']");
    if (await searchContainer.CountAsync() == 0)
        return null;

    var input = searchContainer.Locator("[contenteditable='true']").First;
    if (await input.CountAsync() > 0)
        return input;

    input = searchContainer.Locator("[role='textbox']").First;
    return await input.CountAsync() > 0 ? input : null;
}

static async Task LimpiarBusqueda(IPage page)
{
    try
    {
        var input = await ObtenerInputBusqueda(page);
        if (input is not null && await input.IsVisibleAsync())
            await input.FillAsync(string.Empty);
        await page.Keyboard.PressAsync("Escape");
        await Task.Delay(250);
    }
    catch { }
}

static bool EsMismoChat(string tituloActual, string esperado)
{
    static string N(string value) => value
        .Replace("(Tú)", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("(Tu)", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Trim();
    return string.Equals(N(tituloActual), N(esperado), StringComparison.OrdinalIgnoreCase);
}

static async Task<bool> EsperarChatAbierto(IPage page, string chat)
{
    var title = page.Locator("[data-testid='conversation-info-header-chat-title']");
    try
    {
        await title.WaitForAsync(new LocatorWaitForOptions { Timeout = 6_000 });
    }
    catch
    {
        return false;
    }

    for (var intento = 0; intento < 30; intento++)
    {
        if (EsMismoChat((await title.InnerTextAsync()).Trim(), chat))
            return true;
        await Task.Delay(150);
    }
    return false;
}

static async Task EstabilizarMensajesChat(IPage page, HashSet<string> ids)
{
    var sinCambios = 0;
    var anterior = -1;
    for (var intento = 0; intento < 6 && sinCambios < 2; intento++)
    {
        await AbsorberMensajesActuales(page, ids);
        if (ids.Count == anterior) sinCambios++; else sinCambios = 0;
        anterior = ids.Count;
        await Task.Delay(400);
    }
}

static async Task AbsorberMensajesActuales(IPage page, HashSet<string> knownIds)
{
    var messages = page.Locator("[data-testid^='conv-msg-'][data-id]");
    for (var i = 0; i < await messages.CountAsync(); i++)
    {
        var id = await messages.Nth(i).GetAttributeAsync("data-id");
        if (!string.IsNullOrWhiteSpace(id))
            knownIds.Add(id);
    }
}

static async Task<(int Revisados, List<SolicitudInmobiliaria> Solicitudes)> ProcesarMensajesNuevos(
    IPage page, string chat, HashSet<string> knownIds)
{
    var messages = page.Locator("[data-testid^='conv-msg-'][data-id]");
    var revisados = 0;
    var solicitudes = new List<SolicitudInmobiliaria>();

    for (var i = 0; i < await messages.CountAsync(); i++)
    {
        var message = messages.Nth(i);
        var id = await message.GetAttributeAsync("data-id");
        if (string.IsNullOrWhiteSpace(id) || !knownIds.Add(id))
            continue;

        revisados++;
        var text = (await message.InnerTextAsync()).Trim();
        if (string.IsNullOrWhiteSpace(text))
            continue;

        var classification = ClasificarMensaje(text);
        if (classification != TipoMensaje.Demanda)
        {
            Console.WriteLine($"  ↳ Nuevo mensaje ignorado ({classification}) en {chat}.");
            continue;
        }

        var (autor, telefono) = await ExtraerRemitente(message, text);
        var solicitud = ExtractorInmobiliario.Extraer(text, chat, id);
        solicitud.Autor = autor;
        solicitud.Telefono = telefono;
        solicitudes.Add(solicitud);
    }

    return (revisados, solicitudes);
}

static async Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta(IPage page, SolicitudInmobiliaria s)
{
    try
    {
        if (!await AbrirChat(page, AlertSettings.ChatDestino))
            return (false, false, "abrir chat destino");

        ILocator? compose = null;
        var candidatos = new[]
        {
            "footer [contenteditable='true'][role='textbox']",
            "footer div[contenteditable='true']",
            "[data-testid='conversation-compose-box-input'][contenteditable='true']",
            "div[contenteditable='true'][role='textbox'][data-tab]"
        };

        foreach (var selector in candidatos)
        {
            var locator = page.Locator(selector).Last;
            if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
            {
                compose = locator;
                break;
            }
        }

        if (compose is null)
            return (false, false, "encontrar caja de mensaje");

        var alerta = ConstruirAlerta(s);
        await compose.ClickAsync();
        try { await compose.FillAsync(alerta); }
        catch { await page.Keyboard.InsertTextAsync(alerta); }

        await Task.Delay(AlertSettings.EsperaEnvioMs);
        await page.Keyboard.PressAsync("Enter");
        await Task.Delay(700);

        var marcado = await MarcarChatComoNoLeido(page);
        return (true, marcado, marcado ? "ok" : "enviado; no se pudo marcar no leído");
    }
    catch (Exception ex)
    {
        return (false, false, ex.Message);
    }
}

static async Task<bool> MarcarChatComoNoLeido(IPage page)
{
    try
    {
        // Abrir el menú de la conversación actual. WhatsApp cambia con frecuencia
        // sus test-id, por eso se prueban varias señales accesibles/visibles.
        ILocator? menuButton = null;
        var selectores = new[]
        {
            "header button[aria-label='Menú']",
            "header button[aria-label='Menu']",
            "header [data-testid='menu']",
            "header span[data-icon='menu']"
        };

        foreach (var selector in selectores)
        {
            var candidatos = page.Locator(selector);
            var total = await candidatos.CountAsync();
            for (var i = total - 1; i >= 0; i--)
            {
                var candidato = candidatos.Nth(i);
                if (!await candidato.IsVisibleAsync())
                    continue;

                if (selector.Contains("span[data-icon='menu']", StringComparison.Ordinal))
                {
                    var boton = candidato.Locator("xpath=ancestor::*[@role='button' or self::button][1]");
                    if (await boton.CountAsync() > 0)
                        menuButton = boton.First;
                }
                else
                {
                    menuButton = candidato;
                }

                if (menuButton is not null)
                    break;
            }

            if (menuButton is not null)
                break;
        }

        if (menuButton is null)
            return false;

        await menuButton.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
        await Task.Delay(250);

        var opciones = new[]
        {
            "Marcar como no leído",
            "Marcar como no leido",
            "Mark as unread"
        };

        foreach (var texto in opciones)
        {
            var opcion = page.GetByText(texto, new PageGetByTextOptions { Exact = true });
            var total = await opcion.CountAsync();

            for (var i = 0; i < total; i++)
            {
                var item = opcion.Nth(i);
                if (!await item.IsVisibleAsync())
                    continue;

                await item.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                await Task.Delay(300);
                return true;
            }
        }

        await page.Keyboard.PressAsync("Escape");
        return false;
    }
    catch
    {
        try { await page.Keyboard.PressAsync("Escape"); } catch { }
        return false;
    }
}

static string ConstruirAlerta(SolicitudInmobiliaria s)
{
    var sb = new StringBuilder();
    sb.AppendLine("🔥 RSMAPS RADAR");
    sb.AppendLine();
    sb.AppendLine("ORIGEN");
    sb.AppendLine($"Chat: {s.ChatOrigen}");
    sb.AppendLine($"Autor: {s.Autor ?? "No identificado"}");
    sb.AppendLine($"Teléfono: {s.Telefono ?? "No disponible"}");
    sb.AppendLine();
    sb.AppendLine("SOLICITUD");
    sb.AppendLine($"Operación: {s.Operacion ?? "No determinada"}");
    sb.AppendLine($"Tipo: {MostrarLista(s.TiposPropiedad)}");
    sb.AppendLine($"Zona: {MostrarLista(s.Zonas)}");
    if (s.PrecioMinimo.HasValue) sb.AppendLine($"Precio mínimo: {MostrarDinero(s.PrecioMinimo)}");
    if (s.PrecioMaximo.HasValue) sb.AppendLine($"Precio máximo: {MostrarDinero(s.PrecioMaximo)}");
    if (s.RecamarasMin.HasValue || s.RecamarasMax.HasValue) sb.AppendLine($"Recámaras: {MostrarRango(s.RecamarasMin, s.RecamarasMax)}");
    if (s.BanosMin.HasValue || s.BanosMax.HasValue) sb.AppendLine($"Baños: {MostrarRango(s.BanosMin, s.BanosMax)}");
    if (s.CocheraMinAutos.HasValue) sb.AppendLine($"Cochera mínima: {s.CocheraMinAutos}");
    if (s.AceptaMascotas.HasValue) sb.AppendLine($"Mascotas: {MostrarBooleano(s.AceptaMascotas)}");
    if (s.ModalidadesPago.Count > 0) sb.AppendLine($"Pago/crédito: {MostrarLista(s.ModalidadesPago)}");
    sb.AppendLine();
    sb.AppendLine("MENSAJE ORIGINAL");
    sb.AppendLine(s.MensajeOriginal.Trim());
    sb.AppendLine();
    sb.AppendLine("Estado: pendiente de comparar con RSMaps");
    return sb.ToString().Trim();
}

static async Task<(string? Autor, string? Telefono)> ExtraerRemitente(ILocator message, string textoCompleto)
{
    string? autor = null;
    string? telefono = null;
    try
    {
        var fila = message.Locator("xpath=ancestor::*[@role='row'][1]");
        if (await fila.CountAsync() > 0)
        {
            var perfil = fila.Locator("[data-testid='group-chat-profile-picture']");
            if (await perfil.CountAsync() > 0)
            {
                var aria = await perfil.First.GetAttributeAsync("aria-label") ?? string.Empty;
                var limpio = Regex.Replace(aria, @"^Abrir los detalles del chat para\s+(?:Quizá\s+)?", string.Empty, RegexOptions.IgnoreCase).Trim();
                telefono = ExtraerTelefono(limpio);
                autor = limpio;
                if (!string.IsNullOrWhiteSpace(telefono))
                    autor = autor.Replace(telefono, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                autor = Regex.Replace(autor, @"\s+", " ").Trim();
            }
        }
    }
    catch { }

    var lineas = textoCompleto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToList();
    telefono ??= lineas.Select(ExtraerTelefono).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    if (string.IsNullOrWhiteSpace(autor) && lineas.Count > 0)
    {
        var primera = lineas[0];
        if (ExtraerTelefono(primera) is null && !PareceContenidoInmobiliario(primera) && !Regex.IsMatch(primera, @"^reenviado$", RegexOptions.IgnoreCase))
            autor = primera.Trim();
    }

    return (string.IsNullOrWhiteSpace(autor) ? null : autor, telefono);
}

static bool PareceContenidoInmobiliario(string texto)
{
    var t = Normalizar(texto);
    return new[] { "busco", "buscamos", "solicito", "renta", "venta", "casa", "departamento", "terreno", "bodega", "local", "presupuesto" }.Any(t.Contains);
}

static string? ExtraerTelefono(string texto)
{
    var mexico = Regex.Match(texto, @"\+52\s*(?:1\s*)?\d{3}\s*\d{3}\s*\d{4}");
    if (mexico.Success) return Regex.Replace(mexico.Value, @"\s+", " ").Trim();
    var general = Regex.Match(texto, @"\+?\d(?:[\s-]*\d){9,14}");
    return general.Success ? Regex.Replace(general.Value, @"\s+", " ").Trim() : null;
}

static TipoMensaje ClasificarMensaje(string texto)
{
    var text = Normalizar(texto);
    string[] demandaFuerte =
    {
        "busco", "buscando", "buscamos", "ando buscando", "estoy buscando", "estamos buscando",
        "sigo en busqueda", "aun sigo en busqueda", "solicito para cliente", "solicito renta",
        "solicito casa", "solicito terreno", "solicito departamento", "necesito", "necesitamos",
        "requiero", "requerimos", "cliente busca", "mi cliente busca", "para un cliente", "para cliente",
        "alguien tendra", "alguien traera", "algun compañero tiene", "alguien tiene", "me pudiera compartir",
        "me pueden compartir opciones", "agradezco sus opciones", "recibo propuesta", "recibo propuestas"
    };
    string[] ofertaFuerte =
    {
        "ofrezco", "vendo", "rento", "se vende", "se renta", "pongo a su disposicion", "pongo a la disposicion",
        "tenemos a la venta", "tenemos en venta", "tenemos a la renta", "tenemos en renta", "propiedad en preventa",
        "casa en preventa", "casa en venta", "departamento en renta", "terreno en venta", "local en renta",
        "bodega en renta", "tenemos disponible", "tengo disponible"
    };
    string[] exclusionesDemanda = { "solicitar la licencia", "solicitar licencia", "solicitar informacion", "solicitar constancia", "solicitar informes" };

    if (exclusionesDemanda.Any(text.Contains) && !demandaFuerte.Any(text.Contains)) return TipoMensaje.Otro;
    if (demandaFuerte.Any(text.Contains)) return TipoMensaje.Demanda;
    if (ofertaFuerte.Any(text.Contains)) return TipoMensaje.Oferta;

    string[] demandaDebil = { "tendran", "tendras", "alguna propiedad", "alguna casa", "algun terreno", "alguna bodega", "algun local" };
    return demandaDebil.Any(text.Contains) ? TipoMensaje.Demanda : TipoMensaje.Otro;
}

static string Normalizar(string texto) => texto.ToLowerInvariant().Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ü", "u").Replace("ñ", "n");

static void MostrarSolicitud(SolicitudInmobiliaria s)
{
    Console.WriteLine();
    Console.WriteLine("==============================================");
    Console.WriteLine("🔥 SOLICITUD INMOBILIARIA");
    Console.WriteLine("==============================================");
    Console.WriteLine($"Chat:          {s.ChatOrigen}");
    Console.WriteLine($"Autor:         {s.Autor ?? "-"}");
    Console.WriteLine($"Teléfono:      {s.Telefono ?? "-"}");
    Console.WriteLine($"Operación:     {s.Operacion ?? "No determinada"}");
    Console.WriteLine($"Tipos:         {MostrarLista(s.TiposPropiedad)}");
    Console.WriteLine($"Zonas:         {MostrarLista(s.Zonas)}");
    Console.WriteLine($"Precio mín.:   {MostrarDinero(s.PrecioMinimo)}");
    Console.WriteLine($"Precio máx.:   {MostrarDinero(s.PrecioMaximo)}");
    Console.WriteLine($"Recámaras:     {MostrarRango(s.RecamarasMin, s.RecamarasMax)}");
    Console.WriteLine($"Baños:         {MostrarRango(s.BanosMin, s.BanosMax)}");
    Console.WriteLine($"Terreno mín.:  {MostrarMetros(s.TerrenoMinM2)}");
    Console.WriteLine($"Construcc. mín:{MostrarMetros(s.ConstruccionMinM2, true)}");
    Console.WriteLine($"Mascotas:      {MostrarBooleano(s.AceptaMascotas)}");
    Console.WriteLine($"Amueblado:     {MostrarBooleano(s.Amueblado)}");
    Console.WriteLine($"Una planta:    {MostrarBooleano(s.UnaPlanta)}");
    Console.WriteLine($"Vigilancia:    {MostrarBooleano(s.CasetaVigilancia)}");
    Console.WriteLine($"Cochera mín.:  {s.CocheraMinAutos?.ToString() ?? "-"}");
    Console.WriteLine($"Pago/crédito:  {MostrarLista(s.ModalidadesPago)}");
    Console.WriteLine();
    Console.WriteLine("MENSAJE ORIGINAL:");
    Console.WriteLine(s.MensajeOriginal);
    Console.WriteLine();
    Console.WriteLine($"ID: {s.MessageId}");
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

static string MostrarBooleano(bool? valor) => valor switch
{
    true => "Sí",
    false => "No",
    null => "-"
};

enum TipoMensaje
{
    Demanda,
    Oferta,
    Otro
}
