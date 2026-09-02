using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RSMaps.Radar.Listener.Models;
using System.Text.Json;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/intelligence")]
public sealed class RadarIntelligenceController : ControllerBase
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProcessingBudget = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarMessageProcessingRepository _messageProcessingRepository;
    private readonly IRadarCentralIntelligenceService _intelligence;
    private readonly IRadarCentralProcessingService _processing;

    public RadarIntelligenceController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarMessageProcessingRepository messageProcessingRepository,
        IRadarCentralIntelligenceService intelligence,
        IRadarCentralProcessingService processing)
    {
        _pairingRepository = pairingRepository;
        _messageProcessingRepository = messageProcessingRepository;
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

        if (string.IsNullOrWhiteSpace(mensaje.MessageId) || string.IsNullOrWhiteSpace(mensaje.ChatOrigen))
        {
            return BadRequest(new
            {
                mensaje = "RADAR requiere MessageId y ChatOrigen para procesamiento durable e idempotente."
            });
        }

        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null || string.IsNullOrWhiteSpace(agent.Correo))
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        RadarMessageProcessingClaimResult? claim = null;

        try
        {
            claim = await _messageProcessingRepository.ReclamarAsync(
                agent.IdAgent,
                mensaje,
                ProcessingLease,
                cancellationToken);

            Response.Headers["X-Radar-Attempt"] = claim.IntentosProcesamiento.ToString();

            if (claim.Status == RadarMessageProcessingClaimStatus.Completed)
            {
                RadarInterpretationResult? persistido = JsonSerializer.Deserialize<RadarInterpretationResult>(
                    claim.ResultadoCentralJson!,
                    CacheJsonOptions);

                if (persistido is null)
                    throw new InvalidOperationException("El resultado central persistido no pudo reconstruirse.");

                Response.Headers["X-Radar-Processing"] = "replay";
                return Ok(persistido);
            }

            if (claim.Status == RadarMessageProcessingClaimStatus.Busy)
            {
                int retryAfterSeconds = CalcularRetryAfter(claim.DisponibleDespuesUtc);
                Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                Response.Headers["X-Radar-Processing"] = "busy";

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        mensaje = "El mensaje RADAR ya está en procesamiento o espera de reintento.",
                        reintentarDespuesUtc = claim.DisponibleDespuesUtc
                    });
            }

            if (!claim.LeaseToken.HasValue)
                throw new InvalidOperationException("RADAR obtuvo el mensaje sin un lease de procesamiento válido.");

            Response.Headers["X-Radar-Processing"] = "fresh";

            if (!_intelligence.Configurada)
            {
                await _messageProcessingRepository.MarcarFalloReintentableAsync(
                    claim.IdRadarMessageProcessing,
                    claim.LeaseToken.Value,
                    "RADAR Intelligence central no tiene OPENAI_API_KEY configurada.",
                    RetryDelay,
                    CancellationToken.None);

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        mensaje = "RADAR Intelligence central todavía no tiene OPENAI_API_KEY configurada en RSMaps."
                    });
            }

            using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            processingCts.CancelAfter(ProcessingBudget);

            RadarInterpretationResult resultado;
            try
            {
                resultado = await _processing.ProcesarAsync(
                    agent.Correo,
                    mensaje,
                    processingCts.Token);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested &&
                processingCts.IsCancellationRequested)
            {
                const string errorTimeout =
                    "RADAR Intelligence central excedio 75 segundos de procesamiento.";

                await _messageProcessingRepository.MarcarFalloReintentableAsync(
                    claim.IdRadarMessageProcessing,
                    claim.LeaseToken.Value,
                    errorTimeout,
                    RetryDelay,
                    CancellationToken.None);

                Response.Headers["Retry-After"] = ((int)RetryDelay.TotalSeconds).ToString();
                Response.Headers["X-Radar-Processing"] = "timeout";

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        mensaje = errorTimeout,
                        reintentarDespuesUtc = DateTime.UtcNow.Add(RetryDelay)
                    });
            }

            string resultadoJson = JsonSerializer.Serialize(resultado, CacheJsonOptions);

            await _messageProcessingRepository.CompletarAsync(
                claim.IdRadarMessageProcessing,
                claim.LeaseToken.Value,
                resultado.Motor,
                resultadoJson,
                cancellationToken);

            return Ok(resultado);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarcarFalloSeguroAsync(
                claim,
                "La solicitud HTTP fue cancelada antes de completar RADAR Intelligence central.");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            await MarcarFalloSeguroAsync(claim, ex.Message);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    mensaje = "RADAR Intelligence central no pudo procesar el mensaje.",
                    detalle = ex.Message
                });
        }
    }

    private async Task MarcarFalloSeguroAsync(
        RadarMessageProcessingClaimResult? claim,
        string error)
    {
        if (claim?.Status != RadarMessageProcessingClaimStatus.Acquired ||
            !claim.LeaseToken.HasValue)
        {
            return;
        }

        try
        {
            await _messageProcessingRepository.MarcarFalloReintentableAsync(
                claim.IdRadarMessageProcessing,
                claim.LeaseToken.Value,
                error,
                RetryDelay,
                CancellationToken.None);
        }
        catch
        {
            // No se debe ocultar el fallo original si la persistencia del error también falla.
        }
    }

    private static int CalcularRetryAfter(DateTime? disponibleDespuesUtc)
    {
        if (!disponibleDespuesUtc.HasValue)
            return 5;

        double segundos = (disponibleDespuesUtc.Value - DateTime.UtcNow).TotalSeconds;
        return Math.Clamp((int)Math.Ceiling(segundos), 1, 120);
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
