namespace RSMaps.Radar.Listener.Models;

public class RadarInterpretationResult
{
    public string Motor { get; set; } = "";

    public double? ConfianzaInterpretacion { get; set; }

    public List<SolicitudInmobiliaria> Solicitudes { get; set; } = [];

    public string? Observaciones { get; set; }
}
