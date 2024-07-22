using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using maps4.Recursos;
using maps4.Repositorios.Implementacion;
using NuGet.Protocol.Core.Types;

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
        public async Task<IActionResult> IniciarSesion(string correo, string contra)
        {
            if (!string.IsNullOrEmpty(correo) && !string.IsNullOrEmpty(contra))
            {
                List<Usuario> _lista = new List<Usuario>();

                if (contra.Length < 20)
                {
                    _lista = await _usuarioRepositoryLogin.GetUsuario(correo, Utilidades.EncriptarClave(contra));
                }
                else
                {
                    _lista = await _usuarioRepositoryLogin.GetUsuario(correo, contra);
                }

                Usuario usuario_encontrado = _lista.FirstOrDefault();

                if (usuario_encontrado == null)
                {
                    ViewData["Mensaje"] = "No se encontraron coincidencias";
                    return View();
                }

                List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, usuario_encontrado.correo)
        };

                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                AuthenticationProperties properties = new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = true, // Para que la cookie persista después de cerrar el navegador
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(10) // Asegura que la cookie expira en 10 días
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                );

                if (contra.Length < 20)
                {
                    return StatusCode(StatusCodes.Status200OK, _lista);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
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

            if (!string.IsNullOrEmpty(usuario_creado.correo))
            {
                //return RedirectToAction("IniciarSesion", "Inicio");
                return RedirectToAction("IniciarSesion", "Inicio", new { correo = modelo.correo, contra = modelo.contra });
            }
            else
            {
                ViewData["Mensaje"] = string.IsNullOrEmpty(usuario_creado.revisado) ? "No se pudo crear el usuario" : usuario_creado.revisado;
                return View();
            }
        }

    }
}
