using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class UsuarioRepositorio : IGenericRepository<Usuario>
    {
        private readonly string _cadenaSQL = "";

        public UsuarioRepositorio(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL");
        }

        public async Task<List<Usuario>> Lista()
        {
            List<Usuario> _lista = new List<Usuario>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("sp_ListaUsuarios", conexion);
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
                            aMaterno = dr["nombres"].ToString(),
                            refnmobiliaria = new Inmobiliaria()
                            {
                                idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                                nombres = dr["nombre"].ToString()
                            },
                            nick = dr["nombres"].ToString(),
                            contra = dr["nombres"].ToString(),
                            telefono = dr["nombres"].ToString(),
                            correo = dr["nombres"].ToString(),
                            foto = dr["nombres"].ToString(),
                            obs = dr["nombres"].ToString(),
                            dob = dr["nombres"].ToString(),
                            revisado = dr["dateofbirth"].ToString()
                        });
                    }
                }
            }

            return _lista;
        }

        //public async Task<bool> Guardar(Usuario modelo)
        //{
        //    using (var conexion = new SqlConnection(_cadenaSQL))
        //    {
        //        conexion.Open();
        //        SqlCommand cmd = new SqlCommand("sp_ListaUsuarios", conexion);
        //        cmd.CommandType = CommandType.StoredProcedure;

        //    }
        //}

        //public Task<bool> Editar(Usuario modelo)
        //{
        //    throw new NotImplementedException();
        //}

        //public Task<bool> Eliminar(int id)
        //{
        //    throw new NotImplementedException();
        //}




    }
}
