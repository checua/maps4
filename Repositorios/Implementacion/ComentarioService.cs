using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class ComentarioService : IComentarioService
    {
        private const int MaxIntentosSql = 3;
        private readonly string _connectionString;

        public ComentarioService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<Comentario>> GetComentariosActivos(int? tipoInmuebleSolicitado)
        {
            return await EjecutarConReintentoAsync(async connection =>
            {
                var comentarios = new List<Comentario>();

                using SqlCommand cmd = new("RSMAPS_GetComentariosActivos", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.Add("@tipoInmuebleSolicitado", SqlDbType.Int).Value =
                    (object?)tipoInmuebleSolicitado ?? DBNull.Value;

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    comentarios.Add(new Comentario
                    {
                        IdComentario = Convert.ToInt32(reader["IdComentario"]),
                        Correo = reader["Correo"].ToString() ?? string.Empty,
                        Nombre = reader["Nombre"].ToString() ?? string.Empty,
                        Telefono = reader["Telefono"].ToString() ?? string.Empty,
                        ComentarioTexto = reader["ComentarioTexto"].ToString() ?? string.Empty,
                        FechaComentario = Convert.ToDateTime(reader["FechaComentario"]),
                        Nivel = reader["Nivel"].ToString() ?? string.Empty,
                        FechaExpiracion = Convert.ToDateTime(reader["FechaExpiracion"]),
                        Activo = true,
                        TipoInmuebleSolicitado = reader["TipoInmuebleSolicitado"] != DBNull.Value
                            ? Convert.ToInt32(reader["TipoInmuebleSolicitado"])
                            : (int?)null
                    });
                }

                return comentarios;
            });
        }

        public async Task<bool> RegistrarComentario(Comentario comentario)
        {
            return await EjecutarConReintentoAsync(async connection =>
            {
                using SqlCommand cmd = new("RSMAPS_SaveComentario", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.Add("@Correo", SqlDbType.NVarChar).Value = comentario.Correo;
                cmd.Parameters.Add("@ComentarioTexto", SqlDbType.NVarChar).Value = comentario.ComentarioTexto;
                cmd.Parameters.Add("@Nivel", SqlDbType.NVarChar).Value = comentario.Nivel;
                cmd.Parameters.Add("@TipoInmuebleSolicitado", SqlDbType.Int).Value =
                    (object?)comentario.TipoInmuebleSolicitado ?? DBNull.Value;

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected != 0;
            });
        }

        private async Task<T> EjecutarConReintentoAsync<T>(Func<SqlConnection, Task<T>> operacion)
        {
            Exception? ultimaExcepcion = null;

            for (int intento = 1; intento <= MaxIntentosSql; intento++)
            {
                try
                {
                    await using SqlConnection connection = new(_connectionString);
                    await connection.OpenAsync();
                    return await operacion(connection);
                }
                catch (SqlException ex) when (intento < MaxIntentosSql && EsErrorSqlTransitorio(ex))
                {
                    ultimaExcepcion = ex;
                    await Task.Delay(TimeSpan.FromSeconds(intento));
                }
                catch (TimeoutException ex) when (intento < MaxIntentosSql)
                {
                    ultimaExcepcion = ex;
                    await Task.Delay(TimeSpan.FromSeconds(intento));
                }
            }

            throw ultimaExcepcion ?? new InvalidOperationException("No fue posible completar la operación de comentarios en SQL Server.");
        }

        private static bool EsErrorSqlTransitorio(SqlException ex)
        {
            return ex.Number is 10054 or 258 or -2
                or 233 or 64 or 53 or 10053 or 10060
                or 40613 or 40197 or 40501
                or 10928 or 10929
                or 49918 or 49919 or 49920;
        }
    }
}
