using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarAgentChatDiscoveryCommandRepository
{
    Task<RadarAgentChatDiscoveryCommandState?> ObtenerEstadoAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<RadarAgentChatDiscoveryCommandState?> ObtenerEstadoAgentAsync(
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<DateTime?> SolicitarAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<bool> CompletarAsync(
        Guid idAgent,
        DateTime solicitudUtc,
        CancellationToken cancellationToken = default);
}
