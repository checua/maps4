using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace RSMaps.Radar.Listener.Services;

public enum RadarWhatsAppOperationalState
{
    Starting,
    LoggedOut,
    WaitingForReady,
    Ready,
    TransientFailure,
    FatalFailure
}

public sealed record RadarWhatsAppObservedSignals(
    string CurrentUrl,
    bool PageAccessible,
    bool HasChatList,
    bool HasGridRowsAndTitles,
    bool HasHistoryBoundary,
    bool KnownSyncBlockingSignal = false);

public sealed record RadarWhatsAppStateSnapshot(
    RadarWhatsAppOperationalState State,
    bool IsAuthenticated,
    bool HasFunctionalSidebar,
    bool HasHistoryBoundary,
    bool KnownSyncBlockingSignal,
    string CurrentUrl,
    string DiagnosticReason)
{
    public bool CanSweep => State == RadarWhatsAppOperationalState.Ready;
}

public sealed record RadarWhatsAppReadinessOptions(
    TimeSpan InitialBackoff,
    TimeSpan MaximumBackoff,
    TimeSpan StabilityInterval)
{
    public static RadarWhatsAppReadinessOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(1));
}

public static class RadarWhatsAppStateDetector
{
    private const string ChatListSelector = "[data-testid='chat-list']";
    private const string GridSelector = "[role='grid'][aria-rowcount]";
    private const string RowSelector = "[role='row']";
    private const string ChatTitleSelector = "[data-testid='cell-frame-title']";

    private static readonly Regex HistoryPhoneFragment = new(
        "Usa\\s+WhatsApp\\s+en\\s+tu\\s+tel[eé]fono",
        RegexOptions.IgnoreCase);

    private static readonly Regex HistoryPreviousMessagesFragment = new(
        "mensajes\\s+anteriores",
        RegexOptions.IgnoreCase);

    public static async Task<RadarWhatsAppStateSnapshot> DetectAsync(
        IPage? page,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (page is null || page.IsClosed)
        {
            return Classify(new RadarWhatsAppObservedSignals(
                page?.Url ?? string.Empty,
                PageAccessible: false,
                HasChatList: false,
                HasGridRowsAndTitles: false,
                HasHistoryBoundary: false));
        }

        try
        {
            string currentUrl = page.Url ?? string.Empty;
            bool hasChatList = await IsVisibleAsync(page.Locator(ChatListSelector).First);

            ILocator grid = page.Locator(GridSelector).First;
            bool hasGridRowsAndTitles = await IsVisibleAsync(grid)
                && await grid.Locator(RowSelector).CountAsync() > 0
                && await grid.Locator(ChatTitleSelector).CountAsync() > 0;

            // Esta detección es localizada y frágil; queda como información no bloqueante
            // hasta que WhatsApp exponga una señal estructural estable para este aviso.
            bool hasHistoryBoundary =
                await page.GetByText(HistoryPhoneFragment).CountAsync() > 0
                && await page.GetByText(HistoryPreviousMessagesFragment).CountAsync() > 0;

            cancellationToken.ThrowIfCancellationRequested();

            return Classify(new RadarWhatsAppObservedSignals(
                currentUrl,
                PageAccessible: true,
                hasChatList,
                hasGridRowsAndTitles,
                hasHistoryBoundary));
        }
        catch (PlaywrightException ex)
        {
            return new RadarWhatsAppStateSnapshot(
                RadarWhatsAppOperationalState.TransientFailure,
                IsAuthenticated: false,
                HasFunctionalSidebar: false,
                HasHistoryBoundary: false,
                KnownSyncBlockingSignal: false,
                page.Url ?? string.Empty,
                $"PLAYWRIGHT_TRANSIENT:{ex.GetType().Name}");
        }
    }

    public static RadarWhatsAppStateSnapshot Classify(RadarWhatsAppObservedSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (!signals.PageAccessible)
        {
            return Snapshot(
                RadarWhatsAppOperationalState.TransientFailure,
                signals,
                authenticated: false,
                functionalSidebar: false,
                "PAGE_NOT_ACCESSIBLE");
        }

        if (IsExplicitLogout(signals.CurrentUrl))
        {
            return Snapshot(
                RadarWhatsAppOperationalState.LoggedOut,
                signals,
                authenticated: false,
                functionalSidebar: false,
                "POST_LOGOUT_URL");
        }

        bool functionalSidebar = signals.HasChatList || signals.HasGridRowsAndTitles;

        // No existe todavía una señal productiva confirmada para SyncPending.
        // El campo sólo reserva la extensión: el detector DOM actual nunca lo activa.
        if (signals.KnownSyncBlockingSignal)
        {
            return Snapshot(
                RadarWhatsAppOperationalState.WaitingForReady,
                signals,
                authenticated: true,
                functionalSidebar,
                "KNOWN_SYNC_BLOCKING_SIGNAL");
        }

        return functionalSidebar
            ? Snapshot(
                RadarWhatsAppOperationalState.Ready,
                signals,
                authenticated: true,
                functionalSidebar: true,
                "FUNCTIONAL_SIDEBAR")
            : Snapshot(
                RadarWhatsAppOperationalState.WaitingForReady,
                signals,
                authenticated: false,
                functionalSidebar: false,
                "SIDEBAR_NOT_READY");
    }

    private static RadarWhatsAppStateSnapshot Snapshot(
        RadarWhatsAppOperationalState state,
        RadarWhatsAppObservedSignals signals,
        bool authenticated,
        bool functionalSidebar,
        string reason) =>
        new(
            state,
            authenticated,
            functionalSidebar,
            signals.HasHistoryBoundary,
            signals.KnownSyncBlockingSignal,
            signals.CurrentUrl,
            reason);

    private static bool IsExplicitLogout(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(
                    x.Split('=', 2)[0],
                    "post_logout",
                    StringComparison.OrdinalIgnoreCase)
                    && (x.Split('=', 2).Length == 1
                        || string.Equals(x.Split('=', 2)[1], "1", StringComparison.OrdinalIgnoreCase)));
        }

        return url.Contains("post_logout=1", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsVisibleAsync(ILocator locator) =>
        await locator.CountAsync() > 0 && await locator.IsVisibleAsync();
}

public static class RadarWhatsAppReadinessGate
{
    public static async Task<RadarWhatsAppStateSnapshot> WaitUntilReadyAsync(
        Func<CancellationToken, Task<RadarWhatsAppStateSnapshot>> observeAsync,
        RadarWhatsAppReadinessOptions? options = null,
        Action<RadarWhatsAppStateSnapshot>? onObservation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observeAsync);
        options ??= RadarWhatsAppReadinessOptions.Default;

        if (options.InitialBackoff <= TimeSpan.Zero ||
            options.MaximumBackoff < options.InitialBackoff ||
            options.StabilityInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        int consecutiveReady = 0;
        TimeSpan backoff = options.InitialBackoff;
        RadarWhatsAppOperationalState? previousState = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RadarWhatsAppStateSnapshot snapshot = await observeAsync(cancellationToken);
            onObservation?.Invoke(snapshot);

            if (snapshot.CanSweep)
            {
                consecutiveReady++;
                if (consecutiveReady >= 2)
                    return snapshot;

                previousState = snapshot.State;
                await Task.Delay(options.StabilityInterval, cancellationToken);
                continue;
            }

            consecutiveReady = 0;
            if (previousState != snapshot.State)
                backoff = options.InitialBackoff;

            previousState = snapshot.State;
            await Task.Delay(backoff, cancellationToken);

            double nextMilliseconds = Math.Min(
                options.MaximumBackoff.TotalMilliseconds,
                backoff.TotalMilliseconds * 2d);
            backoff = TimeSpan.FromMilliseconds(nextMilliseconds);
        }
    }
}
