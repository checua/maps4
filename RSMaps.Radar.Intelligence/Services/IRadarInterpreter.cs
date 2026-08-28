using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public interface IRadarInterpreter
{
    Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default);
}
