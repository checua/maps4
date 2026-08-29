using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/agent")]
public sealed class RadarAgentPairingApiController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarAgentChatDiscoveryRepository _chatDiscoveryRepository;
    private readonly IRadarAgentChatDiscoveryCommandRepository _discoveryCommandRepository;

    public RadarAgentPairingApiController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarAgentChatDiscoveryRepository chatDiscoveryRepository,
        IRadarAgentChatDiscoveryCommandRepository discoveryCommandRepository)
    {
        _pairingRepository = pairingRepository;
        _chatDiscoveryRepository = chatDiscoveryRepository;
        _discoveryCommandRepository = discoveryCommandRepository;
    }

    [AllowAnonymous]
    [HttpPost("pair")]
    public async Task<IActionResult> Pair(
        [FromBody] RadarAgentPairingExchangeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return BadRequest(new { mensaje = "Se requiere el código de vinculación." });

        RadarAgentPairingExchangeResult? resultado = await _pairingRepository.ConsumirCodigoAsync(
            request.Codigo,
            request.NombreAgent,
            request.EquipoNombre,
            cancellationToken);

        if (resultado is null)
        {
            return Unauthorized(new
            {
                mensaje = "El código de vinculación no existe, expiró o ya fue utilizado."
            });
        }

        return Ok(new
        {
            idAgent = resultado.IdAgent,
            token = resultado.Token,
            idAsesor = resultado.IdAsesor,
            idCuenta = resultado.IdCuenta,
            cuenta = resultado.CuentaNombre,
            rol = resultado.RolCodigo
        });
    }

    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        RadarAgentAuthenticationResult? resultado = await AutenticarAgentAsync(cancellationToken);
        if (resultado is null)
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        return Ok(new
        {
            idAgent = resultado.IdAgent,
            nombreAgent = resultado.NombreAgent,
            equipo = resultado.EquipoNombre,
            idAsesor = resultado.IdAsesor,
            idCuenta = resultado.IdCuenta,
            cuenta = resultado.CuentaNombre,
            rol = resultado.RolCodigo
        });
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken cancellationToken)
    {
        RadarAgentAuthenticationResult? autenticacion = await AutenticarAgentAsync(cancellationToken);
        if (autenticacion is null)
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        RadarAgentConfiguration configuracion =
            await _pairingRepository.ObtenerConfiguracionAgentAsync(
                autenticacion.IdAgent,
                cancellationToken);

        RadarAgentChatDiscoveryCommandState? exploracion =
            await _discoveryCommandRepository.ObtenerEstadoAgentAsync(
                autenticacion.IdAgent,
                cancellationToken);

        return Ok(new
        {
            idAgent = autenticacion.IdAgent,
            configurada = configuracion.Configurada,
            chatsMonitoreados = configuracion.ChatsMonitoreados,
            destinoAlertas = configuracion.DestinoAlertas,
            intervaloRevisionMs = configuracion.IntervaloRevisionMs,
            terminosBusqueda = configuracion.TerminosBusqueda,
            actualizadoUtc = configuracion.ActualizadoUtc,
            exploracionChatsSolicitadaUtc = exploracion?.SolicitadaUtc,
            exploracionChatsCompletadaUtc = exploracion?.CompletadaUtc,
            exploracionChatsPendiente = exploracion?.Pendiente == true
        });
    }

    [AllowAnonymous]
    [HttpPost("chats/discovered")]
    public async Task<IActionResult> ReportarChats(
        [FromBody] RadarAgentChatDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        RadarAgentAuthenticationResult? autenticacion = await AutenticarAgentAsync(cancellationToken);
        if (autenticacion is null)
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        var chats = (request.Chats ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2_000)
            .ToList();

        if (chats.Count == 0)
            return BadRequest(new { mensaje = "No se recibieron chats para registrar." });

        bool guardados = await _chatDiscoveryRepository.ReemplazarChatsAsync(
            autenticacion.IdAgent,
            chats,
            cancellationToken);

        if (!guardados)
            return Unauthorized(new { mensaje = "No fue posible registrar los chats del RADAR Agent." });

        bool exploracionCompletada = false;
        if (request.SolicitudExploracionUtc.HasValue)
        {
            exploracionCompletada = await _discoveryCommandRepository.CompletarAsync(
                autenticacion.IdAgent,
                request.SolicitudExploracionUtc.Value,
                cancellationToken);
        }

        return Ok(new
        {
            idAgent = autenticacion.IdAgent,
            chatsDetectados = chats.Count,
            exploracionCompletada
        });
    }

    private async Task<RadarAgentAuthenticationResult?> AutenticarAgentAsync(
        CancellationToken cancellationToken)
    {
        string authorization = Request.Headers["Authorization"].ToString();
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
