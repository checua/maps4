using maps4.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using maps4.Repositorios.Contrato;

namespace maps4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGenericRepository<TipoPropiedad> _tipoPropiedadRepository;

        public HomeController(ILogger<HomeController> logger,
            IGenericRepository<TipoPropiedad> tipoPropiedadRepository)
        {
            _logger = logger;
            _tipoPropiedadRepository = tipoPropiedadRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> listaTipoPropiedades()
        {
            List<TipoPropiedad> _lista = await _tipoPropiedadRepository.Lista();
            return StatusCode(StatusCodes.Status200OK, _lista);
            //return View();
        }

        //[HttpGet]
        //public async Task<IActionResult> listaTipoPropiedades()
        //{
        //    List<TipoPropiedad> _lista = await _tipoPropiedadRepository.Lista();
        //    return StatusCode(StatusCodes.Status200OK, _lista);
        //    return View();
        //}

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
