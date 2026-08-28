using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarAgentPairingRepository
{
    Task<RadarAgentPairingCreateResult> CrearCodigoAsync(
        string correo,
        string nombreAgent,
        CancellationToken cancellationToken = default);

    Task<RadarAgentPairingExchangeResult?> ConsumirCodigoAsync(
        string codigo,
        string nombreAgent,
        string? equipoNombre,
        CancellationToken cancellationToken = default);
}
