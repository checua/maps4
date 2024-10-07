using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace maps4.Pages.Share
{
    public class IndexModel : PageModel
    {
        private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;
        private readonly IGenericRepository<TipoPropiedad> _tipoPropiedadRepository;

        public Inmueble Inmueble { get; set; }
        public List<TipoPropiedad> TiposPropiedades { get; set; } // Nueva propiedad para todos los tipos de propiedades

        public IndexModel(IInmuebleServicio<Inmueble> inmuebleRepository, IGenericRepository<TipoPropiedad> tipoPropiedadRepository)
        {
            _inmuebleRepository = inmuebleRepository;
            _tipoPropiedadRepository = tipoPropiedadRepository;
        }

        public async Task<IActionResult> OnGetAsync(int idInmueble)
        {
            // Obtener el inmueble por ID
            List<Inmueble> _lista = await _inmuebleRepository.GetInmuebleById(idInmueble);

            if (_lista == null || !_lista.Any())
            {
                return NotFound("Inmueble no encontrado");
            }

            Inmueble = _lista[0];

            // Recuperar todos los tipos de propiedades
            TiposPropiedades = await _tipoPropiedadRepository.Lista();

            return Page();
        }
    }

}
