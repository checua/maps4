using Microsoft.Playwright;

namespace RSMaps.Radar.Listener.Services;

public static class RadarWhatsAppSession
{
    private const string WhatsAppUrl = "https://web.whatsapp.com";
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(10);
    private static readonly object DiagnosticLock = new();

    private static RadarWhatsAppOperationalState? _lastState;
    private static DateTime _lastReminderUtc = DateTime.MinValue;
    private static bool _startingLogged;
    private static bool _readyAnnounced;
    private static bool _recoveryObserved;
    private static bool _historyBoundaryPresent;

    public static async Task<IPage> WaitUntilReadyAsync(
        IBrowserContext context,
        IPage? actual = null,
        bool mostrarRecuperacion = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        LogStartingOnce();

        IPage? page = actual;
        RadarWhatsAppStateSnapshot ready =
            await RadarWhatsAppReadinessGate.WaitUntilReadyAsync(
                async token =>
                {
                    try
                    {
                        page = await EnsureWhatsAppPageAsync(context, page, token);
                        return await RadarWhatsAppStateDetector.DetectAsync(page, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (PlaywrightException ex)
                    {
                        page = null;
                        return new RadarWhatsAppStateSnapshot(
                            RadarWhatsAppOperationalState.TransientFailure,
                            IsAuthenticated: false,
                            HasFunctionalSidebar: false,
                            HasHistoryBoundary: false,
                            KnownSyncBlockingSignal: false,
                            CurrentUrl: string.Empty,
                            DiagnosticReason: $"PLAYWRIGHT_TRANSIENT:{ex.GetType().Name}");
                    }
                },
                onObservation: ObserveState,
                cancellationToken: cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        AnnounceReady(ready, mostrarRecuperacion);
        await CloseDuplicateWhatsAppPagesAsync(context, page!, cancellationToken);
        return page!;
    }

    private static async Task<IPage> EnsureWhatsAppPageAsync(
        IBrowserContext context,
        IPage? actual,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<IPage> openPages = context.Pages.Where(x => !x.IsClosed).ToList();
        IPage? page = actual is not null && !actual.IsClosed && IsWhatsApp(actual.Url)
            ? actual
            : openPages.FirstOrDefault(x => IsWhatsApp(x.Url));

        page ??= openPages.FirstOrDefault();
        page ??= await context.NewPageAsync();

        if (!IsWhatsApp(page.Url))
        {
            await page.GotoAsync(
                WhatsAppUrl,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        cancellationToken.ThrowIfCancellationRequested();
        return page;
    }

    private static async Task CloseDuplicateWhatsAppPagesAsync(
        IBrowserContext context,
        IPage activePage,
        CancellationToken cancellationToken)
    {
        foreach (IPage duplicate in context.Pages
                     .Where(x => !x.IsClosed && !ReferenceEquals(x, activePage) && IsWhatsApp(x.Url))
                     .ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await duplicate.CloseAsync();
            }
            catch (PlaywrightException)
            {
                // Una pestaña que WhatsApp ya cerró no afecta la sesión activa.
            }
        }
    }

    private static void ObserveState(RadarWhatsAppStateSnapshot snapshot)
    {
        lock (DiagnosticLock)
        {
            DateTime now = DateTime.UtcNow;
            bool changed = _lastState != snapshot.State;
            bool reminderDue = now - _lastReminderUtc >= ReminderInterval;

            if (snapshot.HasHistoryBoundary && !_historyBoundaryPresent)
                Console.WriteLine("[WHATSAPP_HISTORY_BOUNDARY_DETECTED] El historial anterior es parcial; los mensajes disponibles continúan operativos.");

            _historyBoundaryPresent = snapshot.HasHistoryBoundary;

            if (snapshot.State is RadarWhatsAppOperationalState.LoggedOut or
                RadarWhatsAppOperationalState.TransientFailure)
            {
                _recoveryObserved = true;
            }

            if (snapshot.State != RadarWhatsAppOperationalState.Ready && _readyAnnounced)
            {
                _readyAnnounced = false;
                _recoveryObserved = true;
            }

            if (changed)
            {
                switch (snapshot.State)
                {
                    case RadarWhatsAppOperationalState.LoggedOut:
                        Console.WriteLine("[WHATSAPP_LOGGED_OUT] La sesión requiere vinculación manual; RADAR permanece vivo.");
                        Console.WriteLine("[WHATSAPP_WAITING_FOR_LINK] Esperando autenticación sin iniciar barrido.");
                        break;
                    case RadarWhatsAppOperationalState.WaitingForReady:
                        Console.WriteLine("[WHATSAPP_WAITING_FOR_READY] WhatsApp aún no presenta un sidebar funcional; barrido suspendido.");
                        break;
                    case RadarWhatsAppOperationalState.Ready:
                        Console.WriteLine("[WHATSAPP_AUTHENTICATED] Sidebar funcional detectado; verificando estabilidad.");
                        break;
                    case RadarWhatsAppOperationalState.TransientFailure:
                        Console.WriteLine($"[WHATSAPP_TRANSIENT_FAILURE] {snapshot.DiagnosticReason}; se reintentará con backoff.");
                        break;
                    case RadarWhatsAppOperationalState.FatalFailure:
                        Console.WriteLine($"[WHATSAPP_FATAL] {snapshot.DiagnosticReason}");
                        break;
                }

                _lastReminderUtc = now;
            }
            else if (reminderDue && snapshot.State is (
                     RadarWhatsAppOperationalState.LoggedOut or
                     RadarWhatsAppOperationalState.WaitingForReady or
                     RadarWhatsAppOperationalState.TransientFailure))
            {
                Console.WriteLine($"[WHATSAPP_WAITING] Estado={snapshot.State}; RADAR sigue vivo y el barrido continúa suspendido.");
                _lastReminderUtc = now;
            }

            _lastState = snapshot.State;
        }
    }

    private static void LogStartingOnce()
    {
        lock (DiagnosticLock)
        {
            if (_startingLogged)
                return;

            Console.WriteLine("[WHATSAPP_STARTING] Preparando sesión persistente de WhatsApp Web.");
            _startingLogged = true;
        }
    }

    private static void AnnounceReady(
        RadarWhatsAppStateSnapshot snapshot,
        bool mostrarRecuperacion)
    {
        lock (DiagnosticLock)
        {
            if (!_readyAnnounced)
            {
                Console.WriteLine("[WHATSAPP_READY] Sidebar estable; barrido habilitado.");
                _readyAnnounced = true;
            }

            if (_recoveryObserved || mostrarRecuperacion)
            {
                Console.WriteLine("[WHATSAPP_RECOVERED] La sesión volvió a Ready sin reiniciar el proceso.");
                _recoveryObserved = false;
            }

            _lastState = snapshot.State;
        }
    }

    private static bool IsWhatsApp(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith(WhatsAppUrl, StringComparison.OrdinalIgnoreCase);
}
