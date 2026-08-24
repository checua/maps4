using maps4.Models;
using maps4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/radar/matching")]
    public class RadarMatchingController : ControllerBase
    {
        private readonly IRadarMatchingService _matchingService;

        public RadarMatchingController(IRadarMatchingService matchingService)
        {
            _matchingService = matchingService;
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

        // Endpoint temporal de prueba para validar el ranking desde el navegador
        // mientras el Listener todavía no consume la API directamente.
        // Ejemplo:
        // /api/radar/matching/probar?operacion=Renta&tipo=Casa&zona=Domingo%20Arrieta|Tierra%20Blanca&precioMinimo=7000&precioMaximo=10000&recamaras=3&banos=2
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
