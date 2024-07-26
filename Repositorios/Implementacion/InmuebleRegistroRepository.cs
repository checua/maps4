using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                using (var cmd = new SqlCommand("sp_RSMAPS_insertar_coordenadas", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Agregar los parámetros de entrada
                    cmd.Parameters.AddWithValue("@correo", data.RefUsuario.correo);
                    cmd.Parameters.AddWithValue("@idInmobiliaria", 1);
                    cmd.Parameters.AddWithValue("@lat", data.Lat);
                    cmd.Parameters.AddWithValue("@lng", data.Lng);
                    cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                    cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                    cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                    cmd.Parameters.AddWithValue("@precio", data.Precio);
                    cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                    cmd.Parameters.AddWithValue("@contacto", data.Contacto);
                    cmd.Parameters.AddWithValue("@numImagenes", data.Imagenes);

                    // Agregar el parámetro de salida para obtener el idInmueble
                    var idInmuebleParam = new SqlParameter("@idInmueble", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(idInmuebleParam);

                    // Ejecutar el comando
                    await cmd.ExecuteNonQueryAsync();

                    // Recuperar el idInmueble generado
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
                using (var cmd = new SqlCommand("sp_RSMAPS_insertar_coordenadas", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idInmueble", data.IdInmueble);
                    cmd.Parameters.AddWithValue("@correo", data.RefUsuario.correo);
                    cmd.Parameters.AddWithValue("@idInmobiliaria", 1);
                    //cmd.Parameters.AddWithValue("@lat", data.Lat);
                    //cmd.Parameters.AddWithValue("@lng", data.Lng);
                    cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                    cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                    cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                    cmd.Parameters.AddWithValue("@precio", data.Precio);
                    cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                    cmd.Parameters.AddWithValue("@contacto", data.Contacto);
                    //cmd.Parameters.AddWithValue("@numImagenes", data.Imagenes);

                    // Agregar el parámetro de salida para obtener el idInmueble
                    //var idInmuebleParam = new SqlParameter("@idInmueble", SqlDbType.Int)
                    //{
                    //    Direction = ParameterDirection.Output
                    //};
                    //cmd.Parameters.Add(idInmuebleParam);

                    // Ejecutar el comando
                    await cmd.ExecuteNonQueryAsync();

                    // Recuperar el idInmueble generado
                    //data.IdInmueble = (int)idInmuebleParam.Value;

                    return true;
                }
            }
        }

        public async Task<bool> EliminarInmueble(int idInmueble)
        {
            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                await conexion.OpenAsync();
                using (var cmd = new SqlCommand("sp_RSMAPS_delete_inmueble", conexion))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Agregar el parámetro de entrada
                    cmd.Parameters.AddWithValue("@idInmueble", idInmueble);

                    // Ejecutar el comando
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
                SqlCommand cmd = new SqlCommand("sp_GetInmuebleById", conexion);
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
