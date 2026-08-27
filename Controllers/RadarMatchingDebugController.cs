using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace maps4.Controllers
{
    [ApiController]
    [Route("api/radar/debug")]
    public class RadarMatchingDebugController : ControllerBase
    {
        private readonly IInventarioRepository _inventarioRepository;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public RadarMatchingDebugController(
            IInventarioRepository inventarioRepository,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _inventarioRepository = inventarioRepository;
            _configuration = configuration;
            _environment = environment;
        }

        // Diagnóstico exclusivamente local/development para revisar qué datos
        // de zona está viendo realmente el matching. Nunca se expone fuera de loopback.
        [AllowAnonymous]
        [HttpGet("inmueble/{id:int}")]
        public async Task<IActionResult> Inmueble(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip))
                return NotFound();

            string? correo = _configuration["RadarMatching:CorreoInventario"];
            if (string.IsNullOrWhiteSpace(correo))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Falta RadarMatching:CorreoInventario.");

            var inventario = await _inventarioRepository.ListarAutorizadosAsync(correo);
            var inmueble = inventario.FirstOrDefault(x => x.IdInmueble == id);
            if (inmueble is null)
                return NotFound();

            return Ok(new
            {
                inmueble.IdInmueble,
                inmueble.TipoNombre,
                inmueble.Precio,
                inmueble.Direccion,
                inmueble.ZonaPrincipalCodigo,
                inmueble.ZonaPrincipalNombre,
                inmueble.ZonasCsv,
                inmueble.Observaciones,
                inmueble.Recamaras,
                inmueble.BanosCompletos,
                inmueble.AmenidadesCsv,
                inmueble.Lat,
                inmueble.Lng,
                inmueble.EstadoCodigo
            });
        }
    }
}
