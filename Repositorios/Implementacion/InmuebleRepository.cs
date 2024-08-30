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
                SqlCommand cmd = new SqlCommand("RSMAPS_sp_ListaInmuebles", conexion);
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
