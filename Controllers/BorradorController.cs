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
        private readonly IInmuebleFotoRepository _fotoRepository;
        private readonly IInmuebleFotoStorage _fotoStorage;
        private readonly ILogger<BorradorController> _logger;

        public BorradorController(
            IBorradorInmuebleRepository borradorRepository,
            IGenericRepository<TipoPropiedad> tipoPropiedadRepository,
            IInmuebleFotoRepository fotoRepository,
            IInmuebleFotoStorage fotoStorage,
            ILogger<BorradorController> logger)
        {
            _borradorRepository = borradorRepository;
            _tipoPropiedadRepository = tipoPropiedadRepository;
            _fotoRepository = fotoRepository;
            _fotoStorage = fotoStorage;
            _logger = logger;
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

                await PrepararModeloAsync(correo, modelo);
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
                await PrepararModeloAsync(correo, modelo);
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
                await PrepararModeloAsync(correo, modelo);
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(15 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
        public async Task<IActionResult> SubirFoto(int idInmueble, IFormFile? foto, CancellationToken cancellationToken)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized(new { success = false, message = "Tu sesión terminó." });

            if (idInmueble <= 0 || foto == null || foto.Length <= 0)
                return BadRequest(new { success = false, message = "Selecciona una imagen válida." });

            if (foto.Length > 12 * 1024 * 1024)
                return BadRequest(new { success = false, message = "La imagen supera el límite de 12 MB." });

            string mime = (foto.ContentType ?? string.Empty).ToLowerInvariant();
            if (mime is not ("image/jpeg" or "image/png" or "image/webp"))
                return BadRequest(new { success = false, message = "Usa imágenes JPG, PNG o WebP." });

            FotoAlmacenada? almacenada = null;
            try
            {
                BorradorEdicionViewModel? borrador = await _borradorRepository.ObtenerParaEdicionAsync(correo, idInmueble);
                if (borrador == null)
                    return NotFound(new { success = false, message = "El borrador no existe o no puedes editarlo." });

                List<InmuebleFotoViewModel> actuales = await _fotoRepository.ListarAsync(correo, idInmueble);
                if (actuales.Count >= 20)
                    return BadRequest(new { success = false, message = "El borrador ya tiene el máximo de 20 fotos." });

                almacenada = await _fotoStorage.GuardarAsync(idInmueble, foto, cancellationToken);
                long idImagen = await _fotoRepository.RegistrarAsync(correo, idInmueble, almacenada);
                InmuebleFotoViewModel? registrada = await _fotoRepository.ObtenerAsync(correo, idImagen);

                return Ok(new
                {
                    success = true,
                    idImagen,
                    url = $"/Borrador/Foto/{idImagen}",
                    esPortada = registrada?.EsPortada ?? actuales.Count == 0,
                    total = actuales.Count + 1
                });
            }
            catch (SqlException ex)
            {
                if (almacenada != null)
                {
                    try { await _fotoStorage.EliminarAsync(almacenada.ClaveAlmacenamiento, cancellationToken); }
                    catch (Exception cleanupEx) { _logger.LogWarning(cleanupEx, "No fue posible limpiar una foto rechazada del borrador {IdInmueble}.", idInmueble); }
                }

                return BadRequest(new { success = false, message = MensajeFotoSeguro(ex.Number) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Foto(long id, CancellationToken cancellationToken)
        {
            string? correo = User.Identity?.Name;
            if (id <= 0 || string.IsNullOrWhiteSpace(correo))
                return NotFound();

            try
            {
                InmuebleFotoViewModel? foto = await _fotoRepository.ObtenerAsync(correo, id);
                if (foto == null)
                    return NotFound();

                Stream? stream = await _fotoStorage.AbrirLecturaAsync(foto.ClaveAlmacenamiento, cancellationToken);
                if (stream == null)
                    return NotFound();

                Response.Headers.CacheControl = "private,max-age=3600";
                return File(stream, foto.MimeType, enableRangeProcessing: true);
            }
            catch (SqlException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EstablecerPortada(int idInmueble, long idImagen)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Challenge();

            try
            {
                await _fotoRepository.EstablecerPortadaAsync(correo, idInmueble, idImagen);
                TempData["BorradorOk"] = "Portada actualizada. El inmueble continúa privado.";
            }
            catch (SqlException ex)
            {
                TempData["BorradorFotoError"] = MensajeFotoSeguro(ex.Number);
            }

            return RedirectToAction(nameof(Editar), new { id = idInmueble });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoverFoto(int idInmueble, long idImagen, int direccion)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Challenge();

            try
            {
                await _fotoRepository.MoverAsync(correo, idInmueble, idImagen, direccion);
            }
            catch (SqlException ex)
            {
                TempData["BorradorFotoError"] = MensajeFotoSeguro(ex.Number);
            }

            return RedirectToAction(nameof(Editar), new { id = idInmueble });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFoto(int idInmueble, long idImagen, CancellationToken cancellationToken)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Challenge();

            try
            {
                InmuebleFotoViewModel? foto = await _fotoRepository.ObtenerAsync(correo, idImagen);
                if (foto == null || foto.IdInmueble != idInmueble)
                {
                    TempData["BorradorFotoError"] = "La foto no pertenece a este borrador.";
                    return RedirectToAction(nameof(Editar), new { id = idInmueble });
                }

                await _fotoRepository.EliminarMetadataAsync(correo, idInmueble, idImagen);

                try
                {
                    await _fotoStorage.EliminarAsync(foto.ClaveAlmacenamiento, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "La metadata de la foto {IdImagen} fue eliminada, pero el archivo físico quedó pendiente de limpieza.", idImagen);
                }

                TempData["BorradorOk"] = "Foto eliminada del borrador.";
            }
            catch (SqlException ex)
            {
                TempData["BorradorFotoError"] = MensajeFotoSeguro(ex.Number);
            }

            return RedirectToAction(nameof(Editar), new { id = idInmueble });
        }

        private async Task PrepararModeloAsync(string correo, BorradorEdicionViewModel modelo)
        {
            await CargarTiposAsync(modelo);
            modelo.Fotos = await _fotoRepository.ListarAsync(correo, modelo.IdInmueble);
            if (modelo.Fotos.Count > 0)
                modelo.Imagenes = modelo.Fotos.Count;
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

        private static string MensajeFotoSeguro(int numeroError)
        {
            return numeroError switch
            {
                53220 => "La imagen no tiene un tamaño válido.",
                53221 => "El formato de imagen no está permitido.",
                53223 or 53224 => "No fue posible validar tu sesión de trabajo.",
                53225 => "El inmueble ya no existe.",
                53226 => "El inmueble pertenece a otra cuenta.",
                53227 => "Por ahora solo el asesor responsable puede administrar estas fotos.",
                53228 or 53251 or 53261 => "Estas fotos solo pueden modificarse mientras el inmueble sea borrador.",
                53229 => "Tu rol actual no puede editar fotos del borrador.",
                53230 => "El borrador ya tiene el máximo de 20 fotos.",
                53252 or 53262 => "La foto ya no existe o no pertenece al borrador.",
                53310 or 53311 => "No fue posible validar tu sesión de trabajo.",
                53312 or 53323 => "La foto o el borrador ya no existen.",
                53313 or 53314 or 53321 => "No tienes permiso para operar estas fotos.",
                53322 => "El orden solo puede cambiarse mientras el inmueble sea borrador.",
                _ => "No fue posible completar la operación con la foto. Intenta nuevamente."
            };
        }
    }
}
