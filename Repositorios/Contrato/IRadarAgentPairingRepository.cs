using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarAgentPairingRepository
{
    Task<RadarAgentPairingCreateResult> CrearCodigoAsync(
        string correo,
        int idCuenta,
        string nombreAgent,
        CancellationToken cancellationToken = default);

    Task<RadarAgentPairingExchangeResult?> ConsumirCodigoAsync(
        string codigo,
        string nombreAgent,
        string? equipoNombre,
        CancellationToken cancellationToken = default);

    Task<RadarAgentAuthenticationResult?> ValidarCredencialAsync(
        string credencial,
        CancellationToken cancellationToken = default);

    Task<List<RadarAgentDeviceListItem>> ListarAgentsAsync(
        string correo,
        int idCuenta,
        CancellationToken cancellationToken = default);

    Task<bool> RevocarAgentAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<RadarAgentConfiguration?> ObtenerConfiguracionAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<RadarAgentConfiguration> ObtenerConfiguracionAgentAsync(
        Guid idAgent,
        CancellationToken cancellationToken = default);

    Task<bool> GuardarConfiguracionAsync(
        string correo,
        int idCuenta,
        RadarAgentConfiguration configuracion,
        CancellationToken cancellationToken = default);
}
