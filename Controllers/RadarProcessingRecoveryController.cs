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
    private readonly IRadarMessageProcessingRepository _processingRepository;

    public RadarProcessingRecoveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarPendingProcessingRepository pendingRepository,
        IRadarMessageProcessingRepository processingRepository)
    {
        _pairingRepository = pairingRepository;
        _pendingRepository = pendingRepository;
        _processingRepository = processingRepository;
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

    [AllowAnonymous]
    [HttpGet("agent/pending-downstream")]
    public async Task<IActionResult> DownstreamPendiente(
        [FromQuery] int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        IReadOnlyList<RadarPendingWorkflowItem> items = await _pendingRepository.ListarDownstreamPendienteAsync(
            agent.IdAgent,
            max,
            cancellationToken);

        return Ok(items);
    }

    [AllowAnonymous]
    [HttpPost("agent/terminal-ack")]
    public async Task<IActionResult> TerminalAck(
        [FromBody] RadarTerminalAckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.ChatOrigen) ||
            string.IsNullOrWhiteSpace(request.MessageId) ||
            string.IsNullOrWhiteSpace(request.DisposicionTerminal))
        {
            return BadRequest(new { mensaje = "ChatOrigen, MessageId and DisposicionTerminal are required." });
        }

        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        bool ok = await _processingRepository.MarcarTerminadoAsync(
            agent.IdAgent,
            request.ChatOrigen,
            request.MessageId,
            request.DisposicionTerminal,
            cancellationToken);

        if (!ok)
        {
            return Conflict(new
            {
                mensaje = "RADAR could not persist the terminal workflow ACK for this message."
            });
        }

        return Ok(new { ok = true });
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