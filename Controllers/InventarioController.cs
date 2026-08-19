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

        private static readonly HashSet<string> TiposCierrePermitidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "VENTA",
            "RENTA"
        };

        private readonly IInventarioRepository _inventarioRepository;

        public InventarioController(IInventarioRepository inventarioRepository)
        {
            _inventarioRepository = inventarioRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetAccountContext(out int idCuenta, out int idAsesor))
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

        [HttpGet]
        public async Task<IActionResult> CerrarOperacion(int idInmueble)
        {
            InventarioInmuebleViewModel? inmueble = await ObtenerInmueblePropioAsync(idInmueble);
            if (inmueble == null)
                return NotFound();

            if (inmueble.EstadoCodigo is not ("PUBLICADO" or "PAUSADO" or "RETIRADO"))
            {
                TempData["InventarioError"] = "El estado actual de la propiedad no permite registrar un cierre.";
                return RedirectToAction(nameof(Index));
            }

            string tipoSugerido = inmueble.TipoNombre?.Contains("Renta", StringComparison.OrdinalIgnoreCase) == true
                ? "RENTA"
                : "VENTA";

            CerrarOperacionViewModel modelo = new CerrarOperacionViewModel
            {
                IdInmueble = inmueble.IdInmueble,
                TipoOperacion = tipoSugerido,
                FechaCierre = DateOnly.FromDateTime(DateTime.Today),
                TipoNombre = inmueble.TipoNombre,
                Direccion = inmueble.Direccion,
                PrecioPublicado = inmueble.Precio,
                EstadoActual = inmueble.EstadoCodigo,
                VisibilidadActual = inmueble.VisibilidadCodigo,
                Imagenes = inmueble.Imagenes
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CerrarOperacion(CerrarOperacionViewModel modelo)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Forbid();

            modelo.TipoOperacion = (modelo.TipoOperacion ?? string.Empty).Trim().ToUpperInvariant();

            if (!TiposCierrePermitidos.Contains(modelo.TipoOperacion))
                ModelState.AddModelError(nameof(modelo.TipoOperacion), "Selecciona Venta o Renta.");

            if (modelo.FechaCierre > DateOnly.FromDateTime(DateTime.UtcNow))
                ModelState.AddModelError(nameof(modelo.FechaCierre), "La fecha de cierre no puede estar en el futuro.");

            InventarioInmuebleViewModel? inmueble = await ObtenerInmueblePropioAsync(modelo.IdInmueble);
            if (inmueble == null)
                return NotFound();

            CargarContextoCierre(modelo, inmueble);

            if (!ModelState.IsValid)
                return View(modelo);

            DateTime fechaCierre = modelo.FechaCierre.ToDateTime(TimeOnly.MinValue);
            DateTime fechaCierreUtc = DateTime.SpecifyKind(fechaCierre, DateTimeKind.Utc);

            try
            {
                await _inventarioRepository.CerrarOperacionAsync(
                    modelo.IdInmueble,
                    correo,
                    modelo.TipoOperacion,
                    modelo.PrecioCierre,
                    fechaCierreUtc,
                    modelo.NotasCierre);

                TempData["InventarioOk"] = modelo.TipoOperacion == "VENTA"
                    ? "Venta registrada correctamente. La propiedad quedó marcada como Vendida y salió del marketplace."
                    : "Renta registrada correctamente. La propiedad quedó marcada como Rentada y salió del marketplace.";

                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError(string.Empty, MensajeCierreSeguro(ex.Number));
                return View(modelo);
            }
        }

        private bool TryGetAccountContext(out int idCuenta, out int idAsesor)
        {
            idCuenta = 0;
            idAsesor = 0;

            bool cuentaValida = int.TryParse(User.FindFirstValue("IdCuenta"), out idCuenta);
            bool asesorValido = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out idAsesor);

            return cuentaValida && asesorValido;
        }

        private async Task<InventarioInmuebleViewModel?> ObtenerInmueblePropioAsync(int idInmueble)
        {
            if (idInmueble <= 0 || !TryGetAccountContext(out int idCuenta, out int idAsesor))
                return null;

            List<InventarioInmuebleViewModel> inmuebles =
                await _inventarioRepository.ListarAsync(idCuenta, idAsesor);

            return inmuebles.FirstOrDefault(x => x.IdInmueble == idInmueble);
        }

        private static void CargarContextoCierre(CerrarOperacionViewModel modelo, InventarioInmuebleViewModel inmueble)
        {
            modelo.TipoNombre = inmueble.TipoNombre;
            modelo.Direccion = inmueble.Direccion;
            modelo.PrecioPublicado = inmueble.Precio;
            modelo.EstadoActual = inmueble.EstadoCodigo;
            modelo.VisibilidadActual = inmueble.VisibilidadCodigo;
            modelo.Imagenes = inmueble.Imagenes;
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

        private static string MensajeCierreSeguro(int numeroError)
        {
            return numeroError switch
            {
                51420 => "Selecciona Venta o Renta.",
                51421 => "El precio de cierre debe ser mayor que cero.",
                51422 => "La fecha de cierre no puede estar en el futuro.",
                51423 => "No fue posible identificar al usuario autenticado.",
                51424 => "Tu usuario no pertenece a una cuenta activa.",
                51425 => "Tu usuario necesita seleccionar una cuenta de trabajo.",
                51426 => "La propiedad ya no existe.",
                51427 => "La propiedad pertenece a otra cuenta.",
                51428 => "No tienes permiso para cerrar esta propiedad.",
                51429 => "La propiedad ya tiene una operación cerrada.",
                51430 => "El estado actual de la propiedad no permite registrar un cierre.",
                51431 => "La fecha de cierre no puede ser anterior a la fecha de publicación conocida.",
                _ => "No fue posible registrar la operación. Revisa los datos e intenta nuevamente."
            };
        }
    }
}
