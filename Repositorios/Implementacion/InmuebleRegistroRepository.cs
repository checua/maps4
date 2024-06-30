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

        public async Task<Inmueble> SaveInmueble(Inmueble data)//, int files, string correo)
        {
            //List<Usuario> _lista = new List<Usuario>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("sp_RSMAPS_insertar_coordenadas", conexion);
                cmd.Parameters.AddWithValue("@correo", "profesor76@hotmail.com");
                cmd.Parameters.AddWithValue("@idInmobiliaria", 1);
                cmd.Parameters.AddWithValue("@lat", data.Lat);
                //cmd.Parameters.AddWithValue("@direccion", "");
                cmd.Parameters.AddWithValue("@lng", data.Lng);
                cmd.Parameters.AddWithValue("@idTipo", data.IdTipo);
                cmd.Parameters.AddWithValue("@terreno", data.Terreno);
                cmd.Parameters.AddWithValue("@construccion", data.Construccion);
                cmd.Parameters.AddWithValue("@precio", data.Precio);
                cmd.Parameters.AddWithValue("@observaciones", data.Observaciones);
                cmd.Parameters.AddWithValue("@contacto", data.Contacto);
                cmd.Parameters.AddWithValue("@numImagenes", 3);



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
