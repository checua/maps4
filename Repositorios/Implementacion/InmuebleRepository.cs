using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class InmuebleRepository : IGenericRepository<Inmueble>
    {
        private readonly string _cadenaSQL = "";

        public InmuebleRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL");
        }

        public async Task<List<Inmueble>> Lista()
        {
            List<Inmueble> _lista = new List<Inmueble>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("sp_ListaInmuebles", conexion);
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
                            refUsuario = new Usuario()
                            {
                                idAsesor = Convert.ToInt32(dr["idAsesor"]),
                                nombres = dr["nombres"].ToString(),
                                aPaterno = dr["aPaterno"].ToString(),
                            },
                            Direccion = dr["direccion"].ToString(),
                            Lat = dr["lat"] as decimal?,
                            Lng = dr["lng"] as decimal?,
                            IdTipo = dr["idTipo"] as int?,
                            Telefono = dr["telefono"].ToString(),
                            Terreno = dr["terreno"].ToString(),
                            Construccion = dr["construccion"].ToString(),
                            Precio = dr["precio"].ToString(),
                            Observaciones = dr["observaciones"].ToString(),
                            Exclusiva = dr["exclusiva"] as int?,
                            Link = dr["link"].ToString()
                        });
                    }
                }
            }

            return _lista;
        }

        public async Task<Data> SaveInmueble(Data data, int files, string correo)
        {
            //List<Usuario> _lista = new List<Usuario>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("sp_RSMAPS_insertar_coordenadas", conexion);
                cmd.Parameters.AddWithValue("@correo", correo);
                cmd.Parameters.AddWithValue("@idInmobiliaria", "1");
                cmd.Parameters.AddWithValue("@lat", data.Lat);
                cmd.Parameters.AddWithValue("@lng", data.Lng);
                cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                cmd.Parameters.AddWithValue("@precio", data.Precio);
                cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                cmd.Parameters.AddWithValue("@contacto", data.Contacto);
                cmd.Parameters.AddWithValue("@numImagenes", files); 



                cmd.CommandType = CommandType.StoredProcedure;

                int filas_afectadas = await cmd.ExecuteNonQueryAsync();
                if (filas_afectadas > 0)
                {
                    return data;
                }
                else
                {
                    //Correo = "";
                    return data;
                }
            }

        }
    }

}
