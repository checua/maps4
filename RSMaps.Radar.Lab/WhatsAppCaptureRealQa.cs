using Microsoft.Playwright;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class WhatsAppCaptureRealQa
{
    private const string ConversationHeaderSelector =
        "[data-testid='conversation-info-header-chat-title']";
    private const string MessageSelector = "[data-testid^='conv-msg-'][data-id]";
    private static readonly TimeSpan DefaultReadinessTimeout = TimeSpan.FromMinutes(10);

    public static async Task RunAsync()
    {
        string expectedProfile = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSMaps",
            "RadarAgent",
            "DomLab",
            "20260918-091715",
            "WhatsAppProfile"));
        ValidarPerfil(expectedProfile);

        TimeSpan readinessTimeout = ObtenerReadinessTimeout();
        using var cancellation = new CancellationTokenSource(readinessTimeout);
        ConsoleCancelEventHandler cancelHandler = (_, args) =>
        {
            args.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowserContext browser = await playwright.Chromium.LaunchPersistentContextAsync(
                expectedProfile,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    ViewportSize = null
                });

            IPage page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();
            await page.GotoAsync(
                "https://web.whatsapp.com/",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            Console.WriteLine("WHATSAPP_CAPTURE_REAL_QA");
            Console.WriteLine($"TIMEOUT_MINUTES = {readinessTimeout.TotalMinutes:0}");
            Console.WriteLine("Abra manualmente el chat de prueba. No se seleccionará ningún chat automáticamente.");

            bool ready = await EsperarReadinessAsync(page, cancellation.Token);
            if (!ready)
            {
                Console.WriteLine("QA cancelado o agotó el tiempo sin capturar mensajes.");
                return;
            }

            ILocator messages = page.Locator(MessageSelector).Filter(new LocatorFilterOptions { Visible = true });
            int count = await messages.CountAsync();
            Console.WriteLine("Mensajes visibles disponibles:");
            for (int index = 1; index <= count; index++)
                Console.WriteLine($"[{index}]");

            (int normal, int reply, int forwarded, bool nonInteractive) =
                await ObtenerIndicesAsync(count, cancellation.Token);
            if (normal == reply || normal == forwarded || reply == forwarded)
                throw new InvalidOperationException("Los tres índices deben ser distintos.");

            Console.WriteLine($"INDICES = NORMAL:{normal} REPLY:{reply} FORWARDED:{forwarded}");

            await EjecutarCasoAsync("NORMAL", messages.Nth(normal - 1));
            await EjecutarCasoAsync("REPLY", messages.Nth(reply - 1), verificarReply: true);
            await EjecutarCasoAsync("FORWARDED", messages.Nth(forwarded - 1));

            if (nonInteractive)
            {
                Console.WriteLine("QA completado. Cierre automático del Lab no interactivo.");
            }
            else
            {
                Console.WriteLine("QA completado. Presione ENTER para cerrar el Lab.");
                await Console.In.ReadLineAsync(cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("QA cancelado limpiamente.");
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static TimeSpan ObtenerReadinessTimeout()
    {
        string? configured = Environment.GetEnvironmentVariable("RADAR_DOM_LAB_TIMEOUT_MINUTES");
        if (string.IsNullOrWhiteSpace(configured))
            return DefaultReadinessTimeout;

        if (!int.TryParse(configured, out int minutes) || minutes is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "RADAR_DOM_LAB_TIMEOUT_MINUTES debe ser un entero entre 1 y 120.");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static void ValidarPerfil(string profile)
    {
        string domLabRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSMaps",
            "RadarAgent",
            "DomLab")) + Path.DirectorySeparatorChar;

        if (!profile.StartsWith(domLabRoot, StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("AgentProduccion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El perfil de QA no pertenece exclusivamente a DomLab.");
        }

        if (!Directory.Exists(profile))
            throw new DirectoryNotFoundException("No existe el perfil DomLab esperado.");
    }

    private static async Task<bool> EsperarReadinessAsync(IPage page, CancellationToken cancellationToken)
    {
        int stableObservations = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            bool conversationOpen = await page.Locator(ConversationHeaderSelector).IsVisibleAsync();
            int materializedMessages = await page.Locator(MessageSelector)
                .Filter(new LocatorFilterOptions { Visible = true })
                .CountAsync();

            stableObservations = conversationOpen && materializedMessages >= 3
                ? stableObservations + 1
                : 0;
            bool stable = stableObservations >= 2;

            Console.WriteLine($"CONVERSACION_ABIERTA = {(conversationOpen ? "SI" : "NO")}");
            Console.WriteLine($"MENSAJES_MATERIALIZADOS = {materializedMessages}");
            Console.WriteLine($"ESTABLE = {(stable ? "SI" : "NO")}");

            if (stable)
                return true;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return false;
    }

    private static async Task<int> LeerIndiceAsync(
        string label,
        int max,
        CancellationToken cancellationToken)
    {
        Console.Write($"{label} = ? ");
        string? value = await Console.In.ReadLineAsync(cancellationToken);
        if (!int.TryParse(value, out int index) || index < 1 || index > max)
            throw new InvalidOperationException($"Índice inválido para {label}.");
        return index;
    }

    private static async Task<(int Normal, int Reply, int Forwarded, bool NonInteractive)>
        ObtenerIndicesAsync(int max, CancellationToken cancellationToken)
    {
        string? normal = Environment.GetEnvironmentVariable("RADAR_QA_NORMAL_INDEX");
        string? reply = Environment.GetEnvironmentVariable("RADAR_QA_REPLY_INDEX");
        string? forwarded = Environment.GetEnvironmentVariable("RADAR_QA_FORWARDED_INDEX");
        bool anyConfigured =
            !string.IsNullOrWhiteSpace(normal) ||
            !string.IsNullOrWhiteSpace(reply) ||
            !string.IsNullOrWhiteSpace(forwarded);

        if (!anyConfigured)
        {
            return (
                await LeerIndiceAsync("NORMAL", max, cancellationToken),
                await LeerIndiceAsync("REPLY", max, cancellationToken),
                await LeerIndiceAsync("FORWARDED", max, cancellationToken),
                false);
        }

        if (string.IsNullOrWhiteSpace(normal) ||
            string.IsNullOrWhiteSpace(reply) ||
            string.IsNullOrWhiteSpace(forwarded))
        {
            throw new InvalidOperationException("Los tres índices QA deben configurarse juntos.");
        }

        return (
            LeerIndiceConfigurado("RADAR_QA_NORMAL_INDEX", normal, max),
            LeerIndiceConfigurado("RADAR_QA_REPLY_INDEX", reply, max),
            LeerIndiceConfigurado("RADAR_QA_FORWARDED_INDEX", forwarded, max),
            true);
    }

    private static int LeerIndiceConfigurado(string name, string value, int max)
    {
        if (!int.TryParse(value, out int index) || index < 1 || index > max)
            throw new InvalidOperationException($"{name} debe estar entre 1 y {max}.");

        return index;
    }

    private static async Task EjecutarCasoAsync(
        string label,
        ILocator message,
        bool verificarReply = false)
    {
        RadarWhatsAppMessageCapture capture =
            await RadarWhatsAppMessageCaptureReader.ReadAsync(message, "QA-DOM-LAB");

        RadarWhatsAppCaptureDecision decision = RadarWhatsAppCaptureFlowDecision.Evaluar(capture);
        RadarMessageClassification? directClassification = capture.Confirmado
            ? RadarMessageClassifier.Clasificar(capture.TextoPropio ?? "")
            : null;

        if (directClassification != decision.Clasificacion)
            throw new InvalidOperationException("La clasificación directa y FlowDecision no coinciden.");

        Console.WriteLine(label);
        Console.WriteLine($"Confirmado = {Mostrar(capture.Confirmado)}");
        Console.WriteLine($"MotivoNoConfirmado = {capture.MotivoNoConfirmado ?? "-"}");
        Console.WriteLine($"TieneCita = {Mostrar(capture.TieneCita)}");
        Console.WriteLine($"EsReenviado = {Mostrar(capture.EsReenviado)}");
        Console.WriteLine($"TextoPropioPresente = {Mostrar(!string.IsNullOrWhiteSpace(capture.TextoPropio))}");
        Console.WriteLine($"TextoPropioLongitud = {capture.TextoPropio?.Length ?? 0}");
        Console.WriteLine($"TextoCitadoPresente = {Mostrar(!string.IsNullOrWhiteSpace(capture.TextoCitado))}");
        Console.WriteLine($"TextoCitadoLongitud = {capture.TextoCitado?.Length ?? 0}");
        Console.WriteLine($"Clasificacion = {MostrarClasificacion(directClassification)}");

        if (verificarReply)
        {
            Console.WriteLine($"RegionesPropiaYCitadaDistintas = {Mostrar(
                capture.TieneCita &&
                !string.IsNullOrWhiteSpace(capture.TextoPropio) &&
                !string.IsNullOrWhiteSpace(capture.TextoCitado))}");
            Console.WriteLine("ClassifierInput = SOLO_TEXTO_PROPIO");
            Console.WriteLine("TextoCitadoFueArgumento = NO");
            Console.WriteLine("TextoCitadoConcatenado = NO");
            Console.WriteLine($"FlowDecisionClasificoSoloConfirmado = {Mostrar(capture.Confirmado && decision.Clasificacion is not null)}");
        }
    }

    private static string Mostrar(bool value) => value ? "SI" : "NO";

    private static string MostrarClasificacion(RadarMessageClassification? classification) =>
        classification switch
        {
            RadarMessageClassification.Demanda => "DEMANDA",
            RadarMessageClassification.Oferta or RadarMessageClassification.Otro => "NO_DEMANDA",
            null => "NO_EJECUTADA",
            _ => "NO_EJECUTADA"
        };
}
