using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/delivery")]
public sealed class RadarDeliveryController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarMessageDeliveryRepository _deliveryRepository;

    public RadarDeliveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarMessageDeliveryRepository deliveryRepository)
    {
        _pairingRepository = pairingRepository;
        _deliveryRepository = deliveryRepository;
    }

    [AllowAnonymous]
    [HttpPost("agent/prepare")]
    public async Task<IActionResult> Preparar(
        [FromBody] RadarDeliveryPrepareRequest request,
        CancellationToken cancellationToken)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        try
        {
            RadarDeliveryPrepareResult result = await _deliveryRepository.PrepararAsync(
                agent.IdAgent,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("agent/complete")]
    public async Task<IActionResult> Completar(
        [FromBody] RadarDeliveryCompleteRequest request,
        CancellationToken cancellationToken)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        try
        {
            RadarDeliveryCompleteResult result = await _deliveryRepository.CompletarAsync(
                agent.IdAgent,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
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
