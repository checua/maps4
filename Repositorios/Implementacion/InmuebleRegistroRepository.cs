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
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL");
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

        public async Task<List<Inmueble>> GetInmuebleById(int inmuebleId)
        {
            List<Inmueble> _lista = new List<Inmueble>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("RSMAPS_sp_GetInmuebleById", conexion);
                cmd.Parameters.AddWithValue("idInmueble", inmuebleId);
                cmd.CommandType = CommandType.StoredProcedure;

                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        _lista.Add(new Inmueble
                        {
                            IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                            refInmobiliaria = new Inmobiliaria()
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
                            Terreno = float.Parse(dr["terreno"].ToString()),
                            Construccion = float.Parse(dr["construccion"].ToString()),
                            Precio = float.Parse(dr["precio"].ToString()),
                            Observaciones = dr["observaciones"].ToString(),
                            Exclusiva = dr["exclusiva"] as int?,
                            Link = dr["link"].ToString(),
                            Contacto = dr["contacto_a"].ToString(),
                            Imagenes = Convert.ToInt32(dr["imagenes"]),
                        });
                    }
                }
            }
            return _lista;
        }
    }
}
