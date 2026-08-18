using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    public class InmuebleController : Controller
    {
        private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;

        public InmuebleController(IInmuebleServicio<Inmueble> inmuebleRepository)
        {
            _inmuebleRepository = inmuebleRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RegistrarInmueble(InmuebleData data)
        {
            if (data.Datax == null)
            {
                return BadRequest(new { success = false, message = "Datos inválidos recibidos." });
            }

            string? correoAutenticado = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correoAutenticado))
            {
                return Unauthorized(new { success = false, message = "Se requiere iniciar sesión." });
            }

            Inmueble modelo = new Inmueble
            {
                IdInmueble = 1,
                Direccion = "",
                Lat = data.Datax.Lat,
                Lng = data.Datax.Lng,
                IdTipo = data.Datax.IdTipo,
                Telefono = "",
                Terreno = data.Datax.Terreno,
                Construccion = data.Datax.Construccion,
                Precio = data.Datax.Precio,
                Observaciones = data.Datax.Observaciones,
                Exclusiva = 1,
                Link = "",
                Contacto = data.Datax.Contacto,
                Imagenes = data.Files?.Count ?? 0,
                RefUsuario = new Usuario { correo = correoAutenticado }
            };

            var archivos = data.Files ?? new List<IFormFile>();

            try
            {
                Inmueble inmuebleCreado = await _inmuebleRepository.SaveInmueble(modelo);

                if (modelo.Imagenes != 0)
                {
                    int fileCounter = 1;
                    foreach (var file in archivos)
                    {
                        if (file.Length <= 0)
                            continue;

                        var fileName = $"{inmuebleCreado.IdInmueble}_{fileCounter}.jpg";
                        var filePath = Path.Combine("wwwroot/cargas", fileName);

                        using var stream = new FileStream(filePath, FileMode.Create);
                        await file.CopyToAsync(stream);
                        fileCounter++;
                    }
                }

                return Ok(new { success = true, message = "Inmueble e imágenes guardados correctamente." });
            }
            catch (SqlException ex) when (EsErrorAutorizacion(ex))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { success = false, message = "No tienes permiso para realizar esta operación." });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error en RegistrarInmueble: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "No fue posible guardar el inmueble." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ActualizarInmueble(InmuebleData data, int idInmueble)
        {
            if (data.Datax == null || idInmueble <= 0)
            {
                return BadRequest(new { success = false, message = "Datos de inmueble inválidos." });
            }

            string? correoAutenticado = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correoAutenticado))
            {
                return Unauthorized(new { success = false, message = "Se requiere iniciar sesión." });
            }

            Inmueble modelo = new Inmueble
            {
                IdInmueble = idInmueble,
                IdTipo = data.Datax.IdTipo,
                Terreno = data.Datax.Terreno,
                Construccion = data.Datax.Construccion,
                Precio = data.Datax.Precio,
                Observaciones = data.Datax.Observaciones,
                Contacto = data.Datax.Contacto,
                RefUsuario = new Usuario { correo = correoAutenticado }
            };

            try
            {
                await _inmuebleRepository.UpdateInmueble(modelo);
                return Ok(new { success = true, message = "Inmueble actualizado correctamente." });
            }
            catch (SqlException ex) when (EsErrorAutorizacion(ex))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { success = false, message = "No tienes permiso para modificar este inmueble." });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error en ActualizarInmueble: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "No fue posible actualizar el inmueble." });
            }
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> Eliminar(int idInmueble)
        {
            if (idInmueble <= 0)
            {
                return BadRequest(new { success = false, message = "ID de inmueble inválido." });
            }

            string? correoAutenticado = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correoAutenticado))
            {
                return Unauthorized(new { success = false, message = "Se requiere iniciar sesión." });
            }

            try
            {
                bool eliminado = await _inmuebleRepository.EliminarInmueble(idInmueble, correoAutenticado);

                return eliminado
                    ? Ok(new { success = true, message = "Inmueble eliminado correctamente." })
                    : StatusCode(StatusCodes.Status500InternalServerError,
                        new { success = false, message = "No fue posible eliminar el inmueble." });
            }
            catch (SqlException ex) when (EsErrorAutorizacion(ex))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { success = false, message = "No tienes permiso para eliminar este inmueble." });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error en Eliminar: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "No fue posible eliminar el inmueble." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInmuebleById(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid Inmueble ID");
            }

            List<Inmueble> lista = await _inmuebleRepository.GetInmuebleById(id);
            if (lista == null || lista.Count == 0)
            {
                return NotFound("Inmueble not found");
            }

            return Ok(lista);
        }

        [HttpGet("/share")]
        public async Task<IActionResult> Share(int inmuebleId)
        {
            var inmueble = await _inmuebleRepository.GetInmuebleById(inmuebleId);

            if (inmueble == null || inmueble.Count == 0)
            {
                return NotFound("Inmueble no encontrado");
            }

            return View(inmueble[0]);
        }

        private static bool EsErrorAutorizacion(SqlException ex)
        {
            return ex.Number is 51020 or 51021 or 51022 or 51023 or 51024
                or 51030 or 51031 or 51032 or 51033 or 51034 or 51035;
        }
    }
}
