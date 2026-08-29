namespace maps4.Models;

public sealed class RadarAgentChatDiscoveryRequest
{
    public List<string> Chats { get; set; } = [];
    public DateTime? SolicitudExploracionUtc { get; set; }
}

public sealed class RadarAgentDiscoveredChatItem
{
    public string Nombre { get; set; } = string.Empty;
    public DateTime UltimoVistoUtc { get; set; }
}
