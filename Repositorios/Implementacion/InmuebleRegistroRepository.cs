using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class InmuebleRegistroRepository : IInmuebleServicio<Inmueble>
    {
        private readonly string _cadenaSQL = "";

        public InmuebleRegistroRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<Inmueble> SaveInmueble(Inmueble data)
        {
            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                await conexion.OpenAsync();
                using (var cmd = new SqlCommand("RSMAPS_sp_insertar_coordenadas", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // La identidad llega desde la sesión autenticada del servidor.
                    // SQL resuelve la Cuenta desde la membresía activa/predeterminada.
                    cmd.Parameters.AddWithValue("@correo", data.RefUsuario?.correo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@lat", data.Lat);
                    cmd.Parameters.AddWithValue("@lng", data.Lng);
                    cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                    cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                    cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                    cmd.Parameters.AddWithValue("@precio", data.Precio);
                    cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                    cmd.Parameters.AddWithValue("@contacto", data.Contacto);
                    cmd.Parameters.AddWithValue("@numImagenes", data.Imagenes);

                    var idInmuebleParam = new SqlParameter("@idInmueble", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(idInmuebleParam);

                    await cmd.ExecuteNonQueryAsync();

                    data.IdInmueble = (int)idInmuebleParam.Value;
                    return data;
                }
            }
        }

        public async Task<bool> UpdateInmueble(Inmueble data)
        {
            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                await conexion.OpenAsync();
                using (var cmd = new SqlCommand("RSMAPS_sp_insertar_coordenadas", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idInmueble", data.IdInmueble);
                    cmd.Parameters.AddWithValue("@correo", data.RefUsuario?.correo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                    cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                    cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                    cmd.Parameters.AddWithValue("@precio", data.Precio);
                    cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                    cmd.Parameters.AddWithValue("@contacto", data.Contacto);

                    await cmd.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }

        public async Task<bool> EliminarInmueble(int idInmueble, string correoAutenticado)
        {
            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                await conexion.OpenAsync();
                using (var cmd = new SqlCommand("RSMAPS_sp_delete_inmueble_seguro", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idInmueble", idInmueble);
                    cmd.Parameters.AddWithValue("@correo", correoAutenticado);

                    await cmd.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }

        // Lectura pública. El procedimiento devuelve únicamente PUBLICADO + PUBLICO.
        public async Task<List<Inmueble>> GetInmuebleById(int inmuebleId)
        {
            using var conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using var cmd = new SqlCommand("RSMAPS_sp_GetInmueblePublicoById", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = inmuebleId;

            return await LeerInmueblesAsync(cmd);
        }

        // Lectura privada. SQL valida identidad, Cuenta y propietario.
        public async Task<List<Inmueble>> GetInmueblePrivadoById(int inmuebleId, string correoAutenticado)
        {
            using var conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using var cmd = new SqlCommand("RSMAPS_sp_GetInmueblePrivadoById", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = inmuebleId;
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;

            return await LeerInmueblesAsync(cmd);
        }

        private static async Task<List<Inmueble>> LeerInmueblesAsync(SqlCommand cmd)
        {
            List<Inmueble> lista = new();

            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                lista.Add(new Inmueble
                {
                    IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                    refInmobiliaria = dr["idInmobiliaria"] == DBNull.Value
                        ? null
                        : new Inmobiliaria
                        {
                            idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                            nombre = dr["nombre"] == DBNull.Value ? null : dr["nombre"].ToString()
                        },
                    RefUsuario = new Usuario
                    {
                        idAsesor = Convert.ToInt32(dr["idAsesor"]),
                        nombres = dr["nombres"] == DBNull.Value ? null : dr["nombres"].ToString(),
                        aPaterno = dr["aPaterno"] == DBNull.Value ? null : dr["aPaterno"].ToString(),
                        correo = dr["correo"] == DBNull.Value ? null : dr["correo"].ToString(),
                    },
                    Direccion = dr["direccion"] == DBNull.Value ? null : dr["direccion"].ToString(),
                    Lat = dr["lat"] == DBNull.Value ? null : Convert.ToDecimal(dr["lat"]),
                    Lng = dr["lng"] == DBNull.Value ? null : Convert.ToDecimal(dr["lng"]),
                    IdTipo = dr["idTipo"] == DBNull.Value ? null : Convert.ToInt32(dr["idTipo"]),
                    Telefono = dr["telefono"] == DBNull.Value ? null : dr["telefono"].ToString(),
                    Terreno = dr["terreno"] == DBNull.Value ? 0 : Convert.ToDouble(dr["terreno"]),
                    Construccion = dr["construccion"] == DBNull.Value ? 0 : Convert.ToDouble(dr["construccion"]),
                    Precio = dr["precio"] == DBNull.Value ? 0 : Convert.ToDouble(dr["precio"]),
                    Observaciones = dr["observaciones"] == DBNull.Value ? null : dr["observaciones"].ToString(),
                    Exclusiva = dr["exclusiva"] == DBNull.Value ? null : Convert.ToInt32(dr["exclusiva"]),
                    Link = dr["link"] == DBNull.Value ? null : dr["link"].ToString(),
                    Contacto = dr["contacto_a"] == DBNull.Value ? null : dr["contacto_a"].ToString(),
                    Imagenes = dr["imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["imagenes"]),
                });
            }

            return lista;
        }
    }
}
