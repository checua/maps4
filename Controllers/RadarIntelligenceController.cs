using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RSMaps.Radar.Listener.Models;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/intelligence")]
public sealed class RadarIntelligenceController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarCentralIntelligenceService _intelligence;
    private readonly IRadarCentralProcessingService _processing;

    public RadarIntelligenceController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarCentralIntelligenceService intelligence,
        IRadarCentralProcessingService processing)
    {
        _pairingRepository = pairingRepository;
        _intelligence = intelligence;
        _processing = processing;
    }

    [AllowAnonymous]
    [HttpPost("agent")]
    public async Task<IActionResult> ProcesarAgent(
        [FromBody] RadarMessage mensaje,
        CancellationToken cancellationToken)
    {
        if (mensaje is null || string.IsNullOrWhiteSpace(mensaje.TextoOriginal))
            return BadRequest(new { mensaje = "Se requiere un mensaje de WhatsApp para procesar." });

        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null || string.IsNullOrWhiteSpace(agent.Correo))
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        if (!_intelligence.Configurada)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    mensaje = "RADAR Intelligence central todavía no tiene OPENAI_API_KEY configurada en RSMaps."
                });
        }

        try
        {
            RadarInterpretationResult resultado = await _processing.ProcesarAsync(
                agent.Correo,
                mensaje,
                cancellationToken);

            return Ok(resultado);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    mensaje = "RADAR Intelligence central no pudo procesar el mensaje.",
                    detalle = ex.Message
                });
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
