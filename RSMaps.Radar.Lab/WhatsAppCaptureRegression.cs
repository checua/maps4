using Microsoft.Playwright;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class WhatsAppCaptureRegression
{
    private const string MessageSelector = "[data-testid^='conv-msg-']";

    public static async Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("REGRESION CAPTURA WHATSAPP REPLY / QUOTE");
        Console.WriteLine("==============================================");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        IPage page = await browser.NewPageAsync();

        await VerificarNormalAsync(page);
        await VerificarReplyOfertaAsync(page);
        await VerificarReplyNuevaBusquedaAsync(page);
        await VerificarCitaSinTextoPropioAsync(page);
        await VerificarForwardedAsync(page);
        await VerificarNormalBaseAsync(page);
        await VerificarPreciosSeparadosAsync(page);
        await VerificarMultiplesSelectableTextAsync(page);
        await VerificarDomInesperadoAsync(page);
        await VerificarReplyForwardedAsync(page);
        await VerificarQuoteMediaAsync(page);
        await VerificarSelectableAnidadoAsync(page);
        await VerificarOcultoAsync(page);
        await VerificarWhitespaceUnicodeAsync(page);
        await VerificarContenedorDuplicadoAsync(page);
        await VerificarQuoteDuplicadoAsync(page);
        await VerificarForwardedSinTextoAsync(page);
        await VerificarMessageIdAusenteAsync(page);
        await VerificarSelectableFueraAsync(page);
        await VerificarEstructuraAmbiguaAsync(page);
        await VerificarPlaywrightTransitorioAsync(browser);

        await VerificarReplyOfertaNoEsDemandaAsync(page);
        await VerificarReplyNuevaBusquedaEsDemandaAsync(page);
        await VerificarPrecioCitadoNoLlegaAlClassifierAsync(page);
        await VerificarCapturaNoConfirmadaConservaRetryAsync(page);

        Console.WriteLine("WHATSAPP_REPLY_CLASSIFICATION_REGRESSION_OK");
        Console.WriteLine("WHATSAPP_CAPTURE_REGRESSION_OK");
    }

    private static async Task VerificarNormalAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje("<span class='selectable-text'>Busco terreno para vivero</span>"));

        Verificar(!capture.TieneCita, "A: NORMAL no debe marcar cita.");
        Verificar(capture.Confirmado, "A: NORMAL debe quedar confirmado.");
        Verificar(capture.TextoPropio == "Busco terreno para vivero",
            "A: NORMAL debe preservar el texto propio.");
        Verificar(capture.TextoCitado is null, "A: NORMAL no debe tener texto citado.");
        Verificar(!capture.EsReenviado, "A: NORMAL no debe marcar forwarded.");
    }

    private static async Task VerificarReplyOfertaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco terreno para vivero") +
                "<span class='selectable-text'>Yo tengo uno en Mezquital</span>"));

        Verificar(capture.TieneCita, "B: REPLY debe marcar cita.");
        Verificar(capture.Confirmado, "B: REPLY válido debe quedar confirmado.");
        Verificar(capture.TextoCitado == "Busco terreno para vivero",
            "B: debe aislar el texto citado.");
        Verificar(capture.TextoPropio == "Yo tengo uno en Mezquital",
            "B: debe aislar la oferta propia.");
        Verificar(!capture.TextoPropio.Contains("Busco terreno", StringComparison.Ordinal),
            "B: TextoPropio no debe contener la búsqueda citada.");
        Verificar(!capture.EsReenviado, "B: REPLY no debe marcar forwarded.");
    }

    private static async Task VerificarReplyNuevaBusquedaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco terreno para vivero") +
                "<span class='selectable-text'>También busco una casa en Jardines</span>"));

        Verificar(capture.TextoPropio == "También busco una casa en Jardines",
            "C: debe conservar únicamente la nueva búsqueda propia.");
        Verificar(capture.TextoCitado == "Busco terreno para vivero",
            "C: debe mantener la búsqueda anterior separada.");
    }

    private static async Task VerificarCitaSinTextoPropioAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(Quote("Busco terreno para vivero") +
                "<span class='selectable-text'>   </span>"));

        Verificar(capture.TieneCita, "D: debe reconocer la cita.");
        Verificar(capture.Confirmado, "D: una cita válida sin propio debe quedar confirmada.");
        Verificar(capture.TextoPropio.Length == 0,
            "D: no debe reconstruir TextoPropio usando la cita.");
    }

    private static async Task VerificarForwardedAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<div data-testid='forwarded-header'></div>" +
                "<span class='selectable-text'>Busco casa en venta</span>"));

        Verificar(capture.EsReenviado, "E: debe detectar forwarded estructuralmente.");
        Verificar(capture.Confirmado, "E: forwarded válido debe quedar confirmado.");
        Verificar(!capture.TieneCita, "E: forwarded no implica reply.");
        Verificar(capture.TextoPropio == "Busco casa en venta",
            "E: debe preservar el texto propio reenviado.");
    }

    private static async Task VerificarNormalBaseAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje("<span class='selectable-text'>Mensaje normal</span>"));

        Verificar(capture.MessageId == "MSG-SYNTHETIC-1", "F: debe leer data-id.");
        Verificar(capture.Confirmado, "F: el mensaje base debe quedar confirmado.");
        Verificar(capture.ChatOrigen == "CHAT-SINTETICO", "F: debe preservar ChatOrigen.");
        Verificar(capture.AutorActual == "Autor sintético", "F: debe preservar autor recibido.");
        Verificar(capture.TelefonoActual == "0000000000", "F: debe preservar teléfono recibido.");
        Verificar(capture.TimestampMensaje is null, "F: timestamp debe permanecer null.");
    }

    private static async Task VerificarPreciosSeparadosAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco casa de 1,200,000") +
                "<span class='selectable-text'>Yo tengo una de 1,800,000</span>"));

        Verificar(capture.TextoCitado == "Busco casa de 1,200,000",
            "G: debe conservar el precio citado por separado.");
        Verificar(capture.TextoPropio == "Yo tengo una de 1,800,000",
            "G: debe conservar el precio propio por separado.");
    }

    private static async Task VerificarMultiplesSelectableTextAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<div data-testid='quoted-message'>" +
                "<span class='selectable-text'>Cita uno</span>" +
                "<span class='selectable-text'>Cita dos</span></div>" +
                "<span class='selectable-text'>Propio uno</span>" +
                "<span class='selectable-text'>Propio dos</span>"));

        Verificar(capture.TextoCitado == "Cita uno\nCita dos",
            "H: debe agregar sólo fragmentos citados.");
        Verificar(capture.TextoPropio == "Propio uno\nPropio dos",
            "H: debe agregar sólo fragmentos propios por ascendencia.");
    }

    private static async Task VerificarDomInesperadoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            """
            <div data-testid="conv-msg-in" data-id="MSG-SYNTHETIC-1">
              <span class="selectable-text">Texto fuera de estructura confirmada</span>
            </div>
            """);

        Verificar(capture.TextoPropio.Length == 0,
            "I: DOM inesperado no debe inventar TextoPropio.");
        Verificar(capture.TextoCitado is null,
            "I: DOM inesperado no debe inventar TextoCitado.");
        VerificarNoConfirmado(capture, "MSG_CONTAINER_NOT_FOUND", "I: DOM inesperado");
    }

    private static async Task VerificarReplyForwardedAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<div data-testid='forwarded-header'></div>" +
                Quote("Búsqueda citada") +
                "<span class='selectable-text'>Respuesta propia</span>"));

        Verificar(capture.Confirmado && capture.TieneCita && capture.EsReenviado,
            "J: reply + forwarded debe conservar ambas señales.");
        Verificar(capture.TextoPropio == "Respuesta propia" &&
                  capture.TextoCitado == "Búsqueda citada",
            "J: reply + forwarded debe separar ambos textos.");
    }

    private static async Task VerificarQuoteMediaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<div data-testid='quoted-message'><div data-testid='media-placeholder'></div></div>" +
                "<span class='selectable-text'>Texto propio independiente</span>"));

        Verificar(capture.Confirmado && capture.TieneCita,
            "K: quote de media estructuralmente válido debe quedar confirmado.");
        Verificar(capture.TextoCitado is null &&
                  capture.TextoPropio == "Texto propio independiente",
            "K: quote de media no debe contaminar TextoPropio.");
    }

    private static async Task VerificarSelectableAnidadoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<span class='selectable-text'>Texto padre " +
                "<span class='selectable-text'>Texto hijo</span> final</span>"));

        Verificar(capture.Confirmado, "L: selectable anidado debe quedar confirmado.");
        Verificar(capture.TextoPropio == "Texto padre Texto hijo final",
            "L: selectable anidado no debe perder ni duplicar texto.");
    }

    private static async Task VerificarOcultoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                "<span class='selectable-text' style='display:none'>Oculto display</span>" +
                "<span class='selectable-text' style='visibility:hidden'>Oculto visibility</span>" +
                "<div style='opacity:0'><span class='selectable-text'>Oculto ancestor opacity</span></div>" +
                "<div aria-hidden='true'><span class='selectable-text'>Oculto aria</span></div>" +
                "<span class='selectable-text'>Visible</span>"));

        Verificar(capture.Confirmado && capture.TextoPropio == "Visible",
            "M: sólo debe conservar selectable-text visible.");
    }

    private static async Task VerificarWhitespaceUnicodeAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje("<span class='selectable-text'> \u200B\u200C\u200D\u2060\uFEFF </span>"));

        VerificarNoConfirmado(
            capture,
            "TEXT_STRUCTURE_AMBIGUOUS",
            "N: whitespace y caracteres invisibles");
    }

    private static async Task VerificarContenedorDuplicadoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            """
            <div data-testid="conv-msg-in" data-id="MSG-SYNTHETIC-1">
              <div data-testid="msg-container"><span class="selectable-text">Uno</span></div>
              <div data-testid="msg-container"><span class="selectable-text">Dos</span></div>
            </div>
            """);

        VerificarNoConfirmado(capture, "MSG_CONTAINER_MULTIPLE", "O: contenedor duplicado");
    }

    private static async Task VerificarQuoteDuplicadoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(Quote("Cita uno") + Quote("Cita dos") +
                "<span class='selectable-text'>Propio</span>"));

        VerificarNoConfirmado(capture, "QUOTE_MULTIPLE", "P: quote duplicado");
        Verificar(capture.TieneCita, "P: debe conservar la señal estructural de cita.");
    }

    private static async Task VerificarForwardedSinTextoAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje("<div data-testid='forwarded-header'></div>"));

        Verificar(capture.Confirmado && capture.EsReenviado && capture.TextoPropio.Length == 0,
            "Q: forwarded sin texto es estructura válida con TextoPropio vacío.");
    }

    private static async Task VerificarMessageIdAusenteAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            """
            <div data-testid="conv-msg-in">
              <div data-testid="msg-container">
                <span class="selectable-text">Texto válido</span>
              </div>
            </div>
            """);

        VerificarNoConfirmado(capture, "MESSAGE_ID_MISSING", "R: MessageId ausente");
    }

    private static async Task VerificarSelectableFueraAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            """
            <div data-testid="conv-msg-in" data-id="MSG-SYNTHETIC-1">
              <span class="selectable-text">Texto externo que debe ignorarse</span>
              <div data-testid="msg-container"><div data-testid="forwarded-header"></div></div>
            </div>
            """);

        Verificar(capture.Confirmado && capture.TextoPropio.Length == 0,
            "S: selectable fuera de msg-container debe ignorarse.");
    }

    private static async Task VerificarEstructuraAmbiguaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje("<div data-testid='elemento-desconocido'></div>"));

        VerificarNoConfirmado(capture, "TEXT_STRUCTURE_AMBIGUOUS", "T: estructura ambigua");
    }

    private static async Task VerificarPlaywrightTransitorioAsync(IBrowser browser)
    {
        IPage page = await browser.NewPageAsync();
        ILocator message = page.Locator(MessageSelector);
        await page.CloseAsync();

        RadarWhatsAppMessageCapture capture = await RadarWhatsAppMessageCaptureReader.ReadAsync(
            message,
            "CHAT-SINTETICO");

        VerificarNoConfirmado(capture, "PLAYWRIGHT_TRANSIENT", "U: página cerrada");
    }

    private static async Task VerificarReplyOfertaNoEsDemandaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco terreno para vivero") +
                "<span class='selectable-text'>Yo tengo uno en Mezquital</span>"));
        RadarWhatsAppCaptureDecision decision =
            RadarWhatsAppCaptureFlowDecision.Evaluar(capture);

        Verificar(capture.Confirmado && capture.TieneCita,
            "V: reply oferta debe ser una captura confirmada con cita.");
        Verificar(decision.Disposicion == RadarWhatsAppCaptureDisposition.Ignorar &&
                  decision.Clasificacion != RadarMessageClassification.Demanda,
            "V: la oferta propia no debe clasificarse como demanda por la cita.");
    }

    private static async Task VerificarReplyNuevaBusquedaEsDemandaAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco terreno para vivero") +
                "<span class='selectable-text'>También busco una casa en Jardines</span>"));
        RadarWhatsAppCaptureDecision decision =
            RadarWhatsAppCaptureFlowDecision.Evaluar(capture);

        Verificar(decision.Disposicion == RadarWhatsAppCaptureDisposition.Demanda &&
                  decision.Clasificacion == RadarMessageClassification.Demanda &&
                  decision.DebeCrearRadarMessage &&
                  decision.DebeInvocarIntelligence,
            "W: una búsqueda nueva en TextoPropio debe conservar el flujo de demanda.");
    }

    private static async Task VerificarPrecioCitadoNoLlegaAlClassifierAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            Mensaje(
                Quote("Busco casa de 1,200,000") +
                "<span class='selectable-text'>Yo tengo una de 1,800,000</span>"));
        RadarWhatsAppCaptureDecision decision =
            RadarWhatsAppCaptureFlowDecision.Evaluar(capture);

        Verificar(capture.TextoPropio == "Yo tengo una de 1,800,000" &&
                  !capture.TextoPropio.Contains("1,200,000", StringComparison.Ordinal),
            "X: el precio citado no debe entrar al texto accionable.");
        Verificar(decision.Disposicion == RadarWhatsAppCaptureDisposition.Ignorar,
            "X: el classifier debe decidir exclusivamente sobre la oferta propia.");
    }

    private static async Task VerificarCapturaNoConfirmadaConservaRetryAsync(IPage page)
    {
        RadarWhatsAppMessageCapture capture = await CapturarAsync(
            page,
            """
            <div data-testid="conv-msg-in" data-id="MSG-RETRY">
              <span class="selectable-text">Busco casa</span>
            </div>
            """);
        RadarWhatsAppCaptureDecision decision =
            RadarWhatsAppCaptureFlowDecision.Evaluar(capture);
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (decision.DebeMarcarComoConocido)
            knownIds.Add("MSG-RETRY");

        Verificar(decision.Disposicion == RadarWhatsAppCaptureDisposition.Retry,
            "Y: captura no confirmada debe conservar retry.");
        Verificar(!decision.DebeMarcarComoConocido &&
                  !decision.DebeCrearRadarMessage &&
                  !decision.DebeInvocarIntelligence &&
                  knownIds.Count == 0,
            "Y: retry no debe conocer ID, crear RadarMessage ni invocar Intelligence.");
    }

    private static async Task<RadarWhatsAppMessageCapture> CapturarAsync(
        IPage page,
        string html)
    {
        await page.SetContentAsync(html);
        ILocator message = page.Locator(MessageSelector);
        Verificar(await message.CountAsync() == 1,
            "El caso sintético debe contener exactamente un mensaje principal.");

        return await RadarWhatsAppMessageCaptureReader.ReadAsync(
            message,
            "CHAT-SINTETICO",
            "Autor sintético",
            "0000000000");
    }

    private static string Mensaje(string contenido) =>
        $"""
        <div data-testid="conv-msg-in" data-id="MSG-SYNTHETIC-1">
          <div data-testid="msg-container">
            {contenido}
            <div data-testid="msg-meta"></div>
          </div>
        </div>
        """;

    private static string Quote(string texto) =>
        $"<div data-testid='quoted-message'><span class='selectable-text'>{texto}</span></div>";

    private static void Verificar(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void VerificarNoConfirmado(
        RadarWhatsAppMessageCapture capture,
        string motivo,
        string contexto)
    {
        Verificar(!capture.Confirmado, $"{contexto}: no debe quedar confirmado.");
        Verificar(capture.MotivoNoConfirmado == motivo,
            $"{contexto}: motivo esperado {motivo}, actual {capture.MotivoNoConfirmado ?? "null"}.");
        Verificar(capture.TextoPropio.Length == 0 && capture.TextoCitado is null,
            $"{contexto}: una captura no confirmada no debe contener texto.");
    }
}
