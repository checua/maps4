using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/processing")]
public sealed class RadarProcessingRecoveryController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarPendingProcessingRepository _pendingRepository;

    public RadarProcessingRecoveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarPendingProcessingRepository pendingRepository)
    {
        _pairingRepository = pairingRepository;
        _pendingRepository = pendingRepository;
    }

    [AllowAnonymous]
    [HttpGet("agent/pending")]
    public async Task<IActionResult> Pendientes(
        [FromQuery] int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        IReadOnlyList<RadarPendingProcessingItem> items = await _pendingRepository.ListarAsync(
            agent.IdAgent,
            max,
            cancellationToken);

        return Ok(items);
    }

    private async Task<RadarAgentAuthenticationResult?> AutenticarAgentAsync(
        CancellationToken cancellationToken)
    {
        string authorization = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string credencial = authorization[bearer.Length..].Trim();
        if (string.IsNullOrWhiteSpace(credencial))
            return null;

        return await _pairingRepository.ValidarCredencialAsync(
            credencial,
            cancellationToken);
    }
}