using maps4.Models;
using maps4.Recursos;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        // Mantiene una ruta GET válida para CookieAuthentication.LoginPath.
        // Las credenciales ya no deben viajar por URL.
        [HttpGet]
        public IActionResult IniciarSesion()
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> IniciarSesion(string correo, string contra)
        {
            if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contra))
            {
                return BadRequest(new { mensaje = "Correo y contraseña son obligatorios." });
            }

            string contraEncriptada = Utilidades.EncriptarClave(contra);
            List<Usuario> usuarios = await _usuarioRepositoryLogin.GetUsuario(correo.Trim(), contraEncriptada);
            Usuario? usuarioEncontrado = usuarios.FirstOrDefault();

            if (usuarioEncontrado == null)
            {
                return Unauthorized(new { mensaje = "Usuario o contraseña equivocados." });
            }

            if (!TieneContextoActivo(usuarioEncontrado))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { mensaje = "El usuario no tiene una cuenta activa configurada." });
            }

            await CrearSesionAsync(usuarioEncontrado);

            // Respuesta mínima. Nunca devolver hash de contraseña al navegador.
            return Ok(new
            {
                correo = usuarioEncontrado.correo,
                idAsesor = usuarioEncontrado.idAsesor,
                idCuenta = usuarioEncontrado.IdCuenta,
                cuenta = usuarioEncontrado.CuentaNombre,
                tipoCuenta = usuarioEncontrado.TipoCuenta,
                rol = usuarioEncontrado.RolCodigo
            });
        }

        [HttpPost]
        public async Task<IActionResult> Registrarse(Usuario modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo.correo) || string.IsNullOrWhiteSpace(modelo.contra))
            {
                ViewData["Mensaje"] = "Correo y contraseña son obligatorios.";
                return View(modelo);
            }

            string contraEncriptada = Utilidades.EncriptarClave(modelo.contra);
            modelo.contra = contraEncriptada;

            Usuario usuarioCreado = await _usuarioRepositoryLogin.SaveUsuario(modelo);

            if (string.IsNullOrEmpty(usuarioCreado.correo))
            {
                ViewData["Mensaje"] = string.IsNullOrEmpty(usuarioCreado.revisado)
                    ? "No se pudo crear el usuario"
                    : usuarioCreado.revisado;
                return View(modelo);
            }

            // El registro ya creó Usuario + Cuenta INDIVIDUAL + membresía.
            // Recuperamos el contexto recién creado y abrimos sesión desde el servidor,
            // sin enviar correo ni contraseña/hash por la URL.
            List<Usuario> usuarios = await _usuarioRepositoryLogin.GetUsuario(
                usuarioCreado.correo,
                contraEncriptada);

            Usuario? usuarioEncontrado = usuarios.FirstOrDefault();

            if (usuarioEncontrado != null && TieneContextoActivo(usuarioEncontrado))
            {
                await CrearSesionAsync(usuarioEncontrado);
            }

            return RedirectToAction("Index", "Home");
        }

        private static bool TieneContextoActivo(Usuario usuario)
        {
            return usuario.IdCuenta.HasValue
                && !string.IsNullOrWhiteSpace(usuario.RolCodigo)
                && !string.IsNullOrWhiteSpace(usuario.TipoCuenta);
        }

        private async Task CrearSesionAsync(Usuario usuario)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usuario.correo ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, usuario.idAsesor.ToString()),
                new Claim("IdCuenta", usuario.IdCuenta!.Value.ToString()),
                new Claim(ClaimTypes.Role, usuario.RolCodigo ?? string.Empty),
                new Claim("TipoCuenta", usuario.TipoCuenta ?? string.Empty),
                new Claim("CuentaNombre", usuario.CuentaNombre ?? string.Empty)
            };

            ClaimsIdentity claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            AuthenticationProperties properties = new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(10)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                properties);
        }
    }
}
