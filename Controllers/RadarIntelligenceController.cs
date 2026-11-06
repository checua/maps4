using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
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

    public RadarIntelligenceController(IRadarAgentPairingRepository pairingRepository,
        IRadarMessageProcessingRepository messageProcessingRepository,
        IRadarCentralIntelligenceService intelligence, IRadarCentralProcessingService processing)
    {
        _pairingRepository = pairingRepository;
        _messageProcessingRepository = messageProcessingRepository;
        _intelligence = intelligence;
        _processing = processing;
    }

    [AllowAnonymous]
    [HttpPost("agent")]
    public async Task<IActionResult> ProcesarAgent([FromBody] RadarMessage mensaje, CancellationToken cancellationToken)
    {
        if (mensaje is null || string.IsNullOrWhiteSpace(mensaje.TextoOriginal) ||
            string.IsNullOrWhiteSpace(mensaje.MessageId) || string.IsNullOrWhiteSpace(mensaje.ChatOrigen) ||
            mensaje.MessageId.Trim().Length > 200 || mensaje.ChatOrigen.Trim().Length > 300)
            return BadRequest(new { mensaje = "Se requieren texto, MessageId y ChatOrigen válidos." });
        RadarMessageProcessingClaimResult? claim = null;
        try
        {
            RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
            if (agent is null || string.IsNullOrWhiteSpace(agent.Correo))
                return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });
            claim = await _messageProcessingRepository.ReclamarAsync(agent.IdAgent, mensaje, ProcessingLease, cancellationToken);
            Response.Headers["X-Radar-Attempt"] = claim.IntentosProcesamiento.ToString();
            if (claim.Status == RadarMessageProcessingClaimStatus.PermanentFailure) return FalloPermanente();
            if (claim.Status == RadarMessageProcessingClaimStatus.Completed)
            {
                // Corrupt caches must not become new central claims.
                if (!RadarCentralResultReader.TryRead(claim.ResultadoCentralJson, out var persistido, mensaje.MessageId, mensaje.ChatOrigen))
                {
                    Response.Headers["X-Radar-Processing"] = "invalid-cache";
                    return Conflict(new { code = "RADAR_PERSISTED_RESULT_INVALID", retry = true });
                }
                Response.Headers["X-Radar-Processing"] = "replay";
                return Ok(persistido);
            }
            if (claim.Status == RadarMessageProcessingClaimStatus.Busy) return Reintentar("busy", claim.DisponibleDespuesUtc);
            if (claim.Status != RadarMessageProcessingClaimStatus.Acquired || !claim.LeaseToken.HasValue)
                throw new InvalidOperationException("Claim central inválido.");
            if (!_intelligence.Configurada)
                return await RegistrarFalloAsync(claim, "Configuración central no disponible.", false, "unavailable");

            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(ProcessingBudget);
            RadarInterpretationResult resultado;
            try
            {
                Task<RadarInterpretationResult> work = _processing.ProcesarAsync(agent.Correo, mensaje, budget.Token);
                // A dependency may ignore cancellation. Bound the wait and never persist its late result.
                _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                resultado = await work.WaitAsync(budget.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested)
            {
                return await RegistrarFalloAsync(claim, "Presupuesto temporal central agotado.", false, "timeout");
            }
            string resultadoJson = JsonSerializer.Serialize(resultado, CacheJsonOptions);
            if (!RadarCentralResultReader.TryRead(resultadoJson, out _, mensaje.MessageId, mensaje.ChatOrigen))
                throw new InvalidOperationException("El resultado central no cumple el contrato estructural.");
            await _messageProcessingRepository.CompletarAsync(claim.IdRadarMessageProcessing, claim.LeaseToken.Value,
                resultado.Motor, resultadoJson, cancellationToken);
            Response.Headers["X-Radar-Processing"] = "fresh";
            return Ok(resultado);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await RegistrarFalloSeguroAsync(claim, "Solicitud HTTP cancelada.", false);
        }
        catch (Exception ex)
        {
            // Do not infer permanence from exception text or provider HTTP errors.
            return await RegistrarFalloSeguroAsync(claim, "Fallo central: " + ex.GetType().Name,
                ex is RadarPermanentMessageException);
        }
    }

    private IActionResult FalloPermanente()
    {
        Response.Headers.Remove("Retry-After");
        Response.Headers["X-Radar-Processing"] = "permanent-failure";
        return UnprocessableEntity(new RadarCentralFailure
        {
            Code = RadarCentralFailure.PermanentCode, Permanent = true, Retry = false
        });
    }

    private IActionResult Reintentar(string status, DateTime? disponible = null)
    {
        int seconds = disponible.HasValue ? Math.Clamp((int)Math.Ceiling((disponible.Value - DateTime.UtcNow).TotalSeconds), 1, 120) : 10;
        Response.Headers["Retry-After"] = seconds.ToString();
        Response.Headers["X-Radar-Processing"] = status;
        return StatusCode(503, new { code = "RADAR_PROCESSING_RETRY", retry = true });
    }

    private async Task<IActionResult> RegistrarFalloAsync(RadarMessageProcessingClaimResult claim,
        string error, bool permanente, string status)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await _messageProcessingRepository.RegistrarFalloAsync(claim.IdRadarMessageProcessing,
            claim.LeaseToken!.Value, error, RetryDelay, permanente, cleanup.Token);
        return result == RadarMessageProcessingFailureStatus.PermanentFailure ? FalloPermanente() : Reintentar(status);
    }

    private async Task<IActionResult> RegistrarFalloSeguroAsync(RadarMessageProcessingClaimResult? claim, string error, bool permanente)
    {
        if (claim?.Status == RadarMessageProcessingClaimStatus.Acquired && claim.LeaseToken.HasValue)
        {
            try { return await RegistrarFalloAsync(claim, error, permanente, "retry"); }
            catch { /* An expired lease remains recoverable if cleanup cannot commit. */ }
        }
        return Reintentar("unavailable");
    }

    private async Task<RadarAgentAuthenticationResult?> AutenticarAgentAsync(CancellationToken cancellationToken)
    {
        string authorization = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)) return null;
        string credencial = authorization[bearer.Length..].Trim();
        return credencial.Length == 0 ? null : await _pairingRepository.ValidarCredencialAsync(credencial, cancellationToken);
    }
}
