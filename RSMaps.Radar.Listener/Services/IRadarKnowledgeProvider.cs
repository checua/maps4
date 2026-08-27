using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public interface IRadarKnowledgeProvider
{
    Task<string?> ConstruirContextoAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadarKnowledgeTerm>> ObtenerTerminosRelevantesAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default);
}
