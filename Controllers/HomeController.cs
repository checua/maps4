using maps4.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using maps4.Repositorios.Contrato;
using System.Security.Claims;


namespace maps4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGenericRepository<TipoPropiedad> _tipoPropiedadRepository;
        private readonly IUsuarioService<Usuario> _usuarioRepository;

        public HomeController(ILogger<HomeController> logger,
            IGenericRepository<TipoPropiedad> tipoPropiedadRepository,
             IUsuarioService<Usuario> usuarioRepository)
        {
            _logger = logger;
            _tipoPropiedadRepository = tipoPropiedadRepository;
            _usuarioRepository = usuarioRepository;
        }

        public IActionResult Index()
        {

            //ClaimsPrincipal claimuser = HttpContext.User;
            //string nombreUsuario = "";

            //if (claimuser.Identity.IsAuthenticated)
            //{
            //    nombreUsuario = claimuser.Claims.Where(c => c.Type == ClaimTypes.Name)
            //        .Select(c => c.Value).SingleOrDefault();
            //}

            //ViewData["nombreUsuario"] = nombreUsuario;

            //GetUserClaims();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> listaTipoPropiedades()
        {

            List<TipoPropiedad> _lista = await _tipoPropiedadRepository.Lista();
            return StatusCode(StatusCodes.Status200OK, _lista);
            //return View();
        }

        [HttpGet]
        public async Task<IActionResult> listaUsuarios()
        {
            List<Usuario> _lista = await _usuarioRepository.Lista(x,y);
            return StatusCode(StatusCodes.Status200OK, _lista);
            //return View();
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

            if (user != null)
            {
                var claims = user.Claims.Select(c => new { Type = c.Type, Value = c.Value });
                return new JsonResult(claims);
            }
            else
            {
                return new JsonResult(null);
            }
        }
    }
}
