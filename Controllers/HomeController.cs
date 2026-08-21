using maps4.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using maps4.Repositorios.Contrato;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace maps4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGenericRepository<TipoPropiedad> _tipoPropiedadRepository;
        private readonly IGenericRepository<Usuario> _usuarioRepository;
        private readonly IGenericRepository<Inmueble> _inmuebleRepository;
        private readonly IMarketplaceFiltroRepository _marketplaceFiltroRepository;

        public HomeController(
            ILogger<HomeController> logger,
            IGenericRepository<TipoPropiedad> tipoPropiedadRepository,
            IGenericRepository<Usuario> usuarioRepository,
            IGenericRepository<Inmueble> inmuebleRepository,
            IMarketplaceFiltroRepository marketplaceFiltroRepository)
        {
            _logger = logger;
            _tipoPropiedadRepository = tipoPropiedadRepository;
            _usuarioRepository = usuarioRepository;
            _inmuebleRepository = inmuebleRepository;
            _marketplaceFiltroRepository = marketplaceFiltroRepository;
        }

        public IActionResult Index()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewData["CorreoUsuario"] = userEmail;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> listaTipoPropiedades()
        {
            List<TipoPropiedad> _lista = await _tipoPropiedadRepository.Lista();
            return StatusCode(StatusCodes.Status200OK, _lista);
        }

        [HttpGet]
        public async Task<IActionResult> listaUsuarios()
        {
            List<Usuario> _lista = await _usuarioRepository.Lista();
            return StatusCode(StatusCodes.Status200OK, _lista);
        }

        [HttpGet]
        public async Task<IActionResult> listaAmenidadesFiltros()
        {
            List<AmenidadFiltroViewModel> lista = await _marketplaceFiltroRepository.ListarAmenidadesAsync();
            return StatusCode(StatusCodes.Status200OK, lista);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult GetUserClaims()
        {
            var user = HttpContext.User.Identity as ClaimsIdentity;

            if (user != null && user.Claims.Count() != 0)
            {
                var claims = user.Claims.Select(c => new { Type = c.Type, Value = c.Value });
                return new JsonResult(claims);
            }
            else
            {
                return new JsonResult(0);
            }
        }

        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("IniciarSesion", "Inicio");
        }

        [HttpGet]
        public async Task<IActionResult> listaInmuebles()
        {
            List<Inmueble> _lista = await _inmuebleRepository.Lista();
            return StatusCode(StatusCodes.Status200OK, _lista);
        }
    }
}
