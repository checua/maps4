using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace maps4.Share
{
    public class IndexModel : PageModel
    {
        private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;

        // El modelo que ser� enviado a la vista
        public Inmueble Inmueble { get; set; }

        // Constructor con inyecci�n de dependencia para acceder al repositorio
        public IndexModel(IInmuebleServicio<Inmueble> inmuebleRepository)
        {
            _inmuebleRepository = inmuebleRepository;

        }

        // M�todo para manejar la solicitud GET
        public async Task<IActionResult> OnGetAsync(int idInmueble)
        {
            // Cargar el modelo `Inmueble` con el `idInmueble` proporcionado
            List<Inmueble> _lista = await _inmuebleRepository.GetInmuebleById(idInmueble);

            // Verificar si la lista es `null` o est� vac�a
            if (_lista == null || !_lista.Any())
            {
                return NotFound("Inmueble no encontrado");
            }

            // Asignar el primer elemento de la lista a `Inmueble`
            Inmueble = _lista[0];

            // Establecer el modelo de la p�gina
            return Page();
        }
    }
}