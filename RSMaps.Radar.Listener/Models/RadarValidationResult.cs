namespace RSMaps.Radar.Listener.Models;

public sealed class RadarValidationResult
{
    public List<string> Problemas { get; set; } = [];
    public List<string> Advertencias { get; set; } = [];

    public bool EsValida => Problemas.Count == 0;
    public bool RequiereFallback => Problemas.Count > 0;
}
