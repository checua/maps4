using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using maps4.Models;
using maps4.Repositorios.Contrato; // Asegúrate de que el espacio de nombres sea correcto

namespace maps4.Pages.Test
{
    public class DefaultModel : PageModel
    {
        private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;

        // El modelo que será enviado a la vista
        public Inmueble Inmueble { get; set; }

        // Constructor con inyección de dependencia para acceder al repositorio
        public DefaultModel(IInmuebleServicio<Inmueble> inmuebleRepository)
        {
            _inmuebleRepository = inmuebleRepository;

        }

        // Método para manejar la solicitud GET
        public async Task<IActionResult> OnGetAsync(int idInmueble)
        {
            // Cargar el modelo `Inmueble` con el `idInmueble` proporcionado
            List<Inmueble> _lista = await _inmuebleRepository.GetInmuebleById(idInmueble);

            // Verificar si la lista es `null` o está vacía
            if (_lista == null || !_lista.Any())
            {
                return NotFound("Inmueble no encontrado");
            }

            // Asignar el primer elemento de la lista a `Inmueble`
            Inmueble = _lista[0];

            // Establecer el modelo de la página
            return Page();
        }

    }
}
