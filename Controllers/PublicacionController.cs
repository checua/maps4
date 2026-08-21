using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    public class PublicacionController : Controller
    {
        private readonly IBorradorInmuebleRepository _borradorRepository;
        private readonly IPublicacionBorradorRepository _publicacionRepository;

        public PublicacionController(
            IBorradorInmuebleRepository borradorRepository,
            IPublicacionBorradorRepository publicacionRepository)
        {
            _borradorRepository = borradorRepository;
            _publicacionRepository = publicacionRepository;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(BorradorEdicionViewModel modelo)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Challenge();
            if (modelo.IdInmueble <= 0)
                return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["BorradorFotoError"] = "Revisa los datos capturados antes de publicar.";
                return RedirectToAction("Editar", "Borrador", new { id = modelo.IdInmueble });
            }

            try
            {
                // Primero persiste cualquier cambio que el usuario tenga aún en pantalla.
                // Publicar es una acción explícita y separada, pero no obliga a guardar dos veces.
                await _borradorRepository.GuardarAsync(correo, modelo);
                await _publicacionRepository.PublicarAsync(correo, modelo.IdInmueble);

                TempData["InventarioOk"] = $"Propiedad #{modelo.IdInmueble} publicada correctamente y visible en el marketplace.";
                return RedirectToAction("Index", "Inventario", new { borrador = modelo.IdInmueble });
            }
            catch (SqlException ex)
            {
                TempData["BorradorFotoError"] = MensajePublicacionSeguro(ex.Number);
                return RedirectToAction("Editar", "Borrador", new { id = modelo.IdInmueble });
            }
        }

        private static string MensajePublicacionSeguro(int numeroError)
        {
            return numeroError switch
            {
                53420 or 53421 or 53422 => "No fue posible validar tu sesión de trabajo.",
                53423 => "Tu rol actual no puede publicar borradores.",
                53424 => "El inmueble ya no existe.",
                53425 => "El inmueble pertenece a otra cuenta.",
                53426 => "Por ahora solo el asesor responsable puede publicar este borrador.",
                53427 or 53437 => "El inmueble ya no se encuentra disponible como borrador.",
                53430 => "Falta una ubicación válida.",
                53431 => "Selecciona un tipo de propiedad válido.",
                53432 => "Registra un precio mayor que cero.",
                53433 => "Registra al menos una superficie.",
                53434 => "Agrega una descripción comercial.",
                53435 => "Agrega al menos una foto.",
                53436 => "Selecciona una foto de portada.",
                _ => "No fue posible publicar la propiedad. Revisa la información e intenta nuevamente."
            };
        }
    }
}
