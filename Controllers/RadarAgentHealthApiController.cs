using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/agent/health")]
public sealed class RadarAgentHealthApiController : ControllerBase
{
    private const long MaximumBodyBytes = 8 * 1024;
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarAgentHealthRepository _healthRepository;

    public RadarAgentHealthApiController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarAgentHealthRepository healthRepository)
    {
        _pairingRepository = pairingRepository;
        _healthRepository = healthRepository;
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("radar-agent-health")]
    [RequestSizeLimit(MaximumBodyBytes)]
    public async Task<IActionResult> Post(
        [FromBody] RadarAgentHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength > MaximumBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        RadarAgentAuthenticationResult? agent = await AuthenticateAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { code = "AGENT_UNAUTHORIZED" });

        DateTime nowUtc = DateTime.UtcNow;
        if (request.AgentUtc > nowUtc.AddMinutes(5)
            || request.InstanceStartedUtc > nowUtc.AddMinutes(5)
            || request.InstanceStartedUtc > request.AgentUtc.AddMinutes(1))
        {
            return BadRequest(new { code = "INVALID_AGENT_TIME" });
        }

        RadarAgentHealthWriteResult result = await _healthRepository.UpsertAsync(
            agent.IdAgent,
            request,
            cancellationToken);

        return result switch
        {
            RadarAgentHealthWriteResult.Accepted => NoContent(),
            RadarAgentHealthWriteResult.OutOfOrder => Conflict(new { code = "HEARTBEAT_OUT_OF_ORDER" }),
            RadarAgentHealthWriteResult.PreviousInstance => Conflict(new { code = "PREVIOUS_INSTANCE_REJECTED" }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private async Task<RadarAgentAuthenticationResult?> AuthenticateAgentAsync(
        CancellationToken cancellationToken)
    {
        string authorization = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string credential = authorization[bearer.Length..].Trim();
        return string.IsNullOrWhiteSpace(credential)
            ? null
            : await _pairingRepository.ValidarCredencialAsync(credential, cancellationToken);
    }
}
