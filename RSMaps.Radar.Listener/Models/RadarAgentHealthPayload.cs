using System.Text.Json.Serialization;

namespace RSMaps.Radar.Listener.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RadarAgentHealthPayload
{
    public Guid InstanceId { get; init; }
    public long Sequence { get; init; }
    public DateTime InstanceStartedUtc { get; init; }
    public DateTime AgentUtc { get; init; }
    public string Version { get; init; } = string.Empty;
    public string ListenerState { get; init; } = string.Empty;
    public string WhatsAppState { get; init; } = string.Empty;
    public DateTime? WhatsAppStateSinceUtc { get; init; }
    public DateTime? LastSweepStartedUtc { get; init; }
    public DateTime? LastSweepCompletedUtc { get; init; }
    public int ChatsConfigured { get; init; }
    public int ChatsReviewed { get; init; }
    public string CentralMode { get; init; } = string.Empty;
    public string CentralState { get; init; } = string.Empty;
    public DateTime? LastCentralSuccessUtc { get; init; }
    public bool FallbackEnabled { get; init; }
    public string? ErrorCode { get; init; }
}
