using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace maps4.Repositorios.Implementacion
{
    public class ZonaRepository : IZonaRepository
    {
        private readonly string _cadenaSQL;

        public ZonaRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<ZonaResumenViewModel>> ListarAsync(string correo)
        {
            List<ZonaResumenViewModel> lista = new();
            using SqlConnection conexion = new(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new("dbo.RSMAPS_sp_ListarZonasCuenta", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                lista.Add(new ZonaResumenViewModel
                {
                    IdZona = Convert.ToInt32(dr["IdZona"]),
                    Codigo = dr["Codigo"].ToString() ?? string.Empty,
                    Nombre = dr["Nombre"].ToString() ?? string.Empty,
                    Descripcion = dr["Descripcion"] == DBNull.Value ? null : dr["Descripcion"].ToString(),
                    Prioridad = Convert.ToInt32(dr["Prioridad"]),
                    ColorHex = dr["ColorHex"] == DBNull.Value ? null : dr["ColorHex"].ToString(),
                    Activa = Convert.ToBoolean(dr["Activa"]),
                    Poligonos = Convert.ToInt32(dr["Poligonos"]),
                    Alias = Convert.ToInt32(dr["Alias"]),
                    Inmuebles = Convert.ToInt32(dr["Inmuebles"]),
                    Principales = dr["Principales"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Principales"])
                });
            }

            return lista;
        }

        public async Task<ZonaEdicionViewModel?> ObtenerAsync(string correo, int idZona)
        {
            using SqlConnection conexion = new(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new("dbo.RSMAPS_sp_ObtenerZonaEdicion", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;
            cmd.Parameters.Add("@idZona", SqlDbType.Int).Value = idZona;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            if (!await dr.ReadAsync())
                return null;

            ZonaEdicionViewModel zona = new()
            {
                IdZona = Convert.ToInt32(dr["IdZona"]),
                Codigo = dr["Codigo"].ToString() ?? string.Empty,
                Nombre = dr["Nombre"].ToString() ?? string.Empty,
                Descripcion = dr["Descripcion"] == DBNull.Value ? null : dr["Descripcion"].ToString(),
                Prioridad = Convert.ToInt32(dr["Prioridad"]),
                ColorHex = dr["ColorHex"] == DBNull.Value ? null : dr["ColorHex"].ToString(),
                Activa = Convert.ToBoolean(dr["Activa"])
            };

            if (dr["VerticesJson"] != DBNull.Value)
            {
                string json = dr["VerticesJson"].ToString() ?? "[]";
                zona.Vertices = JsonSerializer.Deserialize<List<ZonaVerticeViewModel>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            return zona;
        }

        public async Task<int> GuardarAsync(string correo, ZonaGuardarRequest request)
        {
            if (request.Vertices == null || request.Vertices.Count < 3)
                throw new ArgumentException("La zona necesita al menos tres vertices.");

            string verticesJson = JsonSerializer.Serialize(request.Vertices);
            string poligonoWkt = ConstruirPoligonoWkt(request.Vertices);

            using SqlConnection conexion = new(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new("dbo.RSMAPS_sp_GuardarZona", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;
            cmd.Parameters.Add("@idZona", SqlDbType.Int).Value = request.IdZona.HasValue ? request.IdZona.Value : DBNull.Value;
            cmd.Parameters.Add("@codigo", SqlDbType.VarChar, 60).Value = request.Codigo.Trim().ToUpperInvariant();
            cmd.Parameters.Add("@nombre", SqlDbType.NVarChar, 120).Value = request.Nombre.Trim();
            cmd.Parameters.Add("@descripcion", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(request.Descripcion) ? DBNull.Value : request.Descripcion.Trim();
            cmd.Parameters.Add("@prioridad", SqlDbType.Int).Value = request.Prioridad;
            cmd.Parameters.Add("@colorHex", SqlDbType.Char, 7).Value = string.IsNullOrWhiteSpace(request.ColorHex) ? DBNull.Value : request.ColorHex.Trim();
            cmd.Parameters.Add("@verticesJson", SqlDbType.NVarChar, -1).Value = verticesJson;
            cmd.Parameters.Add("@poligonoWkt", SqlDbType.NVarChar, -1).Value = poligonoWkt;

            object? resultado = await cmd.ExecuteScalarAsync();
            if (resultado == null || resultado == DBNull.Value)
                throw new InvalidOperationException("No se obtuvo el identificador de la zona guardada.");

            return Convert.ToInt32(resultado);
        }

        private static string ConstruirPoligonoWkt(IReadOnlyList<ZonaVerticeViewModel> vertices)
        {
            List<ZonaVerticeViewModel> puntos = vertices.ToList();
            ZonaVerticeViewModel primero = puntos[0];
            ZonaVerticeViewModel ultimo = puntos[^1];

            if (Math.Abs(primero.Lat - ultimo.Lat) > 0.0000001 || Math.Abs(primero.Lng - ultimo.Lng) > 0.0000001)
            {
                puntos.Add(new ZonaVerticeViewModel { Lat = primero.Lat, Lng = primero.Lng });
            }

            string coordenadas = string.Join(", ", puntos.Select(p =>
                $"{p.Lng.ToString("0.########", CultureInfo.InvariantCulture)} {p.Lat.ToString("0.########", CultureInfo.InvariantCulture)}"));

            return $"POLYGON(({coordenadas}))";
        }
    }
}
