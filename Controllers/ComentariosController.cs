using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace maps4.Controllers
{
    //[ApiController]
    //[Route("api/[controller]")]
    public class ComentariosController : Controller
    {
        private readonly IComentarioService _comentarioService;
        private readonly ILogger<ComentariosController> _logger;

        public ComentariosController(IComentarioService comentarioService, ILogger<ComentariosController> logger)
        {
            _comentarioService = comentarioService;
            _logger = logger;
        }

        // Método para servir la vista de comentarios
        //[HttpGet]
        //[Route("/comentarios")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetComentariosActivos(int? tipoInmuebleSolicitado)
        {
            try
            {
                var comentarios = await _comentarioService.GetComentariosActivos(tipoInmuebleSolicitado);
                return Ok(comentarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los comentarios");
                return StatusCode(500, new { success = false, message = "Error al obtener comentarios" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> RegistrarComentario(Comentario comentario)
        {
            _logger.LogInformation("Solicitud POST recibida para registrar un comentario.");

            if (comentario == null || string.IsNullOrEmpty(comentario.ComentarioTexto) || string.IsNullOrEmpty(comentario.Nivel))
            {
                _logger.LogWarning("Comentario o Nivel faltan en la solicitud.");
                return BadRequest(new { success = false, message = "Comentario y Nivel son obligatorios" });
            }

            try
            {
                // Completar los campos faltantes en el servidor
                comentario.Correo = HttpContext.User.Identity.Name;
                comentario.FechaComentario = DateTime.Now;
                comentario.Activo = true;

                bool result = await _comentarioService.RegistrarComentario(comentario);

                if (result)
                {
                    return Ok(new { success = true });
                }
                else
                {
                    _logger.LogWarning("No se pudo registrar el comentario: {@Comentario}", comentario);
                    return BadRequest(new { success = false, message = "No se pudo registrar el comentario" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el comentario.");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

    }
}
