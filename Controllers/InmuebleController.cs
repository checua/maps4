using Microsoft.AspNetCore.Mvc;
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using maps4.Recursos;
using maps4.Repositorios.Implementacion;
using NuGet.Protocol.Core.Types;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace maps4.Controllers
{
    public class InmuebleController : Controller
    {
        private readonly IInmuebleServicio<Inmueble> _inmuebleRepository;

        public InmuebleController(IInmuebleServicio<Inmueble> inmuebleRepository)
        {
            _inmuebleRepository = inmuebleRepository;

        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarInmueble(InmuebleData data)
        {
            Inmueble modelo = new Inmueble();
            modelo.IdInmueble = 1;
            modelo.Direccion = "";
            modelo.Lat = data.Datax.Lat;
            modelo.Lng = data.Datax.Lng;
            modelo.IdTipo = data.Datax.IdTipo;
            modelo.Telefono = "";
            modelo.Terreno = data.Datax.Terreno;
            modelo.Construccion = data.Datax.Construccion;
            modelo.Precio = data.Datax.Precio;
            modelo.Observaciones = data.Datax.Observaciones;
            modelo.Exclusiva = 1;
            modelo.Link = "";
            modelo.Contacto = data.Datax.Contacto;
            modelo.Imagenes = data.Files.Count;
            var correo = data.Correo;
            var archivos = data.Files;

            if (modelo == null)
            {
                return BadRequest("Datos inválidos recibidos.");
            }

            try
            {
                // Guarda el inmueble en la base de datos
                Inmueble inmueble_creado = await _inmuebleRepository.SaveInmueble(modelo);

                // Guarda los archivos
                if (inmueble_creado != null)
                {
                    int fileCounter = 1;
                    foreach (var file in archivos)
                    {
                        if (file.Length > 0)
                        {
                            // Obtener la extensión del archivo
                            var extension = Path.GetExtension(file.FileName);
                            // Generar el nombre del archivo
                            var fileName = $"{inmueble_creado.IdInmueble}_{fileCounter}{extension}";
                            var filePath = Path.Combine("wwwroot/cargas", fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            fileCounter++;
                        }
                    }
                }

                return Json(new { success = true, message = "Inmueble y imágenes guardados correctamente!" });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error en RegistrarInmueble: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
