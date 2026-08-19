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
            List<InventarioInmuebleViewModel> lista = new();

            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ListaInmueblesCuenta", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@IdCuenta", SqlDbType.Int).Value = idCuenta;
            cmd.Parameters.Add("@IdAsesor", SqlDbType.Int).Value = idAsesor;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();

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
                    Imagenes = dr["imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["imagenes"]),
                    EstadoCodigo = dr["EstadoCodigo"] == DBNull.Value ? string.Empty : dr["EstadoCodigo"].ToString() ?? string.Empty,
                    VisibilidadCodigo = dr["VisibilidadCodigo"] == DBNull.Value ? string.Empty : dr["VisibilidadCodigo"].ToString() ?? string.Empty,
                    FechaPublicacionUtc = dr["FechaPublicacionUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaPublicacionUtc"]),
                    FechaUltimoCambioEstadoUtc = dr["FechaUltimoCambioEstadoUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaUltimoCambioEstadoUtc"])
                });
            }

            return lista;
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
                string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo;

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
