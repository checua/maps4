using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using maps4.Recursos;
using maps4.Repositorios.Implementacion;

namespace maps4.Controllers
{
    public class InicioController : Controller
    {
        private readonly IUsuarioServicio<Usuario> _usuarioRepositoryLogin;

        public InicioController(IUsuarioServicio<Usuario> usuarioRepositoryLogin)
        {
            _usuarioRepositoryLogin = usuarioRepositoryLogin;
        }

        public IActionResult Registrarse()
        {
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> IniciarSesion(String correo, String contra)
        {
            if (correo != null && contra != null)
            {
                List<Usuario> _lista = await _usuarioRepositoryLogin.GetUsuario(correo, Utilidades.EncriptarClave(contra));
                Usuario usuario_encontrado = _lista.FirstOrDefault();

                if (usuario_encontrado == null)
                {
                    ViewData["Mensaje"] = "No se encontraron coincidencias";
                    return View();
                }

                List<Claim> claims = new List<Claim>() {
                new Claim(ClaimTypes.Name, usuario_encontrado.correo)
            };

                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                AuthenticationProperties properties = new AuthenticationProperties()
                {
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                    );

                return StatusCode(StatusCodes.Status200OK, _lista);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }


        [HttpPost]
        public async Task<IActionResult> Registrarse(Usuario modelo)
        {
            modelo.contra = Utilidades.EncriptarClave(modelo.contra);

            Usuario usuario_creado = await _usuarioRepositoryLogin.SaveUsuario(modelo);

            if (usuario_creado.idAsesor > 0)
                return RedirectToAction("IniciarSesion", "Inicio");

            ViewData["Mensaje"] = "No se pudo crear el usuario";
            return View();
        }

    }
}
