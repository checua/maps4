using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    public class BorradorFotoController : Controller
    {
        private readonly IInmuebleFotoRepository _fotoRepository;
        private readonly ILogger<BorradorFotoController> _logger;

        public BorradorFotoController(
            IInmuebleFotoRepository fotoRepository,
            ILogger<BorradorFotoController> logger)
        {
            _fotoRepository = fotoRepository;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mover(int idInmueble, long idImagen, int direccion)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized(new { success = false, message = "Tu sesión terminó." });

            if (idInmueble <= 0 || idImagen <= 0 || direccion is not (-1 or 1))
                return BadRequest(new { success = false, message = "La solicitud para reordenar la foto no es válida." });

            try
            {
                await _fotoRepository.MoverAsync(correo, idInmueble, idImagen, direccion);
                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex,
                    "Error SQL al reordenar foto {IdImagen} del inmueble {IdInmueble}. Código {NumeroError}.",
                    idImagen, idInmueble, ex.Number);

                string mensaje = ex.Number switch
                {
                    53310 or 53311 => "No fue posible validar tu sesión de trabajo.",
                    53312 or 53323 => "La foto o el borrador ya no existen.",
                    53313 or 53314 or 53321 => "No tienes permiso para reordenar estas fotos.",
                    53320 => "La dirección del movimiento no es válida.",
                    53322 => "El orden solo puede cambiarse mientras el inmueble sea borrador.",
                    _ => $"No fue posible reordenar la foto. Código SQL {ex.Number}."
                };

                return BadRequest(new { success = false, message = mensaje, sqlCode = ex.Number });
            }
        }
    }
}
