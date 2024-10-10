using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace maps4.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        [HttpGet]
        [Route("/comentarios")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarComentario([FromBody] Comentario comentario)
        {
            if (string.IsNullOrEmpty(comentario.ComentarioTexto) || string.IsNullOrEmpty(comentario.Nivel))
            {
                return BadRequest(new { success = false, message = "Comentario y plan son obligatorios" });
            }

            // Obtener el correo electrónico del usuario autenticado
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new { success = false, message = "Usuario no autenticado" });
            }

            // Llenar los campos que se obtienen automáticamente
            comentario.Correo = userEmail;
            comentario.FechaComentario = DateTime.Now;
            comentario.FechaExpiracion = DateTime.Now.AddMonths(1); // Suponiendo una fecha de expiración de un mes
            comentario.Activo = true;

            try
            {
                var result = await _comentarioService.RegistrarComentario(comentario);
                if (result)
                {
                    return Ok(new { success = true });
                }
                _logger.LogWarning("No se pudo registrar el comentario: {@Comentario}", comentario);
                return BadRequest(new { success = false, message = "Error al registrar el comentario" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el comentario: {@Comentario}", comentario);
                return StatusCode(500, new { success = false, message = "Ocurrió un error al procesar su solicitud" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetComentariosActivos()
        {
            try
            {
                var comentarios = await _comentarioService.GetComentariosActivos();
                return Ok(comentarios);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentarios activos");
                return StatusCode(500, new { success = false, message = "Ocurrió un error al obtener los comentarios" });
            }
        }
    }
}
