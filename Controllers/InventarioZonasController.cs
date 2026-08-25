using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    [Route("InventarioZonas")]
    public class InventarioZonasController : Controller
    {
        private readonly IInventarioRepository _inventarioRepository;

        public InventarioZonasController(IInventarioRepository inventarioRepository)
        {
            _inventarioRepository = inventarioRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized();

            try
            {
                var inmuebles = await _inventarioRepository.ListarAutorizadosAsync(correo);

                var zonas = inmuebles
                    .Where(x => x.TieneZona)
                    .Select(x => new
                    {
                        idInmueble = x.IdInmueble,
                        zonaPrincipalCodigo = x.ZonaPrincipalCodigo,
                        zonaPrincipalNombre = x.ZonaPrincipalNombre,
                        zonasCsv = x.ZonasCsv
                    })
                    .ToList();

                return Ok(new { success = true, zonas });
            }
            catch (SqlException ex) when (ex.Number is 52120 or 52121 or 52122 or 52123 or 52520 or 52521 or 52522)
            {
                return Forbid();
            }
        }
    }
}
