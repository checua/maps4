using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;
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

            if (dr["AliasesJson"] != DBNull.Value)
            {
                string aliasJson = dr["AliasesJson"].ToString() ?? "[]";
                zona.Aliases = LeerAliases(aliasJson);
            }

            return zona;
        }

        public async Task<ZonaCoberturaActualViewModel?> ObtenerCoberturaActualAsync(string correo)
        {
            const string sql = @"
;WITH CuentaActual AS
(
    SELECT TOP (1)
        cu.IdCuenta
    FROM dbo.RSMAPS_Usuario u
    INNER JOIN dbo.RSMAPS_CuentaUsuario cu
        ON cu.IdAsesor = u.idAsesor
       AND cu.Activo = 1
    INNER JOIN dbo.RSMAPS_Cuenta c
        ON c.IdCuenta = cu.IdCuenta
       AND c.Activo = 1
    INNER JOIN dbo.RSMAPS_RolPermiso rp
        ON rp.RolCodigo = cu.RolCodigo
       AND rp.PermisoCodigo = 'ZONA_ADMINISTRAR'
    INNER JOIN dbo.RSMAPS_Permiso p
        ON p.Codigo = rp.PermisoCodigo
       AND p.Activo = 1
    WHERE u.correo = @correo
    ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta
)
SELECT zp.VerticesJson
FROM CuentaActual ca
INNER JOIN dbo.RSMAPS_Zona z
    ON z.IdCuenta = ca.IdCuenta
   AND z.Activa = 1
INNER JOIN dbo.RSMAPS_ZonaPoligono zp
    ON zp.IdZona = z.IdZona
   AND zp.Activo = 1;";

            using SqlConnection conexion = new(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new(sql, conexion);
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;

            List<ZonaVerticeViewModel> vertices = new();
            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                if (dr["VerticesJson"] == DBNull.Value)
                    continue;

                try
                {
                    string json = dr["VerticesJson"].ToString() ?? "[]";
                    List<ZonaVerticeViewModel>? puntos = JsonSerializer.Deserialize<List<ZonaVerticeViewModel>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (puntos != null)
                    {
                        vertices.AddRange(puntos.Where(v =>
                            double.IsFinite(v.Lat) && double.IsFinite(v.Lng) &&
                            v.Lat is >= -90 and <= 90 && v.Lng is >= -180 and <= 180));
                    }
                }
                catch (JsonException)
                {
                    // Un poligono con JSON editable dañado no debe romper ZonaAdmin.
                }
            }

            if (vertices.Count == 0)
                return null;

            return new ZonaCoberturaActualViewModel
            {
                MinLat = vertices.Min(v => v.Lat),
                MaxLat = vertices.Max(v => v.Lat),
                MinLng = vertices.Min(v => v.Lng),
                MaxLng = vertices.Max(v => v.Lng)
            };
        }

        public async Task<int> GuardarAsync(string correo, ZonaGuardarRequest request)
        {
            if (request.Vertices == null || request.Vertices.Count < 3)
                throw new ArgumentException("La zona necesita al menos tres vertices.");

            string verticesJson = JsonSerializer.Serialize(request.Vertices);
            string poligonoWkt = ConstruirPoligonoWkt(request.Vertices);

            var aliases = (request.Aliases ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length >= 2 && x.Length <= 150)
                .Select(x => new
                {
                    alias = x,
                    aliasNormalizado = NormalizarAlias(x)
                })
                .Where(x => x.aliasNormalizado.Length >= 2)
                .GroupBy(x => x.aliasNormalizado, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            string aliasesJson = JsonSerializer.Serialize(aliases);

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
            cmd.Parameters.Add("@aliasesJson", SqlDbType.NVarChar, -1).Value = aliasesJson;

            object? resultado = await cmd.ExecuteScalarAsync();
            if (resultado == null || resultado == DBNull.Value)
                throw new InvalidOperationException("No se obtuvo el identificador de la zona guardada.");

            return Convert.ToInt32(resultado);
        }

        private static List<string> LeerAliases(string json)
        {
            List<string> aliases = new();
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                foreach (JsonElement item in document.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("Alias", out JsonElement aliasElement) ||
                        item.TryGetProperty("alias", out aliasElement))
                    {
                        string? valor = aliasElement.GetString();
                        if (!string.IsNullOrWhiteSpace(valor))
                            aliases.Add(valor.Trim());
                    }
                }
            }
            catch (JsonException)
            {
                return new();
            }

            return aliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string NormalizarAlias(string value)
        {
            string texto = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new();
            bool espacioPendiente = false;

            foreach (char c in texto)
            {
                UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(c))
                {
                    if (espacioPendiente && sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(c);
                    espacioPendiente = false;
                }
                else
                {
                    espacioPendiente = true;
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
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
