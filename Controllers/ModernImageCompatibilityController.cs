using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    /// <summary>
    /// Puente temporal entre las URLs legacy /cargas/{id}_1.jpg y el nuevo
    /// almacenamiento privado por imagen. Los archivos legacy reales siguen
    /// siendo servidos primero por UseStaticFiles(); este controlador solo
    /// entra cuando esa ruta física no existe.
    /// </summary>
    [Authorize]
    public class ModernImageCompatibilityController : Controller
    {
        private readonly IInmuebleFotoRepository _fotoRepository;
        private readonly IInmuebleFotoStorage _fotoStorage;

        public ModernImageCompatibilityController(
            IInmuebleFotoRepository fotoRepository,
            IInmuebleFotoStorage fotoStorage)
        {
            _fotoRepository = fotoRepository;
            _fotoStorage = fotoStorage;
        }

        [HttpGet("/cargas/{idInmueble:int}_1.jpg")]
        public async Task<IActionResult> Portada(int idInmueble, CancellationToken cancellationToken)
        {
            string? correo = User.Identity?.Name;
            if (idInmueble <= 0 || string.IsNullOrWhiteSpace(correo))
                return NotFound();

            try
            {
                List<InmuebleFotoViewModel> fotos = await _fotoRepository.ListarAsync(correo, idInmueble);
                InmuebleFotoViewModel? portada = fotos
                    .OrderByDescending(x => x.EsPortada)
                    .ThenBy(x => x.Orden)
                    .FirstOrDefault();

                if (portada == null)
                    return NotFound();

                // La URL legacy de portada es estable (/cargas/{id}_1.jpg), pero la
                // imagen moderna que representa puede cambiar. Revalidamos usando
                // un ETag basado en el IdImagen actual: si sigue siendo la misma,
                // el navegador aprovecha 304; si cambió/eliminó la portada, obtiene
                // inmediatamente la nueva sin requerir Ctrl+F5.
                string etag = $"\"rsmaps-cover-{portada.IdImagen}\"";
                Response.Headers.ETag = etag;
                Response.Headers.CacheControl = "private,max-age=0,must-revalidate";

                string ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
                if (!string.IsNullOrWhiteSpace(ifNoneMatch))
                {
                    bool coincide = ifNoneMatch
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(x => string.Equals(x, etag, StringComparison.Ordinal));

                    if (coincide)
                        return StatusCode(StatusCodes.Status304NotModified);
                }

                Stream? stream = await _fotoStorage.AbrirLecturaAsync(
                    portada.ClaveAlmacenamiento,
                    cancellationToken);

                if (stream == null)
                    return NotFound();

                return File(stream, portada.MimeType, enableRangeProcessing: true);
            }
            catch (SqlException)
            {
                // No revelamos si el inmueble existe o si simplemente no hay permiso.
                return NotFound();
            }
        }
    }
}
