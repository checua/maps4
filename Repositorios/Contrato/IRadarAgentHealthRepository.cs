using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarAgentHealthRepository
{
    Task<RadarAgentHealthWriteResult> UpsertAsync(
        Guid idAgent,
        RadarAgentHeartbeatRequest request,
        CancellationToken cancellationToken = default);

    Task<List<RadarAgentHealthRecord>> ListForOwnerAsync(
        string correo,
        int idCuenta,
        CancellationToken cancellationToken = default);
}
