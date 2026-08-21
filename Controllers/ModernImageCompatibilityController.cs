using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    /// <summary>
    /// Puente temporal entre /cargas/{id}_{n}.jpg y el almacenamiento moderno.
    /// UseStaticFiles sirve primero los archivos legacy reales. Este controlador
    /// entra solo cuando esa ruta fisica no existe.
    ///
    /// - BORRADOR/privado: requiere sesion y autorizacion.
    /// - PUBLICADO + PUBLICO: puede leerse anonimamente para el marketplace.
    /// - La posicion 1 siempre corresponde a la portada moderna seleccionada.
    /// </summary>
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

        [AllowAnonymous]
        [HttpGet("/cargas/{idInmueble:int}_{orden:int}.jpg")]
        public async Task<IActionResult> Imagen(int idInmueble, int orden, CancellationToken cancellationToken)
        {
            if (idInmueble <= 0 || orden < 1 || orden > 20)
                return NotFound();

            InmuebleFotoViewModel? foto = null;
            bool accesoPrivado = false;

            try
            {
                string? correo = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

                if (!string.IsNullOrWhiteSpace(correo))
                {
                    try
                    {
                        List<InmuebleFotoViewModel> fotos = await _fotoRepository.ListarAsync(correo, idInmueble);
                        foto = fotos
                            .OrderByDescending(x => x.EsPortada)
                            .ThenBy(x => x.Orden)
                            .ThenBy(x => x.IdImagen)
                            .Skip(orden - 1)
                            .FirstOrDefault();

                        accesoPrivado = foto != null;
                    }
                    catch (SqlException)
                    {
                        // Si ya no es un borrador accesible, intentamos la lectura publica.
                    }
                }

                foto ??= await _fotoRepository.ObtenerPublicaPorOrdenAsync(idInmueble, orden);
                if (foto == null)
                    return NotFound();

                string etag = $"\"rsmaps-image-{foto.IdImagen}\"";
                Response.Headers.ETag = etag;
                Response.Headers.CacheControl = accesoPrivado
                    ? "private,max-age=0,must-revalidate"
                    : "public,max-age=60,must-revalidate";

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
                    foto.ClaveAlmacenamiento,
                    cancellationToken);

                return stream == null
                    ? NotFound()
                    : File(stream, foto.MimeType, enableRangeProcessing: true);
            }
            catch (SqlException)
            {
                // No revelamos si el inmueble existe o si simplemente no es publico.
                return NotFound();
            }
        }
    }
}
