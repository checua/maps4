using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    public class BorradorController : Controller
    {
        private readonly IBorradorInmuebleRepository _borradorRepository;
        private readonly IGenericRepository<TipoPropiedad> _tipoPropiedadRepository;

        public BorradorController(
            IBorradorInmuebleRepository borradorRepository,
            IGenericRepository<TipoPropiedad> tipoPropiedadRepository)
        {
            _borradorRepository = borradorRepository;
            _tipoPropiedadRepository = tipoPropiedadRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearBorradorRequest request)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Se requiere iniciar sesión."
                });
            }

            if (request.IdTipo <= 0 || request.Lat < -90 || request.Lat > 90 || request.Lng < -180 || request.Lng > 180)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La ubicación o el tipo de propiedad no son válidos."
                });
            }

            try
            {
                int idInmueble = await _borradorRepository.CrearAsync(
                    correo,
                    request.Lat,
                    request.Lng,
                    request.IdTipo);

                return Ok(new
                {
                    success = true,
                    idInmueble,
                    message = "Borrador creado correctamente. Solo es visible dentro de tu cuenta hasta que lo publiques."
                });
            }
            catch (SqlException ex)
            {
                return ErrorCrear(ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            string? correo = User.Identity?.Name;
            if (id <= 0 || string.IsNullOrWhiteSpace(correo))
                return NotFound();

            try
            {
                BorradorEdicionViewModel? modelo = await _borradorRepository.ObtenerParaEdicionAsync(correo, id);
                if (modelo == null)
                    return NotFound();

                await CargarTiposAsync(modelo);
                return View(modelo);
            }
            catch (SqlException ex) when (ex.Number == 52924)
            {
                return NotFound();
            }
            catch (SqlException ex) when (ex.Number is 52920 or 52921 or 52922 or 52923 or 52925 or 52926 or 52927)
            {
                TempData["InventarioError"] = MensajeEdicionSeguro(ex.Number);
                return RedirectToAction("Index", "Inventario");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(BorradorEdicionViewModel modelo, string accion = "continuar")
        {
            string? correo = User.Identity?.Name;
            if (modelo.IdInmueble <= 0 || string.IsNullOrWhiteSpace(correo))
                return NotFound();

            if (!ModelState.IsValid)
            {
                await RestaurarContextoPersistidoAsync(correo, modelo);
                await CargarTiposAsync(modelo);
                return View(modelo);
            }

            try
            {
                await _borradorRepository.GuardarAsync(correo, modelo);

                if (string.Equals(accion, "inventario", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InventarioOk"] = $"Borrador #{modelo.IdInmueble} guardado. Puedes continuar cuando quieras.";
                    return RedirectToAction("Index", "Inventario", new { borrador = modelo.IdInmueble });
                }

                TempData["BorradorOk"] = "Cambios guardados. El inmueble sigue siendo un borrador privado.";
                return RedirectToAction(nameof(Editar), new { id = modelo.IdInmueble });
            }
            catch (SqlException ex) when (ex.Number == 52924)
            {
                return NotFound();
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError(string.Empty, MensajeEdicionSeguro(ex.Number));
                await RestaurarContextoPersistidoAsync(correo, modelo);
                await CargarTiposAsync(modelo);
                return View(modelo);
            }
        }

        private async Task CargarTiposAsync(BorradorEdicionViewModel modelo)
        {
            List<TipoPropiedad> tipos = await _tipoPropiedadRepository.Lista();
            modelo.TiposDisponibles = tipos
                .Where(x => x.idTipoPropiedad > 1 && !string.IsNullOrWhiteSpace(x.nombre))
                .OrderBy(x => x.nombre)
                .ToList();
        }

        private async Task RestaurarContextoPersistidoAsync(string correo, BorradorEdicionViewModel modelo)
        {
            try
            {
                BorradorEdicionViewModel? actual = await _borradorRepository.ObtenerParaEdicionAsync(correo, modelo.IdInmueble);
                if (actual == null)
                    return;

                modelo.IdCuenta = actual.IdCuenta;
                modelo.IdAsesor = actual.IdAsesor;
                modelo.Lat = actual.Lat;
                modelo.Lng = actual.Lng;
                modelo.Imagenes = actual.Imagenes;
                modelo.EstadoCodigo = actual.EstadoCodigo;
                modelo.VisibilidadCodigo = actual.VisibilidadCodigo;
                modelo.FechaUltimaEdicionUtc = actual.FechaUltimaEdicionUtc;
            }
            catch (SqlException)
            {
                // El mensaje principal se conserva en ModelState.
            }
        }

        private static IActionResult ErrorCrear(SqlException ex)
        {
            string mensaje = ex.Number switch
            {
                52720 or 52721 => "La ubicación seleccionada no es válida.",
                52722 => "El tipo de propiedad seleccionado ya no está disponible.",
                52723 => "No fue posible identificar tu usuario en RSMaps.",
                52724 => "Tu usuario no pertenece a una cuenta activa.",
                52725 => "Selecciona una cuenta de trabajo antes de crear el borrador.",
                52726 => "Tu rol actual no tiene permiso para crear borradores.",
                _ => "No fue posible crear el borrador. Intenta nuevamente."
            };

            int status = ex.Number is 52723 or 52724 or 52725 or 52726
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;

            return new ObjectResult(new { success = false, message = mensaje }) { StatusCode = status };
        }

        private static string MensajeEdicionSeguro(int numeroError)
        {
            return numeroError switch
            {
                52920 => "No fue posible identificar tu usuario.",
                52921 => "Tu usuario no pertenece a una cuenta activa.",
                52922 => "Selecciona una cuenta de trabajo antes de continuar.",
                52923 => "Tu rol actual no tiene permiso para editar borradores.",
                52924 => "El borrador ya no existe.",
                52925 => "El borrador pertenece a otra cuenta.",
                52926 => "Por ahora solo el asesor responsable puede completar este borrador.",
                52927 => "Este inmueble ya no está en estado Borrador.",
                52930 => "Selecciona un tipo de propiedad válido.",
                52931 => "El terreno no puede ser negativo.",
                52932 => "La construcción no puede ser negativa.",
                52933 => "El precio no puede ser negativo.",
                _ => "No fue posible guardar el borrador. Intenta nuevamente."
            };
        }
    }
}
