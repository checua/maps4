using Microsoft.Playwright;
using System.Text;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==================================");
Console.WriteLine("      RSMaps Radar v0.8.0-matching");
Console.WriteLine("==================================");
Console.WriteLine();

using var instanceLock = RadarAgentInstanceLock.Acquire();

var userDataDir = Path.Combine(AppContext.BaseDirectory, "WhatsAppProfile");

using var playwright = await Playwright.CreateAsync();
var context = await playwright.Chromium.LaunchPersistentContextAsync(
    userDataDir,
    new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = false,
        ViewportSize = null
    });

Console.WriteLine("WhatsApp Web abierto.");
Console.WriteLine("Esperando que cargue la lista de chats...");

var page = await RadarWhatsAppSession.ObtenerPaginaActivaAsync(context);

await RadarWhatsAppChatDiscovery.DescubrirYReportarAsync(
    page,
    RadarSettings.ConfiguracionAgente);

var chatsIniciales = RadarSettings.ChatsMonitoreados;

Console.WriteLine();
Console.WriteLine("Chats configurados para Radar:");
foreach (var chat in chatsIniciales)
    Console.WriteLine($"  • {chat}");
Console.WriteLine(RadarSettings.ModoSeguroLab
    ? "Chat de alertas: (bloqueado por MODO SEGURO LAB)"
    : $"Chat de alertas: {AlertSettings.ChatDestino}");

var idsConocidosPorChat = new Dictionary<string, HashSet<string>>(
    StringComparer.OrdinalIgnoreCase);

// Tracks successful partial deliveries while a multi-request message is pending.
// This prevents re-sending already confirmed alerts during an in-process retry.
var enviosConfirmadosPorSolicitud = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// LAB-only state used to simulate one failed delivery followed by recovery.
var entregasLabFalladasUnaVez = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

IRadarInterpreter interpreter = RadarInterpreterFactory.Create();
Console.WriteLine($"Intérprete Radar: {interpreter.GetType().Name}");

Console.WriteLine();
Console.WriteLine("Inicializando chats...");

foreach (var chat in chatsIniciales)
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
    var mensajesAntes = idsConocidosPorChat.Values.Sum(ids => ids.Count);

    foreach (var chat in idsConocidosPorChat.Keys.ToList())
    {
        if (!await AbrirChat(page, chat))
            continue;

        await EstabilizarMensajesChat(page, idsConocidosPorChat[chat]);
    }

    var mensajesDespues = idsConocidosPorChat.Values.Sum(ids => ids.Count);
    var mensajesAdicionales = mensajesDespues - mensajesAntes;

    Console.WriteLine(
        $"  Ronda {ronda}: {mensajesAdicionales} mensaje(s) adicional(es) absorbido(s).");

    if (mensajesAdicionales == 0)
    {
        Console.WriteLine("  Historial estable; se omiten rondas adicionales.");
        break;
    }
}

Console.WriteLine();
Console.WriteLine("==================================");
Console.WriteLine("          RADAR ACTIVO");
Console.WriteLine("==================================");
if (RadarSettings.ModoSeguroLab)
{
    Console.WriteLine("🧪 MODO SEGURO LAB: detección y matching activos; envío de WhatsApp bloqueado.");
    Console.WriteLine("Las coincidencias se mostrarán únicamente en consola.");
}
else
{
    Console.WriteLine($"Las solicitudes nuevas se enviarán a {AlertSettings.ChatDestino}.");
    Console.WriteLine($"Después del envío Radar volverá al chat origen e intentará marcar {AlertSettings.ChatDestino} como no leído.");
}
Console.WriteLine("CTRL+C para terminar.");
Console.WriteLine();

while (true)
{
    try
    {
        page = await RadarWhatsAppSession.ObtenerPaginaActivaAsync(context, page);
        await RadarWhatsAppChatDiscovery.ActualizarSiCorrespondeAsync(
            page,
            RadarSettings.ConfiguracionAgente);

        // Tomamos una fotografía estable de la configuración para este barrido.
        // Si el heartbeat cambia la configuración mientras recorremos los chats,
        // el siguiente ciclo aplicará el nuevo conjunto completo sin mezclar estados.
        var chatsCiclo = RadarSettings.ChatsMonitoreados;
        var inicializadosAhora = await ReconciliarEstadoChatsAsync(
            page,
            chatsCiclo,
            idsConocidosPorChat);

        foreach (var chat in chatsCiclo)
        {
            // Un chat recién agregado primero absorbe su historial visible como línea base.
            // Se empieza a detectar demanda nueva a partir del siguiente barrido.
            if (inicializadosAhora.Contains(chat))
                continue;

            if (!await AbrirChat(page, chat))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ No pude abrir {chat}.");
                continue;
            }

            if (!idsConocidosPorChat.TryGetValue(chat, out var idsConocidos))
            {
                idsConocidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await EstabilizarMensajesChat(page, idsConocidos);
                idsConocidosPorChat[chat] = idsConocidos;
                Console.WriteLine(
                    $"  ⚙ Fuente inicializada de forma defensiva: {chat} · " +
                    $"{idsConocidos.Count} mensaje(s) usados como historial base.");
                continue;
            }

            var resultado = await ProcesarMensajesNuevos(page, chat, idsConocidos, interpreter);

            if (resultado.Revisados > 0)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] {chat}: {resultado.Revisados} mensaje(s) nuevo(s), " +
                    $"{resultado.Solicitudes.Count} solicitud(es).");
            }

            foreach (var messageId in resultado.DemandasInterpretadas)
            {
                var solicitudesMensaje = resultado.Solicitudes
                    .Where(x => string.Equals(x.MessageId, messageId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (solicitudesMensaje.Count == 0)
                {
                    idsConocidos.Add(messageId);
                    Console.WriteLine($"  [ACK] {messageId}: Intelligence finished with no actionable requests.");
                    continue;
                }

                var mensajeCompletado = true;

                for (var indice = 0; indice < solicitudesMensaje.Count; indice++)
                {
                    var solicitud = solicitudesMensaje[indice];
                    MostrarSolicitud(solicitud);

                    solicitud.MatchingResumen = await RsMapsMatchingClient.ConstruirResumenAsync(solicitud);
                    Console.WriteLine(
                        $"  MATCH RSMAPS: {solicitud.MatchingResumen.Replace("\r", " ").Replace("\n", " | ")}");

                    if (MatchingTemporalmenteNoDisponible(solicitud))
                    {
                        mensajeCompletado = false;
                        Console.WriteLine("  [PENDING] Matching is not confirmed; message will be retried.");
                        break;
                    }

                    if (!TieneCoincidenciaUtil(solicitud))
                    {
                        Console.WriteLine("  [NO ALERT] No useful match; WhatsApp alert is not sent.");
                        continue;
                    }

                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);
                    var pruebaEntregaLab = Environment.GetEnvironmentVariable("RADAR_SAFE_LAB_DELIVERY_TEST")?.Trim();
                    var simularFailOnce = RadarSettings.ModoSeguroLab
                        && string.Equals(pruebaEntregaLab, "fail-once", StringComparison.OrdinalIgnoreCase);

                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)
                    {
                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");
                        continue;
                    }

                    RadarDeliveryPrepareClientResult? entregaDurable = null;
                    if (RadarDeliveryClient.Habilitada)
                    {
                        entregaDurable = await RadarDeliveryClient.PrepararAsync(
                            solicitud.ChatOrigen,
                            messageId,
                            indice,
                            claveEntrega,
                            solicitud.IdInmuebleCoincidente,
                            solicitud.MejorCoincidencia,
                            solicitud.MensajeOriginal + Environment.NewLine + Environment.NewLine + solicitud.MatchingResumen);

                        if (!entregaDurable.Ok)
                        {
                            mensajeCompletado = false;
                            Console.WriteLine(
                                $"  [PENDING] Durable delivery could not be prepared: {entregaDurable.Detalle}. Message will be retried.");
                            break;
                        }

                        if (entregaDurable.YaEnviado)
                        {
                            enviosConfirmadosPorSolicitud.Add(claveEntrega);
                            Console.WriteLine(
                                $"  [DEDUP DURABLE] Delivery #{entregaDurable.IdRadarMessageDelivery} was already confirmed by RSMaps; duplicate WhatsApp send skipped.");
                            continue;
                        }

                        Console.WriteLine(
                            $"  [DELIVERY] Durable delivery #{entregaDurable.IdRadarMessageDelivery} prepared Â· attempt {entregaDurable.IntentosEntrega}.");
                    }

                    if (enviosConfirmadosPorSolicitud.Contains(claveEntrega))
                    {
                        if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                        {
                            var confirmacionPendiente = await RadarDeliveryClient.CompletarAsync(
                                entregaDurable.IdRadarMessageDelivery,
                                true,
                                null);

                            if (!confirmacionPendiente.Ok)
                            {
                                mensajeCompletado = false;
                                Console.WriteLine(
                                    $"  [PENDING] Alert was already delivered locally, but durable confirmation is still pending: {confirmacionPendiente.Detalle}.");
                                break;
                            }

                            Console.WriteLine(
                                $"  [DEDUP] Previous local delivery confirmed durably as #{entregaDurable.IdRadarMessageDelivery}; duplicate send skipped.");
                            continue;
                        }

                        Console.WriteLine("  [DEDUP] This alert was already delivered during a previous retry; skipping duplicate.");
                        continue;
                    }

                    (bool Enviada, bool MarcadoNoLeido, string Detalle) envio;

                    if (simularFailOnce)
                    {
                        if (entregasLabFalladasUnaVez.Add(claveEntrega))
                        {
                            envio = (false, false, "SAFE LAB simulated first delivery failure");
                            Console.WriteLine("  [SAFE LAB TEST] First delivery attempt intentionally failed; no WhatsApp message was sent.");
                        }
                        else
                        {
                            envio = (true, true, "SAFE LAB simulated delivery recovery");
                            Console.WriteLine("  [SAFE LAB TEST] Retry delivery intentionally succeeded; no WhatsApp message was sent.");
                        }
                    }
                    else
                    {
                        envio = await EnviarAlerta(page, solicitud);
                    }

                    if (!envio.Enviada)
                    {
                        if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                        {
                            var falloDurable = await RadarDeliveryClient.CompletarAsync(
                                entregaDurable.IdRadarMessageDelivery,
                                false,
                                envio.Detalle);

                            if (!falloDurable.Ok)
                            {
                                Console.WriteLine(
                                    $"  [WARN] Could not persist delivery failure: {falloDurable.Detalle}");
                            }
                        }

                        mensajeCompletado = false;
                        Console.WriteLine(
                            $"  [PENDING] Could not deliver alert to {AlertSettings.ChatDestino}. Stage: {envio.Detalle}. Message will be retried.");
                        break;
                    }

                    enviosConfirmadosPorSolicitud.Add(claveEntrega);

                    if (entregaDurable is not null && entregaDurable.IdRadarMessageDelivery > 0)
                    {
                        var confirmacionDurable = await RadarDeliveryClient.CompletarAsync(
                            entregaDurable.IdRadarMessageDelivery,
                            true,
                            null);

                        if (!confirmacionDurable.Ok)
                        {
                            mensajeCompletado = false;
                            Console.WriteLine(
                                $"  [PENDING] WhatsApp delivery succeeded, but durable confirmation failed: {confirmacionDurable.Detalle}. No duplicate will be sent during this process.");
                            break;
                        }

                        Console.WriteLine(
                            $"  [DELIVERY] Durable delivery #{entregaDurable.IdRadarMessageDelivery} confirmed ENVIADO.");
                    }

                    if (envio.MarcadoNoLeido)
                    {
                        Console.WriteLine(
                            $"  [SENT] Alert delivered to {AlertSettings.ChatDestino}, returned to origin and marked unread.");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"  [SENT] Alert delivered to {AlertSettings.ChatDestino}. Could not mark it unread.");
                    }
                }

                if (mensajeCompletado)
                {
                    idsConocidos.Add(messageId);
                    var prefijo = messageId + ":";
                    enviosConfirmadosPorSolicitud.RemoveWhere(
                        x => x.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"  [ACK] {messageId}: terminal processing completed.");
                }
                else
                {
                    Console.WriteLine($"  [PENDING] {messageId}: not acknowledged; retry remains enabled.");
                }
            }
        }
    }
    catch (PlaywrightException ex)
    {
        Console.WriteLine($"[PLAYWRIGHT] {ex.Message}");

        try
        {
            page = await RadarWhatsAppSession.ObtenerPaginaActivaAsync(
                context,
                page,
                mostrarRecuperacion: true);
        }
        catch (Exception recuperacionEx)
        {
            Console.WriteLine($"[RADAR] No pude recuperar WhatsApp Web: {recuperacionEx.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RADAR] {ex.Message}");
    }

    await Task.Delay(RadarSettings.IntervaloRevisionMs);
}

static async Task<HashSet<string>> ReconciliarEstadoChatsAsync(
    IPage page,
    IReadOnlyCollection<string> chatsConfigurados,
    Dictionary<string, HashSet<string>> idsConocidosPorChat)
{
    var configurados = chatsConfigurados
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var eliminados = idsConocidosPorChat.Keys
        .Where(chat => !configurados.Contains(chat))
        .ToList();

    foreach (var chat in eliminados)
    {
        idsConocidosPorChat.Remove(chat);
        Console.WriteLine($"  ⚙ Fuente retirada: {chat} · estado local eliminado.");
    }

    var nuevos = chatsConfigurados
        .Where(chat => !idsConocidosPorChat.ContainsKey(chat))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var inicializadosAhora = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var chat in nuevos)
    {
        if (!await AbrirChat(page, chat))
        {
            Console.WriteLine(
                $"  ⚠ Fuente nueva pendiente de inicializar: {chat}. " +
                "RADAR volverá a intentarlo en el siguiente barrido.");
            continue;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await EstabilizarMensajesChat(page, ids);
        idsConocidosPorChat[chat] = ids;
        inicializadosAhora.Add(chat);

        Console.WriteLine(
            $"  ⚙ Fuente agregada: {chat} · historial base {ids.Count} mensaje(s); " +
            "solo se procesarán mensajes posteriores.");
    }

    if (eliminados.Count > 0 || nuevos.Count > 0)
    {
        Console.WriteLine(
            $"  ⚙ Estado RADAR reconciliado: {idsConocidosPorChat.Count} fuente(s) activas.");
    }

    return inicializadosAhora;
}

static async Task<bool> AbrirChat(IPage page, string nombreChat)
{
    if (await ClickPorTituloVisible(page, nombreChat) && await EsperarChatAbierto(page, nombreChat))
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

            if (!await ClickPorTituloVisible(page, nombreChat))
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

static async Task<bool> ClickPorTituloVisible(IPage page, string nombreChat)
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
                    var contenedor = item.Locator(
                        "xpath=ancestor::*[@tabindex='0' or @tabindex='-1' or @role='button' or @role='listitem' or @role='row'][1]");

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
    var total = await titulos.CountAsync();

    for (var i = 0; i < total; i++)
    {
        var titulo = titulos.Nth(i);
        try
        {
            if (!await titulo.IsVisibleAsync())
                continue;

            var texto = (await titulo.InnerTextAsync()).Trim();
            if (!EsMismoChat(texto, nombreChat))
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

    var actual = N(tituloActual);
    var objetivo = N(esperado);

    if (string.Equals(actual, objetivo, StringComparison.OrdinalIgnoreCase))
        return true;

    return actual.StartsWith(objetivo, StringComparison.OrdinalIgnoreCase);
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
        var actual = (await title.InnerTextAsync()).Trim();
        if (EsMismoChat(actual, chat))
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

        if (ids.Count == anterior)
            sinCambios++;
        else
            sinCambios = 0;

        anterior = ids.Count;
        await Task.Delay(400);
    }
}

static async Task AbsorberMensajesActuales(IPage page, HashSet<string> knownIds)
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

static async Task<(int Revisados, List<SolicitudInmobiliaria> Solicitudes, HashSet<string> DemandasInterpretadas)> ProcesarMensajesNuevos(
    IPage page,
    string chat,
    HashSet<string> knownIds,
    IRadarInterpreter interpreter)
{
    var messages = page.Locator("[data-testid^='conv-msg-'][data-id]");
    var count = await messages.CountAsync();
    var revisados = 0;
    var solicitudes = new List<SolicitudInmobiliaria>();
    var demandasInterpretadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < count; i++)
    {
        var message = messages.Nth(i);
        var id = await message.GetAttributeAsync("data-id");

        if (string.IsNullOrWhiteSpace(id) || knownIds.Contains(id))
            continue;


        var text = (await message.InnerTextAsync()).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            knownIds.Add(id);
            revisados++;
            continue;
        }

        var classification = ClasificarMensaje(text);
        if (classification != TipoMensaje.Demanda)
        {
            knownIds.Add(id);
            revisados++;
            Console.WriteLine($"  ↳ Nuevo mensaje ignorado ({classification}) en {chat}.");
            continue;
        }

        var (autor, telefono) = await ExtraerRemitente(message, text);
        var radarMessage = new RadarMessage
        {
            MessageId = id,
            ChatOrigen = chat,
            Autor = autor,
            Telefono = telefono,
            TextoOriginal = text,
            DetectadoEn = DateTime.Now
        };

        // Demand messages are only acknowledged after all terminal downstream work finishes.
        var interpretacion = await interpreter.InterpretarAsync(radarMessage);
        demandasInterpretadas.Add(id);
        revisados++;
        Console.WriteLine(
            $"  ↳ Interpretación {interpretacion.Motor}: {interpretacion.Solicitudes.Count} solicitud(es).");

        solicitudes.AddRange(interpretacion.Solicitudes);
    }

    return (revisados, solicitudes, demandasInterpretadas);
}

static bool MatchingTemporalmenteNoDisponible(SolicitudInmobiliaria solicitud)
{
    if (string.IsNullOrWhiteSpace(solicitud.MatchingResumen))
        return true;

    // Warning summaries represent a transient/operational matching failure, not a terminal no-match.
    return solicitud.MatchingResumen.Contains("\u26A0");
}

static bool TieneCoincidenciaUtil(SolicitudInmobiliaria solicitud)
{
    // Keep this aligned with RadarMatchingService.PuntuacionMinimaCandidato.
    return solicitud.IdInmuebleCoincidente.HasValue
        && solicitud.MejorCoincidencia.HasValue
        && solicitud.MejorCoincidencia.Value >= 55;
}

static string ClaveEntrega(string messageId, int indice, SolicitudInmobiliaria solicitud) =>
    $"{messageId}:{indice}:{solicitud.IdInmuebleCoincidente?.ToString() ?? "-"}";

static async Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta(
    IPage page,
    SolicitudInmobiliaria s)
{
    try
    {
        if (!await AbrirChat(page, AlertSettings.ChatDestino))
            return (false, false, "abrir chat destino");

        var compose = await ObtenerCajaMensaje(page);
        if (compose is null)
            return (false, false, "encontrar caja de mensaje");

        var alerta = ConstruirAlerta(s);
        await compose.ClickAsync();

        try
        {
            await compose.FillAsync(alerta);
        }
        catch
        {
            await page.Keyboard.InsertTextAsync(alerta);
        }

        await Task.Delay(AlertSettings.EsperaEnvioMs);
        await page.Keyboard.PressAsync("Enter");
        await Task.Delay(700);

        var regresoOrigen = await AbrirChat(page, s.ChatOrigen);
        if (!regresoOrigen)
            return (true, false, "enviado; no pude regresar al chat origen");

        await Task.Delay(300);
        var marcado = await MarcarChatEnListaComoNoLeido(page, AlertSettings.ChatDestino);

        return (
            true,
            marcado,
            marcado ? "ok" : "enviado; no se pudo marcar no leído desde la lista");
    }
    catch (Exception ex)
    {
        return (false, false, ex.Message);
    }
}

static async Task<ILocator?> ObtenerCajaMensaje(IPage page)
{
    var selectores = new[]
    {
        "footer [contenteditable='true'][role='textbox']",
        "footer div[contenteditable='true']",
        "[data-testid='conversation-compose-box-input'][contenteditable='true']",
        "div[contenteditable='true'][role='textbox'][data-tab]"
    };

    foreach (var selector in selectores)
    {
        var candidatos = page.Locator(selector);
        var total = await candidatos.CountAsync();

        for (var i = total - 1; i >= 0; i--)
        {
            var candidato = candidatos.Nth(i);
            try
            {
                if (await candidato.IsVisibleAsync())
                    return candidato;
            }
            catch { }
        }
    }

    return null;
}

static async Task<bool> MarcarChatEnListaComoNoLeido(IPage page, string nombreChat)
{
    try
    {
        await LimpiarBusqueda(page);

        var titulos = page.Locator("[data-testid='cell-frame-title']");
        ILocator? tituloObjetivo = null;

        for (var i = 0; i < await titulos.CountAsync(); i++)
        {
            var titulo = titulos.Nth(i);
            if (!await titulo.IsVisibleAsync())
                continue;

            var texto = (await titulo.InnerTextAsync()).Trim();
            if (EsMismoChat(texto, nombreChat))
            {
                tituloObjetivo = titulo;
                break;
            }
        }

        if (tituloObjetivo is null)
        {
            var input = await ObtenerInputBusqueda(page);
            if (input is null)
                return false;

            await input.ClickAsync();
            await input.FillAsync(nombreChat);
            await Task.Delay(Math.Max(RadarSettings.EsperaBusquedaMs, 900));

            titulos = page.Locator("[data-testid='cell-frame-title']");
            for (var i = 0; i < await titulos.CountAsync(); i++)
            {
                var titulo = titulos.Nth(i);
                if (!await titulo.IsVisibleAsync())
                    continue;

                var texto = (await titulo.InnerTextAsync()).Trim();
                if (EsMismoChat(texto, nombreChat))
                {
                    tituloObjetivo = titulo;
                    break;
                }
            }
        }

        if (tituloObjetivo is null)
        {
            await LimpiarBusqueda(page);
            return false;
        }

        var fila = tituloObjetivo.Locator(
            "xpath=ancestor::*[@role='row' or @role='listitem' or @tabindex='-1' or @tabindex='0'][1]");

        if (await fila.CountAsync() == 0)
            fila = tituloObjetivo.Locator("xpath=ancestor::div[6]");

        if (await fila.CountAsync() == 0)
        {
            await LimpiarBusqueda(page);
            return false;
        }

        await fila.First.HoverAsync();
        await Task.Delay(250);

        ILocator? botonMenu = null;
        var selectoresMenu = new[]
        {
            "span[data-icon='down-context']",
            "[data-testid='down']",
            "button[aria-label*='menú' i]",
            "button[aria-label*='menu' i]"
        };

        foreach (var selector in selectoresMenu)
        {
            var candidatos = fila.First.Locator(selector);
            var total = await candidatos.CountAsync();

            for (var i = 0; i < total; i++)
            {
                var candidato = candidatos.Nth(i);
                if (!await candidato.IsVisibleAsync())
                    continue;

                if (selector.StartsWith("span", StringComparison.OrdinalIgnoreCase))
                {
                    var ancestro = candidato.Locator(
                        "xpath=ancestor::*[@role='button' or self::button or @tabindex='0'][1]");
                    botonMenu = await ancestro.CountAsync() > 0 ? ancestro.First : candidato;
                }
                else
                {
                    botonMenu = candidato;
                }
                break;
            }

            if (botonMenu is not null)
                break;
        }

        if (botonMenu is null)
        {
            await LimpiarBusqueda(page);
            return false;
        }

        await botonMenu.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
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
                await Task.Delay(350);
                await LimpiarBusqueda(page);
                return true;
            }
        }

        await page.Keyboard.PressAsync("Escape");
        await LimpiarBusqueda(page);
        return false;
    }
    catch
    {
        try { await page.Keyboard.PressAsync("Escape"); } catch { }
        await LimpiarBusqueda(page);
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
    sb.AppendLine($"Subtipo: {MostrarLista(s.SubtiposPropiedad)}");
    sb.AppendLine($"Zona: {MostrarLista(s.Zonas)}");

    if (s.PrecioMinimo.HasValue)
        sb.AppendLine($"Precio mínimo: {MostrarDinero(s.PrecioMinimo)}");

    if (s.PrecioMaximo.HasValue)
        sb.AppendLine($"Precio máximo: {MostrarDinero(s.PrecioMaximo)}");

    if (s.RecamarasMin.HasValue || s.RecamarasMax.HasValue)
        sb.AppendLine($"Recámaras: {MostrarRango(s.RecamarasMin, s.RecamarasMax)}");

    if (s.BanosMin.HasValue || s.BanosMax.HasValue)
        sb.AppendLine($"Baños: {MostrarRango(s.BanosMin, s.BanosMax)}");

    if (s.CocheraMinAutos.HasValue)
        sb.AppendLine($"Cochera mínima: {s.CocheraMinAutos}");

    if (s.AceptaMascotas.HasValue)
        sb.AppendLine($"Mascotas: {MostrarBooleano(s.AceptaMascotas)}");

    if (s.ModalidadesPago.Count > 0)
        sb.AppendLine($"Pago/crédito: {MostrarLista(s.ModalidadesPago)}");

    sb.AppendLine();
    sb.AppendLine("MENSAJE ORIGINAL");
    sb.AppendLine(s.MensajeOriginal.Trim());
    sb.AppendLine();
    if (!string.IsNullOrWhiteSpace(s.MatchingResumen))
    {
        sb.AppendLine(s.MatchingResumen);
    }
    else
    {
        sb.AppendLine("COINCIDENCIAS RSMAPS");
        sb.AppendLine("AVISO: Comparacion no disponible.");
    }

    return sb.ToString().Trim();
}

static async Task<(string? Autor, string? Telefono)> ExtraerRemitente(
    ILocator message,
    string textoCompleto)
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
                var limpio = Regex.Replace(
                    aria,
                    @"^Abrir los detalles del chat para\s+(?:Quizá\s+)?",
                    string.Empty,
                    RegexOptions.IgnoreCase).Trim();

                telefono = ExtraerTelefono(limpio);
                autor = limpio;

                if (!string.IsNullOrWhiteSpace(telefono))
                {
                    autor = autor.Replace(
                        telefono,
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase).Trim();
                }

                autor = Regex.Replace(autor, @"\s+", " ").Trim();
            }
        }
    }
    catch { }

    var lineas = textoCompleto
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Take(4)
        .ToList();

    telefono ??= lineas
        .Select(ExtraerTelefono)
        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    if (string.IsNullOrWhiteSpace(autor) && lineas.Count > 0)
    {
        var primera = lineas[0];

        if (ExtraerTelefono(primera) is null &&
            !PareceContenidoInmobiliario(primera) &&
            !Regex.IsMatch(primera, @"^reenviado$", RegexOptions.IgnoreCase))
        {
            autor = primera.Trim();
        }
    }

    return (
        string.IsNullOrWhiteSpace(autor) ? null : autor,
        telefono);
}

static bool PareceContenidoInmobiliario(string texto)
{
    var t = Normalizar(texto);

    return new[]
    {
        "busco", "buscamos", "solicito", "renta", "venta", "casa",
        "departamento", "terreno", "bodega", "local", "presupuesto"
    }.Any(t.Contains);
}

static string? ExtraerTelefono(string texto)
{
    var mexico = Regex.Match(
        texto,
        @"\+52\s*(?:1\s*)?\d{3}\s*\d{3}\s*\d{4}");

    if (mexico.Success)
        return Regex.Replace(mexico.Value, @"\s+", " ").Trim();

    var general = Regex.Match(
        texto,
        @"\+?\d(?:[\s-]*\d){9,14}");

    return general.Success
        ? Regex.Replace(general.Value, @"\s+", " ").Trim()
        : null;
}

static TipoMensaje ClasificarMensaje(string texto)
{
    var text = Normalizar(texto);

    string[] demandaFuerte =
    {
        "busco", "buscando", "buscamos", "ando buscando", "estoy buscando",
        "estamos buscando", "sigo en busqueda", "aun sigo en busqueda",
        "solicito para cliente", "solicito renta", "solicito casa",
        "solicito terreno", "solicito departamento", "necesito", "necesitamos",
        "requiero", "requerimos", "cliente busca", "mi cliente busca",
        "para un cliente", "para cliente", "alguien tendra", "alguien traera",
        "algun compañero tiene", "alguien tiene", "me pudiera compartir",
        "me pueden compartir opciones", "agradezco sus opciones",
        "recibo propuesta", "recibo propuestas"
    };

    string[] ofertaFuerte =
    {
        "ofrezco", "vendo", "rento", "se vende", "se renta",
        "pongo a su disposicion", "pongo a la disposicion",
        "tenemos a la venta", "tenemos en venta", "tenemos a la renta",
        "tenemos en renta", "propiedad en preventa", "casa en preventa",
        "casa en venta", "departamento en renta", "terreno en venta",
        "local en renta", "bodega en renta", "tenemos disponible", "tengo disponible"
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

    string[] demandaDebil =
    {
        "tendran", "tendras", "alguna propiedad", "alguna casa",
        "algun terreno", "alguna bodega", "algun local"
    };

    return demandaDebil.Any(text.Contains)
        ? TipoMensaje.Demanda
        : TipoMensaje.Otro;
}

static string Normalizar(string texto) => texto
    .ToLowerInvariant()
    .Replace("á", "a")
    .Replace("é", "e")
    .Replace("í", "i")
    .Replace("ó", "o")
    .Replace("ú", "u")
    .Replace("ü", "u")
    .Replace("ñ", "n");

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
    Console.WriteLine($"Subtipos:      {MostrarLista(s.SubtiposPropiedad)}");
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

static string MostrarDinero(decimal? valor) =>
    valor.HasValue ? valor.Value.ToString("C0") : "-";

static string MostrarRango(int? min, int? max)
{
    if (!min.HasValue && !max.HasValue)
        return "-";

    if (min == max)
        return min?.ToString() ?? "-";

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
