using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class UsuarioRepositoryLogin : IUsuarioServicio<Usuario>
    {
        private readonly string _cadenaSQL = "";
        public UsuarioRepositoryLogin(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL");
        }
        public async Task<List<Usuario>> GetUsuario(string correo, string contra)
        {
            List<Usuario> _lista = new List<Usuario>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("RSMAPS_sp_ListaUsuarioLogin", conexion);
                cmd.Parameters.AddWithValue("correo", correo);
                cmd.Parameters.AddWithValue("contra", contra);
                cmd.CommandType = CommandType.StoredProcedure;

                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        _lista.Add(new Usuario
                        {
                            idAsesor = Convert.ToInt32(dr["idAsesor"]),
                            nombres = dr["nombres"].ToString(),
                            aPaterno = dr["aPaterno"].ToString(),
                            aMaterno = dr["aMaterno"].ToString(),
                            refInmobiliaria = new Inmobiliaria()
                            {
                                idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                                nombre = dr["nombre"].ToString()
                            },
                            nick = dr["nick"].ToString(),
                            contra = dr["contra"].ToString(),
                            telefono = dr["telefono"].ToString(),
                            correo = dr["correo"].ToString(),
                            foto = dr["foto"].ToString(),
                            obs = dr["obs"].ToString(),
                            dob = dr["fechaNacimiento"].ToString(),
                            revisado = dr["revisado"].ToString()
                        });
                    }
                }
            }
            return _lista;
        }

        public async Task<Usuario> SaveUsuario(Usuario modelo)
        {
            try
            {
                using (var conexion = new SqlConnection(_cadenaSQL))
                {
                    conexion.Open();
                    SqlCommand cmd = new SqlCommand("RSMAPS_sp_GuardarUsuario", conexion);
                    cmd.Parameters.AddWithValue("nombres", modelo.nombres);
                    cmd.Parameters.AddWithValue("aPaterno", modelo.aPaterno);
                    cmd.Parameters.AddWithValue("aMaterno", "");
                    cmd.Parameters.AddWithValue("idInmobiliaria", 1);
                    cmd.Parameters.AddWithValue("nick", "nick");
                    cmd.Parameters.AddWithValue("contra", modelo.contra);
                    cmd.Parameters.AddWithValue("telefono", modelo.telefono);
                    cmd.Parameters.AddWithValue("correo", modelo.correo);
                    cmd.Parameters.AddWithValue("foto", "foto");
                    cmd.Parameters.AddWithValue("obs", "obs");
                    cmd.Parameters.AddWithValue("dob", DateTime.Now);
                    cmd.Parameters.AddWithValue("revisado", 1);

                    cmd.CommandType = CommandType.StoredProcedure;

                    int filas_afectadas = await cmd.ExecuteNonQueryAsync();
                    if (filas_afectadas > 0)
                    {
                        return modelo;
                    }
                    else
                    {
                        modelo.correo = "";
                        modelo.revisado = "No se afectaron filas en la base de datos.";
                        return modelo;
                    }
                }
            }
            catch (Exception ex)
            {
                modelo.correo = "";
                if (ex.Message.Contains("Violation of UNIQUE KEY constraint"))
                {
                    modelo.revisado = "Este correo ya ha sido registrado";
                }
                else
                {
                    modelo.revisado = $"Error al guardar usuario: {ex.Message}";
                }
                return modelo;
            }
        }
    }
}
