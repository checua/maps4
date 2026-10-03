using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace maps4.Models;

public static class RadarAgentHealthValues
{
    public static readonly HashSet<string> ListenerStates =
        new(StringComparer.OrdinalIgnoreCase) { "Starting", "Running", "Stopping", "Error" };

    public static readonly HashSet<string> WhatsAppStates =
        new(StringComparer.OrdinalIgnoreCase) { "Starting", "Ready", "WaitingForReady", "LoggedOut", "Error", "Unknown" };

    public static readonly HashSet<string> CentralStates =
        new(StringComparer.OrdinalIgnoreCase) { "Healthy", "Degraded", "NotRecentlyUsed", "Unknown" };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RadarAgentHeartbeatRequest : IValidatableObject
{
    public Guid InstanceId { get; set; }
    public long Sequence { get; set; }
    public DateTime InstanceStartedUtc { get; set; }
    public DateTime AgentUtc { get; set; }

    [Required, StringLength(64), RegularExpression("^[A-Za-z0-9._+\\-]+$")]
    public string Version { get; set; } = string.Empty;

    [Required, StringLength(24)]
    public string ListenerState { get; set; } = string.Empty;

    [Required, StringLength(32)]
    public string WhatsAppState { get; set; } = string.Empty;

    public DateTime? WhatsAppStateSinceUtc { get; set; }
    public DateTime? LastSweepStartedUtc { get; set; }
    public DateTime? LastSweepCompletedUtc { get; set; }

    [Range(0, 2000)]
    public int ChatsConfigured { get; set; }

    [Range(0, 2000)]
    public int ChatsReviewed { get; set; }

    [Required, StringLength(20), RegularExpression("^(Central|Local)$")]
    public string CentralMode { get; set; } = string.Empty;

    [Required, StringLength(24)]
    public string CentralState { get; set; } = string.Empty;

    public DateTime? LastCentralSuccessUtc { get; set; }
    public bool FallbackEnabled { get; set; }

    [StringLength(64), RegularExpression("^[A-Z0-9_]+$")]
    public string? ErrorCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (InstanceId == Guid.Empty)
            yield return new ValidationResult("instanceId es obligatorio.", [nameof(InstanceId)]);
        if (Sequence <= 0)
            yield return new ValidationResult("sequence debe ser mayor que cero.", [nameof(Sequence)]);
        if (!RadarAgentHealthValues.ListenerStates.Contains(ListenerState))
            yield return new ValidationResult("listenerState no es válido.", [nameof(ListenerState)]);
        if (!RadarAgentHealthValues.WhatsAppStates.Contains(WhatsAppState))
            yield return new ValidationResult("whatsAppState no es válido.", [nameof(WhatsAppState)]);
        if (!RadarAgentHealthValues.CentralStates.Contains(CentralState))
            yield return new ValidationResult("centralState no es válido.", [nameof(CentralState)]);
        if (ChatsReviewed > ChatsConfigured)
            yield return new ValidationResult("chatsReviewed no puede exceder chatsConfigured.", [nameof(ChatsReviewed)]);
        if (LastSweepStartedUtc.HasValue && LastSweepCompletedUtc < LastSweepStartedUtc)
            yield return new ValidationResult("El fin del barrido no puede preceder su inicio.", [nameof(LastSweepCompletedUtc)]);
        if (InstanceStartedUtc == default || AgentUtc == default)
            yield return new ValidationResult("Los timestamps de instancia y Agent son obligatorios.");
        if (ErrorCode is not null && string.IsNullOrWhiteSpace(ErrorCode))
            yield return new ValidationResult("errorCode debe omitirse o usar un código permitido.", [nameof(ErrorCode)]);
    }
}

public enum RadarAgentHealthWriteResult
{
    Accepted,
    OutOfOrder,
    PreviousInstance
}

public static class RadarAgentHeartbeatOrderPolicy
{
    public static RadarAgentHealthWriteResult Evaluate(
        Guid? currentInstanceId,
        long currentSequence,
        DateTime currentInstanceStartedUtc,
        RadarAgentHeartbeatRequest incoming)
    {
        if (!currentInstanceId.HasValue)
            return RadarAgentHealthWriteResult.Accepted;
        if (currentInstanceId == incoming.InstanceId)
        {
            return incoming.Sequence > currentSequence
                ? RadarAgentHealthWriteResult.Accepted
                : RadarAgentHealthWriteResult.OutOfOrder;
        }

        return incoming.InstanceStartedUtc > currentInstanceStartedUtc
            ? RadarAgentHealthWriteResult.Accepted
            : RadarAgentHealthWriteResult.PreviousInstance;
    }
}

public enum RadarAgentHealthLevel
{
    NoData,
    Online,
    Degraded,
    NoCommunication
}

public sealed class RadarAgentHealthRecord
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public long Sequence { get; set; }
    public DateTime InstanceStartedUtc { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public DateTime AgentUtc { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ListenerState { get; set; } = string.Empty;
    public string WhatsAppState { get; set; } = string.Empty;
    public DateTime? WhatsAppStateSinceUtc { get; set; }
    public DateTime? LastSweepStartedUtc { get; set; }
    public DateTime? LastSweepCompletedUtc { get; set; }
    public int ChatsConfigured { get; set; }
    public int ChatsReviewed { get; set; }
    public string CentralMode { get; set; } = string.Empty;
    public string CentralState { get; set; } = string.Empty;
    public DateTime? LastCentralSuccessUtc { get; set; }
    public bool FallbackEnabled { get; set; }
    public string? ErrorCode { get; set; }
    public int IntervaloRevisionMs { get; set; } = 60_000;
}

public sealed class RadarAgentHealthViewModel
{
    public DateTime ServerUtc { get; init; }
    public IReadOnlyList<RadarAgentHealthItemViewModel> Agents { get; init; } = [];
}

public sealed class RadarAgentHealthItemViewModel
{
    public string NombreAgent { get; init; } = string.Empty;
    public RadarAgentHealthLevel Level { get; init; }
    public string LevelLabel { get; init; } = string.Empty;
    public DateTime? ReceivedUtc { get; init; }
    public string? Version { get; init; }
    public string? ListenerState { get; init; }
    public string? WhatsAppState { get; init; }
    public DateTime? LastSweepCompletedUtc { get; init; }
    public int? ChatsConfigured { get; init; }
    public int? ChatsReviewed { get; init; }
    public string? CentralState { get; init; }
    public bool? FallbackEnabled { get; init; }
    public string? ErrorCode { get; init; }
}

public static class RadarAgentHealthPolicy
{
    public static RadarAgentHealthLevel Evaluate(RadarAgentHealthRecord? item, DateTime serverUtc)
    {
        if (item is null || item.InstanceId == Guid.Empty || item.ReceivedUtc == default)
            return RadarAgentHealthLevel.NoData;

        TimeSpan signalAge = serverUtc - DateTime.SpecifyKind(item.ReceivedUtc, DateTimeKind.Utc);
        if (signalAge > TimeSpan.FromSeconds(180))
            return RadarAgentHealthLevel.NoCommunication;
        if (signalAge > TimeSpan.FromSeconds(120))
            return RadarAgentHealthLevel.Degraded;

        int intervalMs = Math.Clamp(item.IntervaloRevisionMs, 10_000, 1_200_000);
        TimeSpan sweepLimit = TimeSpan.FromMilliseconds(Math.Max(intervalMs * 2L, intervalMs + 300_000L));
        bool sweepFresh = item.LastSweepCompletedUtc.HasValue
            && serverUtc - DateTime.SpecifyKind(item.LastSweepCompletedUtc.Value, DateTimeKind.Utc) <= sweepLimit;

        bool functional = item.ListenerState.Equals("Running", StringComparison.OrdinalIgnoreCase)
            && item.WhatsAppState.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            && sweepFresh
            && !item.CentralState.Equals("Degraded", StringComparison.OrdinalIgnoreCase)
            && item.ChatsConfigured > 0
            && item.ChatsReviewed == item.ChatsConfigured;

        return functional ? RadarAgentHealthLevel.Online : RadarAgentHealthLevel.Degraded;
    }

    public static string Label(RadarAgentHealthLevel level) => level switch
    {
        RadarAgentHealthLevel.Online => "Online",
        RadarAgentHealthLevel.Degraded => "Degradado",
        RadarAgentHealthLevel.NoCommunication => "Sin comunicación",
        _ => "Sin datos históricos"
    };
}
