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

    public RadarAgentPairingApiController(IRadarAgentPairingRepository pairingRepository)
    {
        _pairingRepository = pairingRepository;
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
        string authorization = Request.Headers["Authorization"].ToString();
        const string bearer = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { mensaje = "Se requiere la credencial del RADAR Agent." });
        }

        string credencial = authorization[bearer.Length..].Trim();
        RadarAgentAuthenticationResult? resultado = await _pairingRepository.ValidarCredencialAsync(
            credencial,
            cancellationToken);

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
}
