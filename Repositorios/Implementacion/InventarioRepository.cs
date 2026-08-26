using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class InventarioRepository : IInventarioRepository
    {
        private readonly string _cadenaSQL;

        public InventarioRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ListaInmueblesCuenta", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@IdCuenta", SqlDbType.Int).Value = idCuenta;
            cmd.Parameters.Add("@IdAsesor", SqlDbType.Int).Value = idAsesor;

            var lista = await LeerInventarioAsync(cmd);
            await CompletarDatosEstructuradosAsync(conexion, lista);
            return lista;
        }

        public async Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ListaInmueblesAutorizados", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;

            var lista = await LeerInventarioAsync(cmd);
            await CompletarDatosEstructuradosAsync(conexion, lista);
            return lista;
        }

        public async Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ContextoAutorizacionActual", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            if (!await dr.ReadAsync())
                return null;

            return new InventarioAutorizacionContexto
            {
                IdCuenta = Convert.ToInt32(dr["IdCuenta"]),
                CuentaNombre = dr["CuentaNombre"] == DBNull.Value ? string.Empty : dr["CuentaNombre"].ToString() ?? string.Empty,
                TipoCuenta = dr["TipoCuenta"] == DBNull.Value ? string.Empty : dr["TipoCuenta"].ToString() ?? string.Empty,
                IdAsesor = Convert.ToInt32(dr["IdAsesor"]),
                RolCodigo = dr["RolCodigo"] == DBNull.Value ? string.Empty : dr["RolCodigo"].ToString() ?? string.Empty,
                PuedeVerCuenta = dr["PuedeVerCuenta"] != DBNull.Value && Convert.ToBoolean(dr["PuedeVerCuenta"]),
                PuedeCambiarEstadoCuenta = dr["PuedeCambiarEstadoCuenta"] != DBNull.Value && Convert.ToBoolean(dr["PuedeCambiarEstadoCuenta"]),
                PuedeCerrarOperacionCuenta = dr["PuedeCerrarOperacionCuenta"] != DBNull.Value && Convert.ToBoolean(dr["PuedeCerrarOperacionCuenta"]),
                PuedeCapturarParaOtro = dr["PuedeCapturarParaOtro"] != DBNull.Value && Convert.ToBoolean(dr["PuedeCapturarParaOtro"])
            };
        }

        private static async Task<List<InventarioInmuebleViewModel>> LeerInventarioAsync(SqlCommand cmd)
        {
            List<InventarioInmuebleViewModel> lista = new();

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            HashSet<string> columnas = Enumerable
                .Range(0, dr.FieldCount)
                .Select(dr.GetName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string? ValorOpcional(string nombre)
            {
                if (!columnas.Contains(nombre) || dr[nombre] == DBNull.Value)
                    return null;
                return dr[nombre].ToString();
            }

            while (await dr.ReadAsync())
            {
                string nombres = dr["nombres"] == DBNull.Value ? string.Empty : dr["nombres"].ToString() ?? string.Empty;
                string aPaterno = dr["aPaterno"] == DBNull.Value ? string.Empty : dr["aPaterno"].ToString() ?? string.Empty;

                lista.Add(new InventarioInmuebleViewModel
                {
                    IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                    IdCuenta = Convert.ToInt32(dr["IdCuenta"]),
                    IdAsesor = Convert.ToInt32(dr["idAsesor"]),
                    AsesorNombre = $"{nombres} {aPaterno}".Trim(),
                    CorreoAsesor = dr["correo"] == DBNull.Value ? null : dr["correo"].ToString(),
                    Direccion = dr["direccion"] == DBNull.Value ? null : dr["direccion"].ToString(),
                    Lat = dr["lat"] == DBNull.Value ? null : Convert.ToDecimal(dr["lat"]),
                    Lng = dr["lng"] == DBNull.Value ? null : Convert.ToDecimal(dr["lng"]),
                    IdTipo = dr["idTipo"] == DBNull.Value ? null : Convert.ToInt32(dr["idTipo"]),
                    TipoNombre = dr["TipoNombre"] == DBNull.Value ? null : dr["TipoNombre"].ToString(),
                    Telefono = dr["telefono"] == DBNull.Value ? null : dr["telefono"].ToString(),
                    Terreno = dr["terreno"] == DBNull.Value ? null : Convert.ToDouble(dr["terreno"]),
                    Construccion = dr["construccion"] == DBNull.Value ? null : Convert.ToDouble(dr["construccion"]),
                    Precio = dr["precio"] == DBNull.Value ? null : Convert.ToDouble(dr["precio"]),
                    Observaciones = dr["observaciones"] == DBNull.Value ? null : dr["observaciones"].ToString(),
                    Link = ObtenerString(dr, "link"),
                    Imagenes = dr["imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["imagenes"]),
                    EstadoCodigo = dr["EstadoCodigo"] == DBNull.Value ? string.Empty : dr["EstadoCodigo"].ToString() ?? string.Empty,
                    VisibilidadCodigo = dr["VisibilidadCodigo"] == DBNull.Value ? string.Empty : dr["VisibilidadCodigo"].ToString() ?? string.Empty,
                    FechaPublicacionUtc = dr["FechaPublicacionUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaPublicacionUtc"]),
                    FechaUltimoCambioEstadoUtc = dr["FechaUltimoCambioEstadoUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaUltimoCambioEstadoUtc"]),
                    ZonaPrincipalCodigo = ValorOpcional("ZonaPrincipalCodigo"),
                    ZonaPrincipalNombre = ValorOpcional("ZonaPrincipalNombre"),
                    ZonasCsv = ValorOpcional("ZonasCsv")
                });
            }

            return lista;
        }

        private static async Task CompletarDatosEstructuradosAsync(
            SqlConnection conexion,
            List<InventarioInmuebleViewModel> lista)
        {
            if (lista.Count == 0)
                return;

            var porId = lista.ToDictionary(x => x.IdInmueble);
            var nombresParametros = lista
                .Select((_, index) => $"@id{index}")
                .ToArray();

            string sql = $@"
SELECT
    i.idInmueble,
    i.Recamaras,
    i.BanosCompletos,
    i.MediosBanos,
    i.Estacionamientos,
    i.Niveles,
    i.AntiguedadAnos,
    a.AmenidadesCsv
FROM dbo.RSMAPS_Inmueble i
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(varchar(max), ia.AmenidadCodigo), ',') AS AmenidadesCsv
    FROM dbo.RSMAPS_InmuebleAmenidad ia
    WHERE ia.IdInmueble = i.idInmueble
) a
WHERE i.idInmueble IN ({string.Join(",", nombresParametros)});";

            using SqlCommand cmd = new SqlCommand(sql, conexion);
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add(nombresParametros[i], SqlDbType.Int).Value = lista[i].IdInmueble;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                int id = Convert.ToInt32(dr["idInmueble"]);
                if (!porId.TryGetValue(id, out var inmueble))
                    continue;

                inmueble.Recamaras = dr["Recamaras"] == DBNull.Value ? null : Convert.ToInt32(dr["Recamaras"]);
                inmueble.BanosCompletos = dr["BanosCompletos"] == DBNull.Value ? null : Convert.ToInt32(dr["BanosCompletos"]);
                inmueble.MediosBanos = dr["MediosBanos"] == DBNull.Value ? null : Convert.ToInt32(dr["MediosBanos"]);
                inmueble.Estacionamientos = dr["Estacionamientos"] == DBNull.Value ? null : Convert.ToInt32(dr["Estacionamientos"]);
                inmueble.Niveles = dr["Niveles"] == DBNull.Value ? null : Convert.ToInt32(dr["Niveles"]);
                inmueble.AntiguedadAnos = dr["AntiguedadAnos"] == DBNull.Value ? null : Convert.ToInt32(dr["AntiguedadAnos"]);
                inmueble.AmenidadesCsv = dr["AmenidadesCsv"] == DBNull.Value ? null : dr["AmenidadesCsv"].ToString();
            }
        }

        private static string? ObtenerString(SqlDataReader dr, string nombreColumna)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (!string.Equals(dr.GetName(i), nombreColumna, StringComparison.OrdinalIgnoreCase))
                    continue;

                return dr.IsDBNull(i) ? null : dr.GetValue(i)?.ToString();
            }

            return null;
        }

        public async Task CambiarEstadoOVisibilidadAsync(
            int idInmueble,
            string correo,
            string? estadoNuevo,
            string? visibilidadNueva,
            string? motivo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_CambiarEstadoInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;
            cmd.Parameters.Add("@EstadoNuevo", SqlDbType.VarChar, 20).Value =
                string.IsNullOrWhiteSpace(estadoNuevo) ? DBNull.Value : estadoNuevo;
            cmd.Parameters.Add("@VisibilidadNueva", SqlDbType.VarChar, 20).Value =
                string.IsNullOrWhiteSpace(visibilidadNueva) ? DBNull.Value : visibilidadNueva;
            cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim();

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CerrarOperacionAsync(
            int idInmueble,
            string correo,
            string tipoOperacion,
            decimal precioCierre,
            DateTime fechaCierreUtc,
            string? notasCierre)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_CerrarOperacionInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo;
            cmd.Parameters.Add("@TipoOperacion", SqlDbType.VarChar, 10).Value = tipoOperacion;

            SqlParameter precio = cmd.Parameters.Add("@PrecioCierre", SqlDbType.Decimal);
            precio.Precision = 18;
            precio.Scale = 2;
            precio.Value = precioCierre;

            cmd.Parameters.Add("@FechaCierreUtc", SqlDbType.DateTime2).Value = fechaCierreUtc;
            cmd.Parameters.Add("@NotasCierre", SqlDbType.NVarChar, 1000).Value =
                string.IsNullOrWhiteSpace(notasCierre) ? DBNull.Value : notasCierre.Trim();

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
