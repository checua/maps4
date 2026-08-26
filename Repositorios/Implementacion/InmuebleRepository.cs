using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class InmuebleRepository : IGenericRepository<Inmueble>
    {
        private const int MaxIntentosSql = 3;
        private readonly string _cadenaSQL = "";

        public InmuebleRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<Inmueble>> Lista()
        {
            return await EjecutarConReintentoAsync(async conexion =>
            {
                List<Inmueble> lista = new();

                using SqlCommand cmd = new("RSMAPS_sp_ListaInmuebles", conexion)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using SqlDataReader dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                    lista.Add(MapearInmueblePublico(dr));

                return lista;
            });
        }

        public async Task<List<Inmueble>> GetInmuebleById(int inmuebleId)
        {
            return await EjecutarConReintentoAsync(async conexion =>
            {
                List<Inmueble> lista = new();

                using SqlCommand cmd = new("sp_GetInmuebleById", conexion)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = inmuebleId;

                using SqlDataReader dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                    lista.Add(MapearInmueblePublico(dr));

                return lista;
            });
        }

        private async Task<T> EjecutarConReintentoAsync<T>(Func<SqlConnection, Task<T>> operacion)
        {
            Exception? ultimaExcepcion = null;

            for (int intento = 1; intento <= MaxIntentosSql; intento++)
            {
                try
                {
                    await using SqlConnection conexion = new(_cadenaSQL);
                    await conexion.OpenAsync();
                    return await operacion(conexion);
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

            throw ultimaExcepcion ?? new InvalidOperationException("No fue posible completar la consulta a SQL Server.");
        }

        private static bool EsErrorSqlTransitorio(SqlException ex)
        {
            // Errores vistos durante conexiones inestables hacia Azure SQL:
            // 10054: conexión interrumpida por el host remoto.
            // 258: timeout durante el handshake SSL/TLS.
            // -2: timeout de SQL Client.
            if (ex.Number is 10054 or 258 or -2)
                return true;

            string mensaje = ex.Message.ToUpperInvariant();
            return mensaje.Contains("SSL")
                || mensaje.Contains("HANDSHAKE")
                || mensaje.Contains("TIEMPO DE ESPERA")
                || mensaje.Contains("TIMEOUT");
        }

        private static Inmueble MapearInmueblePublico(SqlDataReader dr)
        {
            return new Inmueble
            {
                IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                refInmobiliaria = dr["idInmobiliaria"] == DBNull.Value
                    ? null
                    : new Inmobiliaria()
                    {
                        idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                        nombre = dr["nombre"].ToString()
                    },
                RefUsuario = new Usuario()
                {
                    idAsesor = Convert.ToInt32(dr["idAsesor"]),
                    nombres = dr["nombres"].ToString(),
                    aPaterno = dr["aPaterno"].ToString(),
                    correo = dr["correo"].ToString(),
                },
                Direccion = dr["direccion"].ToString(),
                Lat = dr["lat"] as decimal?,
                Lng = dr["lng"] as decimal?,
                IdTipo = dr["idTipo"] as int?,
                Telefono = dr["telefono"].ToString(),
                Terreno = dr["terreno"] == DBNull.Value ? null : Convert.ToSingle(dr["terreno"]),
                Construccion = dr["construccion"] == DBNull.Value ? null : Convert.ToSingle(dr["construccion"]),
                Precio = dr["precio"] == DBNull.Value ? null : Convert.ToSingle(dr["precio"]),
                Observaciones = dr["observaciones"].ToString(),
                Exclusiva = dr["exclusiva"] as int?,
                Link = dr["link"].ToString(),
                Contacto = null,
                Imagenes = Convert.ToInt32(dr["imagenes"]),
                Recamaras = LeerNullableIntSiExiste(dr, "Recamaras"),
                BanosCompletos = LeerNullableIntSiExiste(dr, "BanosCompletos"),
                MediosBanos = LeerNullableIntSiExiste(dr, "MediosBanos"),
                Estacionamientos = LeerNullableIntSiExiste(dr, "Estacionamientos"),
                Niveles = LeerNullableIntSiExiste(dr, "Niveles"),
                AntiguedadAnos = LeerNullableIntSiExiste(dr, "AntiguedadAnos"),
                AmenidadesCsv = LeerStringSiExiste(dr, "AmenidadesCsv")
            };
        }

        private static int? LeerNullableIntSiExiste(SqlDataReader dr, string columna)
        {
            int ordinal;
            try { ordinal = dr.GetOrdinal(columna); }
            catch (IndexOutOfRangeException) { return null; }
            return dr.IsDBNull(ordinal) ? null : Convert.ToInt32(dr.GetValue(ordinal));
        }

        private static string? LeerStringSiExiste(SqlDataReader dr, string columna)
        {
            int ordinal;
            try { ordinal = dr.GetOrdinal(columna); }
            catch (IndexOutOfRangeException) { return null; }
            return dr.IsDBNull(ordinal) ? null : dr.GetString(ordinal);
        }
    }
}
