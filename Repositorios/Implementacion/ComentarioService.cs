using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class ComentarioService : IComentarioService
    {
        private readonly string _connectionString;

        public ComentarioService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("cadenaSQL");
        }

        public async Task<List<Comentario>> GetComentariosActivos()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var comentarios = new List<Comentario>();
                SqlCommand cmd = new SqlCommand("RSMAPS_GetComentariosActivos", connection);
                cmd.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        comentarios.Add(new Comentario
                        {
                            IdComentario = Convert.ToInt32(reader["IdComentario"]),
                            Nombre = reader["Nombre"].ToString(),
                            Telefono = reader["Telefono"].ToString(),
                            ComentarioTexto = reader["Comentario"].ToString(),
                            FechaComentario = Convert.ToDateTime(reader["FechaComentario"]),
                            Planx = reader["Planx"].ToString(),
                            FechaExpiracion = Convert.ToDateTime(reader["FechaExpiracion"]),
                            Activo = Convert.ToBoolean(reader["Activo"])
                        });
                    }
                }

                return comentarios;
            }
        }

        public async Task<bool> RegistrarComentario(Comentario comentario)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("RSMAPS_InsertComentario", connection);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Nombre", comentario.Nombre);
                cmd.Parameters.AddWithValue("@Telefono", comentario.Telefono);
                cmd.Parameters.AddWithValue("@Comentario", comentario.ComentarioTexto);
                cmd.Parameters.AddWithValue("@Plan", comentario.Planx);
                cmd.Parameters.AddWithValue("@FechaExpiracion", comentario.FechaExpiracion);

                await connection.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

}
