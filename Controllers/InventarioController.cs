using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace maps4.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly IInventarioRepository _inventarioRepository;

        public InventarioController(IInventarioRepository inventarioRepository)
        {
            _inventarioRepository = inventarioRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!int.TryParse(User.FindFirstValue("IdCuenta"), out int idCuenta)
                || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idAsesor))
            {
                return Forbid();
            }

            List<InventarioInmuebleViewModel> inmuebles =
                await _inventarioRepository.ListarAsync(idCuenta, idAsesor);

            InventarioIndexViewModel modelo = new InventarioIndexViewModel
            {
                CuentaNombre = User.FindFirstValue("CuentaNombre") ?? "Mi cuenta",
                Rol = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
                Inmuebles = inmuebles
            };

            return View(modelo);
        }
    }
}
