using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarPendingDeliveryRepository
{
    Task<IReadOnlyList<RadarPendingDeliveryItem>> ListarAsync(
        Guid idAgent,
        int maxResultados,
        CancellationToken cancellationToken = default);
}