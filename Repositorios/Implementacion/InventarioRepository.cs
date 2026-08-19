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
    }
}
