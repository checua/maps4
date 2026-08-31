using maps4.Models;
using RSMaps.Radar.Listener.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarMessageProcessingRepository
{
    Task<RadarMessageProcessingClaimResult> ReclamarAsync(
        Guid idAgent,
        RadarMessage mensaje,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task CompletarAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string? motorInteligencia,
        string resultadoCentralJson,
        CancellationToken cancellationToken = default);

    Task<bool> MarcarTerminadoAsync(
        Guid idAgent,
        string chatOrigen,
        string messageId,
        string disposicionTerminal,
        CancellationToken cancellationToken = default);

    Task<bool> MarcarFalloReintentableAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string error,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}