namespace RSMaps.Radar.Listener.Models;

public class RadarInterpretationResult
{
    public string Motor { get; set; } = "";

    public double? ConfianzaInterpretacion { get; set; }

    public List<SolicitudInmobiliaria> Solicitudes { get; set; } = [];

    public string? Observaciones { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
}
