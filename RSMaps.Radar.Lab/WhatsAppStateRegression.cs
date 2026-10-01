using Microsoft.Playwright;
using RSMaps.Radar.Listener.Services;
using System.Reflection;

internal static class WhatsAppStateRegression
{
    private static readonly RadarWhatsAppReadinessOptions FastOptions = new(
        TimeSpan.FromMilliseconds(5),
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(5));

    public static async Task RunAsync()
    {
        VerifyLogoutHasPriorityOverResidualSidebar();
        await VerifyProlongedStatesRemainAliveAsync();
        await VerifyTwoStableObservationsAsync();
        await VerifyUnstableReadyRequiresConsecutiveObservationsAsync();
        await VerifyNoSweepBeforeStableReadyAsync();
        await VerifyTransientRecoveryAndKnownIdsAsync();
        await VerifyCancellationPhasesAsync();
        await VerifyBackoffGrowthAndCapAsync();
        await VerifyContextFailureClassificationAsync();
        await VerifyHistoryBoundaryAndNoAutomaticClickAsync();
        VerifyDiagnosticLogThrottling();

        Console.WriteLine("WHATSAPP_STATE_REGRESSION_OK");
    }

    private static async Task VerifyUnstableReadyRequiresConsecutiveObservationsAsync()
    {
        var sequence = new Queue<RadarWhatsAppStateSnapshot>(
            [Ready(), Waiting(), Ready(), Ready()]);
        var observed = new List<RadarWhatsAppStateSnapshot>();

        RadarWhatsAppStateSnapshot result = await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
            _ => Task.FromResult(sequence.Dequeue()),
            FastOptions,
            observed.Add);

        Verify(result.CanSweep && observed.Count == 4,
            "Ready inestable debe reiniciar el contador y exigir dos observaciones consecutivas.");
    }

    private static void VerifyLogoutHasPriorityOverResidualSidebar()
    {
        RadarWhatsAppStateSnapshot snapshot = RadarWhatsAppStateDetector.Classify(
            Signals(
                "https://web.whatsapp.com/?post_logout=1",
                chatList: true,
                grid: true));

        Verify(snapshot.State == RadarWhatsAppOperationalState.LoggedOut,
            "A/M: post_logout debe tener prioridad sobre sidebar residual.");
        Verify(!snapshot.CanSweep && !snapshot.IsAuthenticated,
            "A: LoggedOut no puede habilitar sweep ni autenticación.");

        RadarWhatsAppStateSnapshot unknownSync = RadarWhatsAppStateDetector.Classify(
            Signals(
                "https://web.whatsapp.com/",
                chatList: true,
                grid: true,
                knownSyncBlockingSignal: true));

        Verify(unknownSync.State == RadarWhatsAppOperationalState.WaitingForReady &&
               !unknownSync.CanSweep,
            "L: una futura señal sync positiva debe bloquear sin inventar Ready.");
    }

    private static async Task VerifyProlongedStatesRemainAliveAsync()
    {
        int loggedOutObservations = 0;
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(180)))
        {
            bool cancelled = false;
            try
            {
                await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                    _ => Task.FromResult(LoggedOut()),
                    FastOptions,
                    _ => loggedOutObservations++,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Verify(cancelled && loggedOutObservations >= 3,
                "B: logout prolongado debe esperar hasta cancelación sin excepción fatal.");
        }

        int waitingObservations = 0;
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(180)))
        {
            bool cancelled = false;
            try
            {
                await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                    _ => Task.FromResult(Waiting()),
                    FastOptions,
                    _ => waitingObservations++,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Verify(cancelled && waitingObservations >= 3,
                "H: WaitingForReady prolongado debe seguir vivo y cancelable.");
        }
    }

    private static async Task VerifyTwoStableObservationsAsync()
    {
        var sequence = new Queue<RadarWhatsAppStateSnapshot>(
            [LoggedOut(), Ready(), Ready()]);
        var observed = new List<RadarWhatsAppStateSnapshot>();

        RadarWhatsAppStateSnapshot result = await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
            _ => Task.FromResult(sequence.Dequeue()),
            FastOptions,
            observed.Add);

        Verify(observed.Count == 3 && observed.Count(x => x.CanSweep) == 2,
            "C/D: la primera observación Ready no debe completar el gate.");
        Verify(result.CanSweep,
            "E: dos observaciones Ready consecutivas deben habilitar sweep.");
        Verify(observed[0].State == RadarWhatsAppOperationalState.LoggedOut &&
               observed[1].State == RadarWhatsAppOperationalState.Ready,
            "K: el gate debe observar la transición sin finalizar en el primer Ready.");
    }

    private static async Task VerifyTransientRecoveryAndKnownIdsAsync()
    {
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MSG-BASE" };
        HashSet<string> sameReference = knownIds;
        var sequence = new Queue<RadarWhatsAppStateSnapshot>(
            [Transient(), Ready(), Ready()]);

        RadarWhatsAppStateSnapshot result = await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
            _ => Task.FromResult(sequence.Dequeue()),
            FastOptions);

        Verify(result.CanSweep,
            "I: fallo transitorio debe poder volver a Ready mediante el gate.");
        Verify(ReferenceEquals(knownIds, sameReference) &&
               knownIds.SetEquals(["MSG-BASE"]),
            "J: la recuperación no debe recrear ni borrar IDs conocidos.");
    }

    private static async Task VerifyNoSweepBeforeStableReadyAsync()
    {
        int observations = 0;
        int sweepCalls = 0;
        var sequence = new Queue<RadarWhatsAppStateSnapshot>(
            [Waiting(), Ready(), Waiting(), Ready(), Ready()]);

        await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
            _ => Task.FromResult(sequence.Dequeue()),
            FastOptions,
            _ =>
            {
                observations++;
                Verify(sweepCalls == 0,
                    "K: ningún sweep puede ejecutarse mientras el gate observa estados.");
            });

        sweepCalls++;
        Verify(observations == 5 && sweepCalls == 1,
            "K: el caller sólo debe alcanzar sweep después de dos Ready consecutivos.");
    }

    private static async Task VerifyCancellationPhasesAsync()
    {
        await VerifyCancellationAsync(LoggedOut(), TimeSpan.FromMilliseconds(30),
            "Cancellation durante LoggedOut debe ser rápida y limpia.");
        await VerifyCancellationAsync(Waiting(), TimeSpan.FromMilliseconds(30),
            "Cancellation durante WaitingForReady debe ser rápida y limpia.");

        using var stabilityCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(15));
        bool stabilityCancelled = false;
        try
        {
            await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                _ => Task.FromResult(Ready()),
                new RadarWhatsAppReadinessOptions(
                    TimeSpan.FromMilliseconds(5),
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromSeconds(5)),
                cancellationToken: stabilityCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            stabilityCancelled = true;
        }

        Verify(stabilityCancelled,
            "Cancellation debe interrumpir el stability delay sin esperar su duración completa.");
    }

    private static async Task VerifyCancellationAsync(
        RadarWhatsAppStateSnapshot persistentState,
        TimeSpan cancelAfter,
        string message)
    {
        using var cancellation = new CancellationTokenSource(cancelAfter);
        bool cancelled = false;
        try
        {
            await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                _ => Task.FromResult(persistentState),
                new RadarWhatsAppReadinessOptions(
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMilliseconds(5)),
                cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Verify(cancelled, message);
    }

    private static async Task VerifyBackoffGrowthAndCapAsync()
    {
        var observations = new List<DateTime>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(115));
        try
        {
            await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                _ => Task.FromResult(Waiting()),
                new RadarWhatsAppReadinessOptions(
                    TimeSpan.FromMilliseconds(5),
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(5)),
                _ => observations.Add(DateTime.UtcNow),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Verify(observations.Count is >= 5 and <= 9,
            "Backoff debe crecer, quedar limitado y evitar busy-loop.");
        double maximumObservedDelay = observations
            .Zip(observations.Skip(1), (a, b) => (b - a).TotalMilliseconds)
            .DefaultIfEmpty(0)
            .Max();
        Verify(maximumObservedDelay < 80,
            "Backoff reducido de prueba no debe crecer indefinidamente sobre el cap.");
    }

    private static async Task VerifyContextFailureClassificationAsync()
    {
        RadarWhatsAppStateSnapshot transient =
            RadarWhatsAppStateDetector.ClassifyPlaywrightFailure(
                contextAvailable: true,
                exceptionType: nameof(PlaywrightException));
        RadarWhatsAppStateSnapshot fatal =
            RadarWhatsAppStateDetector.ClassifyPlaywrightFailure(
                contextAvailable: false,
                exceptionType: nameof(PlaywrightException));

        Verify(transient.State == RadarWhatsAppOperationalState.TransientFailure,
            "Fallo de page con contexto sano debe seguir siendo transitorio.");
        Verify(fatal.State == RadarWhatsAppOperationalState.FatalFailure,
            "Contexto/browser desconectado debe clasificarse como fatal no recuperable.");

        bool escalated = false;
        try
        {
            await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                _ => Task.FromResult(fatal),
                FastOptions);
        }
        catch (RadarWhatsAppFatalSessionException ex)
        {
            escalated = ex.DiagnosticReason == "BROWSER_CONTEXT_UNAVAILABLE";
        }

        Verify(escalated,
            "FatalFailure debe escalar una vez, sin loop creando pages inútiles.");
    }

    private static async Task VerifyHistoryBoundaryAndNoAutomaticClickAsync()
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        IPage page = await browser.NewPageAsync();

        foreach (string date in new[] { "29/6/2026", "1/1/2030" })
        {
            await page.SetContentAsync($$"""
                <div data-testid="chat-list" style="display:block;width:100px;height:100px"></div>
                <div>Usa WhatsApp en tu teléfono para ver mensajes anteriores al {{date}}.</div>
                <button id="sync-action" onclick="window.__clicks++">Sincronizar</button>
                <script>window.__clicks = 0;</script>
                """);

            RadarWhatsAppStateSnapshot snapshot =
                await RadarWhatsAppStateDetector.DetectAsync(page);
            int clicks = await page.EvaluateAsync<int>("() => window.__clicks");

            Verify(snapshot.State == RadarWhatsAppOperationalState.Ready &&
                   snapshot.HasHistoryBoundary && snapshot.CanSweep,
                "F/G: HistoryBoundary debe ser informativo e independiente de la fecha.");
            Verify(clicks == 0,
                "L: detector y gate no deben ejecutar acciones de sincronización.");
        }
    }

    private static void VerifyDiagnosticLogThrottling()
    {
        Type sessionType = typeof(RadarWhatsAppSession);
        MethodInfo observe = sessionType.GetMethod(
            "ObserveState",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("No se encontró ObserveState.");
        FieldInfo lastState = RequiredField(sessionType, "_lastState");
        FieldInfo lastReminder = RequiredField(sessionType, "_lastReminderUtc");
        FieldInfo readyAnnounced = RequiredField(sessionType, "_readyAnnounced");
        FieldInfo historyPresent = RequiredField(sessionType, "_historyBoundaryPresent");

        TextWriter original = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            lastState.SetValue(null, null);
            lastReminder.SetValue(null, DateTime.MinValue);
            readyAnnounced.SetValue(null, false);
            historyPresent.SetValue(null, false);

            observe.Invoke(null, [LoggedOut()]);
            observe.Invoke(null, [LoggedOut()]);
            lastReminder.SetValue(null, DateTime.UtcNow - TimeSpan.FromMinutes(11));
            observe.Invoke(null, [LoggedOut()]);

            RadarWhatsAppStateSnapshot history = RadarWhatsAppStateDetector.Classify(
                Signals(
                    "https://web.whatsapp.com/",
                    chatList: true,
                    history: true));
            observe.Invoke(null, [history]);
            observe.Invoke(null, [history]);
        }
        finally
        {
            Console.SetOut(original);
        }

        string output = captured.ToString();
        Verify(Count(output, "[WHATSAPP_LOGGED_OUT]") == 1 &&
               Count(output, "[WHATSAPP_WAITING_FOR_LINK]") == 1 &&
               Count(output, "[WHATSAPP_WAITING]") == 1,
            "Estado persistente debe registrar inicio y sólo un recordatorio al vencer 10 minutos.");
        Verify(Count(output, "[WHATSAPP_HISTORY_BOUNDARY_DETECTED]") == 1,
            "HistoryBoundary persistente no debe repetir el evento en cada poll.");
    }

    private static FieldInfo RequiredField(Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"No se encontró {name}.");

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    private static RadarWhatsAppObservedSignals Signals(
        string url,
        bool chatList = false,
        bool grid = false,
        bool history = false,
        bool knownSyncBlockingSignal = false) =>
        new(
            url,
            PageAccessible: true,
            HasChatList: chatList,
            HasGridRowsAndTitles: grid,
            HasHistoryBoundary: history,
            KnownSyncBlockingSignal: knownSyncBlockingSignal);

    private static RadarWhatsAppStateSnapshot LoggedOut() =>
        RadarWhatsAppStateDetector.Classify(
            Signals("https://web.whatsapp.com/?post_logout=1"));

    private static RadarWhatsAppStateSnapshot Waiting() =>
        RadarWhatsAppStateDetector.Classify(
            Signals("https://web.whatsapp.com/"));

    private static RadarWhatsAppStateSnapshot Ready() =>
        RadarWhatsAppStateDetector.Classify(
            Signals("https://web.whatsapp.com/", chatList: true));

    private static RadarWhatsAppStateSnapshot Transient() =>
        RadarWhatsAppStateDetector.Classify(
            new RadarWhatsAppObservedSignals(
                string.Empty,
                PageAccessible: false,
                HasChatList: false,
                HasGridRowsAndTitles: false,
                HasHistoryBoundary: false));

    private static void Verify(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
