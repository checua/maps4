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

        public BorradorController(IBorradorInmuebleRepository borradorRepository)
        {
            _borradorRepository = borradorRepository;
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

                return StatusCode(status, new
                {
                    success = false,
                    message = mensaje
                });
            }
        }
    }
}
