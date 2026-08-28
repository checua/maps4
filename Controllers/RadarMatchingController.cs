using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace maps4.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/radar/matching")]
    public class RadarMatchingController : ControllerBase
    {
        private readonly IRadarMatchingService _matchingService;
        private readonly IRadarAgentPairingRepository _pairingRepository;
        private readonly IConfiguration _configuration;

        public RadarMatchingController(
            IRadarMatchingService matchingService,
            IRadarAgentPairingRepository pairingRepository,
            IConfiguration configuration)
        {
            _matchingService = matchingService;
            _pairingRepository = pairingRepository;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Comparar([FromBody] RadarMatchingRequest solicitud)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized();

            if (solicitud is null)
                return BadRequest("La solicitud es obligatoria.");

            RadarMatchingResponse resultado = await _matchingService.CompararAsync(correo, solicitud);
            return Ok(resultado);
        }

        // Ruta principal para RADAR Agent. La identidad, cuenta y alcance de inventario
        // se resuelven únicamente desde la credencial de dispositivo registrada en RSMaps.
        [AllowAnonymous]
        [HttpPost("agent")]
        public async Task<IActionResult> CompararAgent(
            [FromBody] RadarMatchingRequest solicitud,
            CancellationToken cancellationToken)
        {
            if (solicitud is null)
                return BadRequest("La solicitud es obligatoria.");

            string? credencial = ObtenerBearer();
            if (string.IsNullOrWhiteSpace(credencial))
                return Unauthorized(new { mensaje = "Se requiere la credencial del RADAR Agent." });

            RadarAgentAuthenticationResult? agent = await _pairingRepository.ValidarCredencialAsync(
                credencial,
                cancellationToken);

            if (agent is null || string.IsNullOrWhiteSpace(agent.Correo))
                return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

            RadarMatchingResponse resultado = await _matchingService.CompararAsync(agent.Correo, solicitud);
            return Ok(resultado);
        }

        // Compatibilidad temporal del prototipo anterior. Sólo loopback y correo fijo.
        // Se conserva mientras terminamos la migración de todos los Agents vinculados.
        [AllowAnonymous]
        [HttpPost("local")]
        public async Task<IActionResult> CompararLocal([FromBody] RadarMatchingRequest solicitud)
        {
            IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip))
                return NotFound();

            if (solicitud is null)
                return BadRequest("La solicitud es obligatoria.");

            string? correo = _configuration["RadarMatching:CorreoInventario"];
            if (string.IsNullOrWhiteSpace(correo))
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        success = false,
                        message = "Falta configurar RadarMatching:CorreoInventario en user-secrets."
                    });
            }

            RadarMatchingResponse resultado = await _matchingService.CompararAsync(correo, solicitud);
            return Ok(resultado);
        }

        // Endpoint temporal de prueba para validar el ranking desde el navegador.
        [HttpGet("probar")]
        public async Task<IActionResult> Probar(
            [FromQuery] string? operacion = null,
            [FromQuery] string? tipo = null,
            [FromQuery] string? zona = null,
            [FromQuery] decimal? precioMinimo = null,
            [FromQuery] decimal? precioMaximo = null,
            [FromQuery] int? recamaras = null,
            [FromQuery] int? banos = null,
            [FromQuery] int? cochera = null,
            [FromQuery] int maxResultados = 5)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized();

            var solicitud = new RadarMatchingRequest
            {
                Operacion = operacion,
                TiposPropiedad = Separar(tipo),
                Zonas = Separar(zona),
                PrecioMinimo = precioMinimo,
                PrecioMaximo = precioMaximo,
                RecamarasMin = recamaras,
                RecamarasMax = recamaras,
                BanosMin = banos,
                BanosMax = banos,
                CocheraMinAutos = cochera,
                MaxResultados = maxResultados
            };

            RadarMatchingResponse resultado = await _matchingService.CompararAsync(correo, solicitud);
            return Ok(resultado);
        }

        private string? ObtenerBearer()
        {
            string authorization = Request.Headers["Authorization"].ToString();
            const string bearer = "Bearer ";

            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string valor = authorization[bearer.Length..].Trim();
            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }

        private static List<string> Separar(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return new List<string>();

            return valor
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
