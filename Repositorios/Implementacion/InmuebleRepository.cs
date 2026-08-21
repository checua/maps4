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
                        _lista.Add(MapearInmueblePublico(dr));
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
                        _lista.Add(MapearInmueblePublico(dr));
                    }
                }
            }
            return _lista;
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
                // contacto_a fue usado históricamente como Notas privadas.
                // Nunca debe salir por una lectura pública.
                Contacto = null,
                Imagenes = Convert.ToInt32(dr["imagenes"]),
                Recamaras = LeerNullableInt(dr, "Recamaras"),
                BanosCompletos = LeerNullableInt(dr, "BanosCompletos"),
                MediosBanos = LeerNullableInt(dr, "MediosBanos"),
                Estacionamientos = LeerNullableInt(dr, "Estacionamientos"),
                Niveles = LeerNullableInt(dr, "Niveles"),
                AntiguedadAnos = LeerNullableInt(dr, "AntiguedadAnos"),
                AmenidadesCsv = dr["AmenidadesCsv"] == DBNull.Value ? null : dr["AmenidadesCsv"].ToString()
            };
        }

        private static int? LeerNullableInt(SqlDataReader dr, string columna)
        {
            return dr[columna] == DBNull.Value ? null : Convert.ToInt32(dr[columna]);
        }
    }
}
