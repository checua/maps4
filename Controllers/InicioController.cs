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
        private readonly IInmuebleServicio<InmuebleData> _inmuebleRepository;
        //private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;

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

            if (usuario_creado.correo != "")
            {
                //return RedirectToAction("IniciarSesion", "Inicio");
                return RedirectToAction("IniciarSesion", "Inicio", new { correo = modelo.correo, contra = modelo.contra }); //Para cuando quiere loguearse después de registrarse
                ViewData["Mensaje"] = modelo.correo;
            }
            else
                ViewData["Mensaje"] = "No se pudo crear el usuario";
            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> GuardarInmueble([FromForm] InmuebleData data)
        //{
        //    try
        //    {
        //        var correo = data.Correo;

        //        var lat = data.Inmueble.Lat;
        //        var lng = data.Inmueble.Lng;
        //        var tipo = data.Inmueble.IdTipo;
        //        var terreno = data.Inmueble.Terreno;
        //        var construccion = data.Inmueble.Construccion;
        //        var precio = data.Inmueble.Precio;
        //        var observaciones = data.Inmueble.Observaciones;
        //        var contacto = data.Inmueble.Contacto;
        //        var numImagenes = data.Files.Count;

        //        // Aquí puedes procesar y guardar la información del inmueble (tipo, terreno, construccion)
        //        // Guardar las imágenes
        //        foreach (var file in data.Files)
        //        {
        //            if (file.Length > 0)
        //            {
        //                var filePath = Path.Combine("wwwroot/cargas", file.FileName);
        //                using (var stream = new FileStream(filePath, FileMode.Create))
        //                {
        //                    await file.CopyToAsync(stream);
        //                }
        //            }
        //        }

        //        return Json(new { success = true });
        //    }
        //    catch (Exception ex)
        //    {
        //        // Manejo de errores
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}
        //public async Task<IActionResult> GuardarInmueble([FromForm] InmuebleData data)
        //{
        //    Inmueble inmueble_creado = await _inmuebleRepository.SaveInmueble(data);

        //    return View();
        //}

        [HttpPost]
        public async Task<IActionResult> GuardarInmueble([FromForm] InmuebleData data)
        {
            // Aquí procesamos el modelo del inmueble y el correo

            // Procesa los datos del inmueble
            Data inmueble = data.Datax;
            //var files = data.Files.Count;
            //var correo = data.Correo;

            Data inmueble_creado = await _inmuebleRepository.SaveInmueble(inmueble); //, files, correo ); 

            // Aquí procesamos los archivos
            var archivos = data.Files;

            foreach (var file in archivos)
            {
                if (file.Length > 0)
                {
                    var filePath = Path.Combine("wwwroot/cargas", file.FileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }
            }

            return Json(new { success = true, message = "Inmueble y imágenes guardados correctamente!" });
        }

        //[HttpPost]
        //public async Task<IActionResult> GuardarInmueble([FromForm] InmuebleData data)
        //{
        //    // Aquí asumimos que InmuebleData incluye tanto el modelo Inmueble como el correo y los archivos
        //    // Procesa el modelo como antes
        //    // Ahora procesa los archivos
        //    foreach (var file in data.Files)
        //    {
        //        // Guardar cada archivo, por ejemplo en un sistema de archivos o base de datos
        //    }

        //    return Json(new { success = true, message = "Inmueble y imágenes guardados correctamente!" });
        //}
    }
}
