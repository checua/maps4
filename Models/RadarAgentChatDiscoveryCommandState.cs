namespace maps4.Models;

public sealed class RadarAgentChatDiscoveryCommandState
{
    public Guid IdAgent { get; set; }
    public DateTime? SolicitadaUtc { get; set; }
    public DateTime? CompletadaUtc { get; set; }

    public bool Pendiente =>
        SolicitadaUtc.HasValue &&
        (!CompletadaUtc.HasValue || CompletadaUtc.Value < SolicitadaUtc.Value);
}
