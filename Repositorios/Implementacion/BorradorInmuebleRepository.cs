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
    }
}
