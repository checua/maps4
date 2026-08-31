using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarPendingProcessingRepository
{
    Task<IReadOnlyList<RadarPendingProcessingItem>> ListarAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadarPendingWorkflowItem>> ListarDownstreamPendienteAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default);
}