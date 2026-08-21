using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class PublicacionBorradorRepository : IPublicacionBorradorRepository
    {
        private readonly string _cadenaSQL;

        public PublicacionBorradorRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task PublicarAsync(string correoAutenticado, int idInmueble)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_PublicarBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
