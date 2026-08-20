using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class BorradorInmuebleRepository : IBorradorInmuebleRepository
    {
        private readonly string _cadenaSQL;

        public BorradorInmuebleRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<int> CrearAsync(string correoAutenticado, decimal lat, decimal lng, int idTipo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_CrearBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;

            SqlParameter latParam = cmd.Parameters.Add("@lat", SqlDbType.Decimal);
            latParam.Precision = 10;
            latParam.Scale = 6;
            latParam.Value = lat;

            SqlParameter lngParam = cmd.Parameters.Add("@lng", SqlDbType.Decimal);
            lngParam.Precision = 10;
            lngParam.Scale = 6;
            lngParam.Value = lng;

            cmd.Parameters.Add("@idTipo", SqlDbType.Int).Value = idTipo;

            SqlParameter idParam = cmd.Parameters.Add("@idInmueble", SqlDbType.Int);
            idParam.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(idParam.Value);
        }

        public async Task<BorradorEdicionViewModel?> ObtenerParaEdicionAsync(string correoAutenticado, int idInmueble)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ObtenerBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            if (!await dr.ReadAsync())
                return null;

            return new BorradorEdicionViewModel
            {
                IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                IdCuenta = Convert.ToInt32(dr["IdCuenta"]),
                IdAsesor = Convert.ToInt32(dr["idAsesor"]),
                Direccion = dr["direccion"] == DBNull.Value ? null : dr["direccion"].ToString(),
                Lat = dr["lat"] == DBNull.Value ? null : Convert.ToDecimal(dr["lat"]),
                Lng = dr["lng"] == DBNull.Value ? null : Convert.ToDecimal(dr["lng"]),
                IdTipo = dr["idTipo"] == DBNull.Value ? 0 : Convert.ToInt32(dr["idTipo"]),
                TipoNombre = dr["TipoNombre"] == DBNull.Value ? null : dr["TipoNombre"].ToString(),
                Terreno = dr["terreno"] == DBNull.Value ? null : Convert.ToDouble(dr["terreno"]),
                Construccion = dr["construccion"] == DBNull.Value ? null : Convert.ToDouble(dr["construccion"]),
                Precio = dr["precio"] == DBNull.Value ? null : Convert.ToDecimal(dr["precio"]),
                Observaciones = dr["observaciones"] == DBNull.Value ? null : dr["observaciones"].ToString(),
                NotasPrivadas = dr["contacto_a"] == DBNull.Value ? null : dr["contacto_a"].ToString(),
                Imagenes = dr["Imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Imagenes"]),
                EstadoCodigo = dr["EstadoCodigo"] == DBNull.Value ? "BORRADOR" : dr["EstadoCodigo"].ToString() ?? "BORRADOR",
                VisibilidadCodigo = dr["VisibilidadCodigo"] == DBNull.Value ? "CUENTA" : dr["VisibilidadCodigo"].ToString() ?? "CUENTA",
                FechaUltimaEdicionUtc = dr["FechaUltimaEdicionUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaUltimaEdicionUtc"])
            };
        }

        public async Task GuardarAsync(string correoAutenticado, BorradorEdicionViewModel modelo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_GuardarBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = modelo.IdInmueble;
            cmd.Parameters.Add("@direccion", SqlDbType.VarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.Direccion) ? DBNull.Value : modelo.Direccion.Trim();
            cmd.Parameters.Add("@idTipo", SqlDbType.Int).Value = modelo.IdTipo;
            cmd.Parameters.Add("@terreno", SqlDbType.Float).Value = modelo.Terreno.HasValue ? modelo.Terreno.Value : DBNull.Value;
            cmd.Parameters.Add("@construccion", SqlDbType.Float).Value = modelo.Construccion.HasValue ? modelo.Construccion.Value : DBNull.Value;

            SqlParameter precio = cmd.Parameters.Add("@precio", SqlDbType.Decimal);
            precio.Precision = 18;
            precio.Scale = 2;
            precio.Value = modelo.Precio.HasValue ? modelo.Precio.Value : DBNull.Value;

            cmd.Parameters.Add("@observaciones", SqlDbType.VarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.Observaciones) ? DBNull.Value : modelo.Observaciones.Trim();
            cmd.Parameters.Add("@notasPrivadas", SqlDbType.VarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.NotasPrivadas) ? DBNull.Value : modelo.NotasPrivadas.Trim();

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
