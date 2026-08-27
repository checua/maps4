using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;

namespace maps4.Controllers
{
    [ApiController]
    [Route("api/radar/debug")]
    public class RadarMatchingDebugController : ControllerBase
    {
        private readonly IInventarioRepository _inventarioRepository;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public RadarMatchingDebugController(
            IInventarioRepository inventarioRepository,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _inventarioRepository = inventarioRepository;
            _configuration = configuration;
            _environment = environment;
        }

        // Diagnóstico exclusivamente local/development para revisar qué datos
        // de zona está viendo realmente el matching. Nunca se expone fuera de loopback.
        [AllowAnonymous]
        [HttpGet("inmueble/{id:int}")]
        public async Task<IActionResult> Inmueble(int id)
        {
            if (!EsDiagnosticoLocalPermitido())
                return NotFound();

            string? correo = _configuration["RadarMatching:CorreoInventario"];
            if (string.IsNullOrWhiteSpace(correo))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Falta RadarMatching:CorreoInventario.");

            var inventario = await _inventarioRepository.ListarAutorizadosAsync(correo);
            var inmueble = inventario.FirstOrDefault(x => x.IdInmueble == id);
            if (inmueble is null)
                return NotFound();

            return Ok(new
            {
                inmueble.IdInmueble,
                inmueble.TipoNombre,
                inmueble.Precio,
                inmueble.Direccion,
                inmueble.ZonaPrincipalCodigo,
                inmueble.ZonaPrincipalNombre,
                inmueble.ZonasCsv,
                inmueble.Observaciones,
                inmueble.Recamaras,
                inmueble.BanosCompletos,
                inmueble.AmenidadesCsv,
                inmueble.Lat,
                inmueble.Lng,
                inmueble.EstadoCodigo
            });
        }

        // Diagnóstico espacial: compara las coordenadas reales del inmueble contra
        // los polígonos activos de la misma cuenta, sin modificar ninguna asignación.
        [AllowAnonymous]
        [HttpGet("inmueble/{id:int}/zonas-espaciales")]
        public async Task<IActionResult> ZonasEspaciales(int id)
        {
            if (!EsDiagnosticoLocalPermitido())
                return NotFound();

            string cadenaSQL = _configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cadenaSQL))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Falta ConnectionStrings:cadenaSQL.");

            using SqlConnection conexion = new(cadenaSQL);
            await conexion.OpenAsync();

            const string sql = @"
SELECT
    i.idInmueble,
    i.IdCuenta,
    TRY_CONVERT(float, i.lat) AS Lat,
    TRY_CONVERT(float, i.lng) AS Lng,
    z.IdZona,
    z.Codigo,
    z.Nombre,
    z.Prioridad,
    zp.IdZonaPoligono,
    zp.Nombre AS PoligonoNombre,
    CAST(CASE WHEN iz.IdInmueble IS NULL THEN 0 ELSE 1 END AS bit) AS YaAsignada,
    iz.Origen,
    iz.EsPrincipal
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Zona z
    ON z.IdCuenta = i.IdCuenta
   AND z.Activa = 1
INNER JOIN dbo.RSMAPS_ZonaPoligono zp
    ON zp.IdZona = z.IdZona
   AND zp.Activo = 1
LEFT JOIN dbo.RSMAPS_InmuebleZona iz
    ON iz.IdInmueble = i.idInmueble
   AND iz.IdZona = z.IdZona
WHERE i.idInmueble = @id
  AND TRY_CONVERT(float, i.lat) BETWEEN -90 AND 90
  AND TRY_CONVERT(float, i.lng) BETWEEN -180 AND 180
  AND zp.Poligono.STIntersects(
        geometry::Point(
            TRY_CONVERT(float, i.lng),
            TRY_CONVERT(float, i.lat),
            4326)) = 1
ORDER BY z.Prioridad DESC, z.Nombre, zp.Orden, zp.IdZonaPoligono;";

            using SqlCommand cmd = new(sql, conexion)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;

            var coincidencias = new List<object>();
            int? idCuenta = null;
            double? lat = null;
            double? lng = null;

            using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
            {
                while (await dr.ReadAsync())
                {
                    idCuenta ??= Convert.ToInt32(dr["IdCuenta"]);
                    lat ??= dr["Lat"] == DBNull.Value ? null : Convert.ToDouble(dr["Lat"]);
                    lng ??= dr["Lng"] == DBNull.Value ? null : Convert.ToDouble(dr["Lng"]);

                    coincidencias.Add(new
                    {
                        idZona = Convert.ToInt32(dr["IdZona"]),
                        codigo = dr["Codigo"]?.ToString(),
                        nombre = dr["Nombre"]?.ToString(),
                        prioridad = Convert.ToInt32(dr["Prioridad"]),
                        idZonaPoligono = Convert.ToInt32(dr["IdZonaPoligono"]),
                        poligonoNombre = dr["PoligonoNombre"] == DBNull.Value ? null : dr["PoligonoNombre"].ToString(),
                        yaAsignada = dr["YaAsignada"] != DBNull.Value && Convert.ToBoolean(dr["YaAsignada"]),
                        origen = dr["Origen"] == DBNull.Value ? null : dr["Origen"].ToString(),
                        esPrincipal = dr["EsPrincipal"] != DBNull.Value && Convert.ToBoolean(dr["EsPrincipal"])
                    });
                }
            }

            if (idCuenta is null)
            {
                // Distingue entre inmueble inexistente/coordenadas inválidas y un punto
                // válido que simplemente no cae dentro de ningún polígono configurado.
                using SqlCommand info = new(@"
SELECT IdCuenta, TRY_CONVERT(float, lat) AS Lat, TRY_CONVERT(float, lng) AS Lng
FROM dbo.RSMAPS_Inmueble
WHERE idInmueble = @id;", conexion);
                info.Parameters.Add("@id", SqlDbType.Int).Value = id;

                using SqlDataReader infoReader = await info.ExecuteReaderAsync();
                if (!await infoReader.ReadAsync())
                    return NotFound();

                idCuenta = infoReader["IdCuenta"] == DBNull.Value ? null : Convert.ToInt32(infoReader["IdCuenta"]);
                lat = infoReader["Lat"] == DBNull.Value ? null : Convert.ToDouble(infoReader["Lat"]);
                lng = infoReader["Lng"] == DBNull.Value ? null : Convert.ToDouble(infoReader["Lng"]);
            }

            return Ok(new
            {
                idInmueble = id,
                idCuenta,
                lat,
                lng,
                totalPoligonosCoincidentes = coincidencias.Count,
                coincidencias
            });
        }

        private bool EsDiagnosticoLocalPermitido()
        {
            if (!_environment.IsDevelopment())
                return false;

            IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            return ip is not null && IPAddress.IsLoopback(ip);
        }
    }
}
