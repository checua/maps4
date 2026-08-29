using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarAgentChatDiscoveryRepository
{
    Task<bool> ReemplazarChatsAsync(
        Guid idAgent,
        IReadOnlyCollection<string> chats,
        CancellationToken cancellationToken = default);

    Task<List<RadarAgentDiscoveredChatItem>> ListarChatsAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default);
}
