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



        public async Task<List<Comentario>> GetComentariosActivos(int? tipoInmuebleSolicitado)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var comentarios = new List<Comentario>();
                SqlCommand cmd = new SqlCommand("RSMAPS_GetComentariosActivos", connection);
                cmd.CommandType = CommandType.StoredProcedure;

                // Añadir el parámetro tipoInmuebleSolicitado, se enviará NULL si no se especifica
                cmd.Parameters.AddWithValue("@tipoInmuebleSolicitado", (object)tipoInmuebleSolicitado ?? DBNull.Value);

                await connection.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        comentarios.Add(new Comentario
                        {
                            IdComentario = Convert.ToInt32(reader["IdComentario"]),
                            Correo = reader["Correo"].ToString(),
                            Nombre = reader["Nombre"].ToString(),
                            Telefono = reader["Telefono"].ToString(),
                            ComentarioTexto = reader["ComentarioTexto"].ToString(),
                            FechaComentario = Convert.ToDateTime(reader["FechaComentario"]),
                            Nivel = reader["Nivel"].ToString(),
                            FechaExpiracion = Convert.ToDateTime(reader["FechaExpiracion"]),
                            Activo = true, // Solo se devuelven los activos

                            // Manejar posibles valores nulos para TipoInmuebleSolicitado
                            TipoInmuebleSolicitado = reader["TipoInmuebleSolicitado"] != DBNull.Value
                                ? Convert.ToInt32(reader["TipoInmuebleSolicitado"])
                                : (int?)null
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
                SqlCommand cmd = new SqlCommand("RSMAPS_SaveComentario", connection);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Correo", comentario.Correo);
                cmd.Parameters.AddWithValue("@ComentarioTexto", comentario.ComentarioTexto);
                cmd.Parameters.AddWithValue("@Nivel", comentario.Nivel);
                cmd.Parameters.AddWithValue("@TipoInmuebleSolicitado", comentario.TipoInmuebleSolicitado);

                await connection.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected != 0;
            }
        }
    }
}