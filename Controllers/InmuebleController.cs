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

            //if(data.Files != null)
            //modelo.Imagenes = data.Files.Count;
            //else
            //{
            //    modelo.Imagenes = 0;
            //}

            modelo.Imagenes = data.Files?.Count ?? 0;


            modelo.RefUsuario = new Usuario();
            modelo.RefUsuario.correo = data.Correo;

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
                    if (modelo.Imagenes != 0)
                    {
                        int fileCounter = 1;
                        foreach (var file in archivos)
                        {
                            if (file.Length > 0)
                            {
                                // Generar el nombre del archivo con la extensión .jpg
                                var fileName = $"{inmueble_creado.IdInmueble}_{fileCounter}.jpg";
                                var filePath = Path.Combine("wwwroot/cargas", fileName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }
                                fileCounter++;
                            }
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

        [HttpPost]
        public async Task<IActionResult> ActualizarInmueble(InmuebleData data, int idInmueble)
        {
            Inmueble modelo = new Inmueble();
            //Inmueble modelo = await _inmuebleRepository.GetInmuebleById(data.IdInmueble);
            if (modelo == null)
            {
                return BadRequest("Inmueble no encontrado.");
            }

            modelo.IdInmueble = idInmueble;
            modelo.IdTipo = data.Datax.IdTipo;
            modelo.Terreno = data.Datax.Terreno;
            modelo.Construccion = data.Datax.Construccion;
            modelo.Precio = data.Datax.Precio;
            modelo.Observaciones = data.Datax.Observaciones;
            modelo.Contacto = data.Datax.Contacto;

            // No actualizamos Lat, Lng y Files en este método.

            modelo.RefUsuario = new Usuario();
            modelo.RefUsuario.correo = data.Correo;

            try
            {
                // Actualiza el inmueble en la base de datos
                await _inmuebleRepository.UpdateInmueble(modelo);

                return Json(new { success = true, message = "Inmueble actualizado correctamente!" });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error en ActualizarInmueble: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Eliminar(int idInmueble)
        {
            if (idInmueble <= 0)
            {
                return BadRequest(new { success = false, message = "ID de inmueble inválido." });
            }

            bool eliminado = await _inmuebleRepository.EliminarInmueble(idInmueble);

            if (eliminado)
            {
                return Ok(new { success = true, message = "Inmueble eliminado correctamente." });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Error al eliminar el inmueble." });
            }
        }
    }
}
