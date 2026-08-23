using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar")]
public class RadarController : ControllerBase
{
    private readonly IGenericRepository<Inmueble> _inmuebles;
    private readonly IGenericRepository<TipoPropiedad> _tipos;

    public RadarController(
        IGenericRepository<Inmueble> inmuebles,
        IGenericRepository<TipoPropiedad> tipos)
    {
        _inmuebles = inmuebles;
        _tipos = tipos;
    }

    [HttpGet("inmuebles")]
    public async Task<ActionResult<IEnumerable<RadarInmuebleDto>>> GetInmuebles()
    {
        var inmuebles = await _inmuebles.Lista();
        var tipos = await _tipos.Lista();

        var nombresTipo = tipos
            .GroupBy(x => x.idTipoPropiedad)
            .ToDictionary(g => g.Key, g => g.First().nombre);

        var resultado = inmuebles.Select(i => new RadarInmuebleDto
        {
            IdInmueble = i.IdInmueble,
            IdTipo = i.IdTipo,
            Tipo = i.IdTipo.HasValue && nombresTipo.TryGetValue(i.IdTipo.Value, out var nombreTipo)
                ? nombreTipo
                : null,
            Direccion = i.Direccion,
            Lat = i.Lat,
            Lng = i.Lng,
            Precio = i.Precio.HasValue ? Convert.ToDecimal(i.Precio.Value) : null,
            TerrenoM2 = i.Terreno.HasValue ? Convert.ToDecimal(i.Terreno.Value) : null,
            ConstruccionM2 = i.Construccion.HasValue ? Convert.ToDecimal(i.Construccion.Value) : null,
            Recamaras = i.Recamaras,
            BanosCompletos = i.BanosCompletos,
            MediosBanos = i.MediosBanos,
            Estacionamientos = i.Estacionamientos,
            Niveles = i.Niveles,
            Amenidades = i.AmenidadesCsv,
            Observaciones = i.Observaciones,
            Link = i.Link,
            Imagenes = i.Imagenes
        })
        .OrderBy(x => x.IdInmueble)
        .ToList();

        return Ok(resultado);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            servicio = "RSMaps Radar API",
            estado = "ok",
            fechaUtc = DateTime.UtcNow
        });
    }
}
