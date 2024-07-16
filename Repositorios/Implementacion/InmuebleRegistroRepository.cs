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

    }
}
