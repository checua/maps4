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

        public IActionResult Index()
        {
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> IniciarSesion(String correo, String contra)
        {
            List<Usuario> _lista = await _usuarioRepositoryLogin.GetUsuario(correo, contra);
            return StatusCode(StatusCodes.Status200OK, _lista);
        }
    }
}
