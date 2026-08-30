using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarMessageDeliveryRepository
{
    Task<RadarDeliveryPrepareResult> PrepararAsync(Guid idAgent, RadarDeliveryPrepareRequest request, CancellationToken cancellationToken = default);
    Task<RadarDeliveryCompleteResult> CompletarAsync(Guid idAgent, RadarDeliveryCompleteRequest request, CancellationToken cancellationToken = default);
}
