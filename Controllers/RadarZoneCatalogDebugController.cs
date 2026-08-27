using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;

namespace maps4.Controllers
{
    [ApiController]
    [Route("api/radar/debug/zonas")]
    public class RadarZoneCatalogDebugController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public RadarZoneCatalogDebugController(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        // Diagnóstico exclusivamente local/development. Lista las zonas de una cuenta,
        // cuántos polígonos activos tienen y sus alias, sin modificar datos.
        [AllowAnonymous]
        [HttpGet("cuenta/{idCuenta:int}")]
        public async Task<IActionResult> Cuenta(int idCuenta)
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
    z.IdZona,
    z.Codigo,
    z.Nombre,
    z.Activa,
    z.Prioridad,
    COUNT(DISTINCT CASE WHEN zp.Activo = 1 THEN zp.IdZonaPoligono END) AS PoligonosActivos,
    STRING_AGG(CASE WHEN za.Activo = 1 THEN CONVERT(nvarchar(max), za.Alias) END, N' | ') AS Alias
FROM dbo.RSMAPS_Zona z
LEFT JOIN dbo.RSMAPS_ZonaPoligono zp
    ON zp.IdZona = z.IdZona
LEFT JOIN dbo.RSMAPS_ZonaAlias za
    ON za.IdZona = z.IdZona
WHERE z.IdCuenta = @idCuenta
GROUP BY z.IdZona, z.Codigo, z.Nombre, z.Activa, z.Prioridad
ORDER BY z.Activa DESC, z.Prioridad DESC, z.Nombre;";

            using SqlCommand cmd = new(sql, conexion)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;

            var zonas = new List<object>();
            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                zonas.Add(new
                {
                    idZona = Convert.ToInt32(dr["IdZona"]),
                    codigo = dr["Codigo"]?.ToString(),
                    nombre = dr["Nombre"]?.ToString(),
                    activa = dr["Activa"] != DBNull.Value && Convert.ToBoolean(dr["Activa"]),
                    prioridad = Convert.ToInt32(dr["Prioridad"]),
                    poligonosActivos = Convert.ToInt32(dr["PoligonosActivos"]),
                    alias = dr["Alias"] == DBNull.Value ? null : dr["Alias"].ToString()
                });
            }

            return Ok(new
            {
                idCuenta,
                totalZonas = zonas.Count,
                zonas
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
