using RSMaps.Radar.Listener.Models;
using System.Reflection;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarAgentRuntimeHealth
{
    private readonly object _sync = new();
    private readonly Guid _instanceId;
    private readonly DateTime _instanceStartedUtc;
    private readonly string _version;
    private string _listenerState = "Starting";
    private string _whatsAppState = "Starting";
    private DateTime? _whatsAppStateSinceUtc;
    private DateTime? _lastSweepStartedUtc;
    private DateTime? _lastSweepCompletedUtc;
    private DateTime? _activeSweepStartedUtc;
    private int _activeSweepChatsConfigured;
    private int _chatsConfigured;
    private int _chatsReviewed;
    private string _centralState = "NotRecentlyUsed";
    private DateTime? _lastCentralSuccessUtc;
    private string? _centralErrorCode;
    private string? _reporterErrorCode;

    public RadarAgentRuntimeHealth(
        Guid? instanceId = null,
        DateTime? instanceStartedUtc = null,
        string? version = null)
    {
        _instanceId = instanceId ?? Guid.NewGuid();
        _instanceStartedUtc = EnsureUtc(instanceStartedUtc ?? DateTime.UtcNow);
        _version = NormalizeVersion(version ?? ReadVersion());
        _whatsAppStateSinceUtc = _instanceStartedUtc;
    }

    public static RadarAgentRuntimeHealth Current { get; } = new();

    public void SetListenerState(string state)
    {
        lock (_sync)
            _listenerState = state;
    }

    public void SetWhatsAppState(string state, DateTime? observedUtc = null)
    {
        DateTime now = EnsureUtc(observedUtc ?? DateTime.UtcNow);
        lock (_sync)
        {
            if (!string.Equals(_whatsAppState, state, StringComparison.Ordinal))
                _whatsAppStateSinceUtc = now;
            _whatsAppState = state;
        }
    }

    public void StartSweep(int chatsConfigured, DateTime? startedUtc = null)
    {
        lock (_sync)
        {
            // Do not overwrite the last completed sweep: a heartbeat may arrive mid-sweep.
            _activeSweepStartedUtc = EnsureUtc(startedUtc ?? DateTime.UtcNow);
            _activeSweepChatsConfigured = Math.Max(0, chatsConfigured);
        }
    }

    public void CompleteSweep(int chatsReviewed, DateTime? completedUtc = null)
    {
        lock (_sync)
        {
            if (!_activeSweepStartedUtc.HasValue)
                return;

            DateTime completed = EnsureUtc(completedUtc ?? DateTime.UtcNow);
            _lastSweepStartedUtc = _activeSweepStartedUtc.Value;
            _lastSweepCompletedUtc = completed < _lastSweepStartedUtc.Value
                ? _lastSweepStartedUtc.Value
                : completed;
            _chatsConfigured = _activeSweepChatsConfigured;
            _chatsReviewed = Math.Clamp(chatsReviewed, 0, _chatsConfigured);
            _activeSweepStartedUtc = null;
        }
    }

    public void SetCentralState(
        string state,
        string? errorCode = null,
        DateTime? successfulUtc = null)
    {
        lock (_sync)
        {
            _centralState = state;
            _centralErrorCode = NormalizeErrorCode(errorCode);
            if (successfulUtc.HasValue)
                _lastCentralSuccessUtc = EnsureUtc(successfulUtc.Value);
        }
    }

    public void SetReporterError(string? errorCode)
    {
        lock (_sync)
            _reporterErrorCode = NormalizeErrorCode(errorCode);
    }

    public RadarAgentHealthPayload Snapshot(long sequence, DateTime? agentUtc = null)
    {
        lock (_sync)
        {
            return new RadarAgentHealthPayload
            {
                InstanceId = _instanceId,
                Sequence = sequence,
                InstanceStartedUtc = _instanceStartedUtc,
                AgentUtc = EnsureUtc(agentUtc ?? DateTime.UtcNow),
                Version = _version,
                ListenerState = _listenerState,
                WhatsAppState = _whatsAppState,
                WhatsAppStateSinceUtc = _whatsAppStateSinceUtc,
                LastSweepStartedUtc = _lastSweepStartedUtc,
                LastSweepCompletedUtc = _lastSweepCompletedUtc,
                ChatsConfigured = _chatsConfigured,
                ChatsReviewed = _chatsReviewed,
                CentralMode = RadarCentralIntelligenceClient.Habilitada ? "Central" : "Local",
                CentralState = _centralState,
                LastCentralSuccessUtc = _lastCentralSuccessUtc,
                FallbackEnabled = RadarCentralIntelligenceClient.FallbackLocalHabilitado,
                ErrorCode = _centralErrorCode ?? _reporterErrorCode
            };
        }
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string ReadVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(RadarAgentRuntimeHealth).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string NormalizeVersion(string value)
    {
        string normalized = new(value
            .Where(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '_' or '+' or '-')
            .Take(64)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static string? NormalizeErrorCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string normalized = new(value.Trim().ToUpperInvariant()
            .Where(x => char.IsAsciiLetterOrDigit(x) || x == '_')
            .Take(64)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
