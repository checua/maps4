using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace maps4.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ComentariosController : Controller
    {
        private readonly IComentarioService _comentarioService;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<ComentariosController> _logger;

        public ComentariosController(IComentarioService comentarioService, IAntiforgery antiforgery, ILogger<ComentariosController> logger)
        {
            _comentarioService = comentarioService;
            _antiforgery = antiforgery;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarComentario([FromBody] Comentario comentario)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Modelo de comentario no válido: {@Comentario}", comentario);
                return BadRequest(ModelState);
            }

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