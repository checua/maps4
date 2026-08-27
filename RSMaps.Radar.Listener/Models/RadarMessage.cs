namespace RSMaps.Radar.Listener.Models;

public class RadarMessage
{
    public string MessageId { get; set; } = "";
    public string ChatOrigen { get; set; } = "";

    public string? Autor { get; set; }
    public string? Telefono { get; set; }

    public string TextoOriginal { get; set; } = "";
    public DateTime DetectadoEn { get; set; } = DateTime.Now;
}
