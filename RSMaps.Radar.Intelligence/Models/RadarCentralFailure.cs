namespace RSMaps.Radar.Listener.Models;

public sealed class RadarCentralFailure
{
    public const string PermanentCode = "RADAR_PROCESSING_PERMANENT_FAILURE";
    public string Code { get; init; } = string.Empty;
    public bool Permanent { get; init; }
    public bool Retry { get; init; } = true;
}
