using maps4.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
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
        public async Task<IActionResult> listaUsuario(string x, string y)
        {
            List<Usuario> _lista = await _usuarioRepository.Lista(x,y);
            return StatusCode(StatusCodes.Status200OK, _lista);
            //return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> IniciarSesion(string correo, string clave)
        //{

        //    Usuario usuario_encontrado = await _usuarioServicio.GetUsuario(correo, Utilidades.EncriptarClave(clave));

        //    if (usuario_encontrado == null)
        //    {
        //        ViewData["Mensaje"] = "No se encontraron coincidencias";
        //        return View();
        //    }

        //    List<Claim> claims = new List<Claim>() {
        //        new Claim(ClaimTypes.Name, usuario_encontrado.NombreUsuario)
        //    };

        //    ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        //    AuthenticationProperties properties = new AuthenticationProperties()
        //    {
        //        AllowRefresh = true
        //    };

        //    await HttpContext.SignInAsync(
        //        CookieAuthenticationDefaults.AuthenticationScheme,
        //        new ClaimsPrincipal(claimsIdentity),
        //        properties
        //        );

        //    return RedirectToAction("Index", "Home");
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
