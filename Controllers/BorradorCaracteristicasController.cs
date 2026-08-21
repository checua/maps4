using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    public class BorradorCaracteristicasController : Controller
    {
        private readonly IBorradorInmuebleRepository _borradorRepository;

        public BorradorCaracteristicasController(IBorradorInmuebleRepository borradorRepository)
        {
            _borradorRepository = borradorRepository;
        }

        [HttpGet("/Borrador/Caracteristicas/{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            string? correo = User.Identity?.Name;
            if (id <= 0 || string.IsNullOrWhiteSpace(correo))
                return NotFound();

            try
            {
                BorradorEdicionViewModel? modelo = await _borradorRepository.ObtenerParaEdicionAsync(correo, id);
                if (modelo == null)
                    return NotFound();

                return Ok(new
                {
                    recamaras = modelo.Recamaras,
                    banosCompletos = modelo.BanosCompletos,
                    mediosBanos = modelo.MediosBanos,
                    estacionamientos = modelo.Estacionamientos,
                    niveles = modelo.Niveles,
                    antiguedadAnos = modelo.AntiguedadAnos,
                    amenidades = modelo.AmenidadesDisponibles.Select(x => new
                    {
                        codigo = x.Codigo,
                        nombre = x.Nombre,
                        grupo = x.Grupo,
                        orden = x.Orden,
                        seleccionada = x.Seleccionada
                    })
                });
            }
            catch (SqlException)
            {
                return NotFound();
            }
        }
    }
}
