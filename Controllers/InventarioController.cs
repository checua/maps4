using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace maps4.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private static readonly HashSet<string> EstadosPermitidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "PUBLICADO",
            "PAUSADO",
            "RETIRADO"
        };

        private static readonly HashSet<string> VisibilidadesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            "CUENTA",
            "COLABORADORES",
            "ENLACE",
            "PUBLICO"
        };

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idInmueble, string estadoNuevo)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Forbid();

            string estado = (estadoNuevo ?? string.Empty).Trim().ToUpperInvariant();
            if (!EstadosPermitidos.Contains(estado))
                return BadRequest();

            try
            {
                await _inventarioRepository.CambiarEstadoOVisibilidadAsync(
                    idInmueble,
                    correo,
                    estado,
                    null,
                    $"Cambio de estado desde Mi inventario web: {estado}");

                TempData["InventarioOk"] = estado switch
                {
                    "PAUSADO" => "La propiedad quedó pausada y salió del marketplace público.",
                    "RETIRADO" => "La propiedad quedó retirada del marketplace.",
                    "PUBLICADO" => "La propiedad quedó publicada nuevamente.",
                    _ => "Estado actualizado."
                };
            }
            catch (SqlException ex)
            {
                TempData["InventarioError"] = MensajeSeguro(ex.Number);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarVisibilidad(int idInmueble, string visibilidadNueva)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Forbid();

            string visibilidad = (visibilidadNueva ?? string.Empty).Trim().ToUpperInvariant();
            if (!VisibilidadesPermitidas.Contains(visibilidad))
                return BadRequest();

            try
            {
                await _inventarioRepository.CambiarEstadoOVisibilidadAsync(
                    idInmueble,
                    correo,
                    null,
                    visibilidad,
                    $"Cambio de visibilidad desde Mi inventario web: {visibilidad}");

                TempData["InventarioOk"] = visibilidad switch
                {
                    "PUBLICO" => "La propiedad es visible públicamente cuando su estado sea Publicado.",
                    "ENLACE" => "La propiedad quedó como Solo con enlace; no aparecerá en búsquedas públicas.",
                    "COLABORADORES" => "La propiedad quedó visible para colaboración autorizada.",
                    "CUENTA" => "La propiedad quedó visible únicamente dentro de tu cuenta.",
                    _ => "Visibilidad actualizada."
                };
            }
            catch (SqlException ex)
            {
                TempData["InventarioError"] = MensajeSeguro(ex.Number);
            }

            return RedirectToAction(nameof(Index));
        }

        private static string MensajeSeguro(int numeroError)
        {
            return numeroError switch
            {
                51323 => "La propiedad ya no existe.",
                51324 => "La propiedad pertenece a otra cuenta.",
                51325 => "No tienes permiso para modificar esta propiedad.",
                51326 => "El estado solicitado no está disponible.",
                51327 => "La visibilidad solicitada no está disponible.",
                51328 => "Las ventas y rentas deben cerrarse mediante el flujo de operación.",
                51329 => "Una propiedad cerrada no puede reabrirse desde esta acción.",
                51330 => "Ese cambio de estado no está permitido desde el estado actual.",
                51331 => "La propiedad ya tiene ese estado o visibilidad.",
                _ => "No fue posible actualizar la propiedad. Intenta nuevamente."
            };
        }
    }
}
