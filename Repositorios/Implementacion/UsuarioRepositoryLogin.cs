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
                await conexion.OpenAsync();

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
                            refInmobiliaria = dr["idInmobiliaria"] == DBNull.Value
                                ? null
                                : new Inmobiliaria()
                                {
                                    idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                                    nombre = dr["nombre"].ToString()
                                },
                            nick = dr["nick"].ToString(),
                            telefono = dr["telefono"].ToString(),
                            correo = dr["correo"].ToString(),
                            foto = dr["foto"].ToString(),
                            obs = dr["obs"].ToString(),
                            dob = dr["fechaNacimiento"].ToString(),
                            revisado = dr["revisado"].ToString(),
                            IdCuenta = dr["IdCuenta"] == DBNull.Value ? null : Convert.ToInt32(dr["IdCuenta"]),
                            CuentaNombre = dr["CuentaNombre"].ToString(),
                            TipoCuenta = dr["TipoCuenta"].ToString(),
                            RolCodigo = dr["RolCodigo"].ToString()
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
                    await conexion.OpenAsync();

                    SqlCommand cmd = new SqlCommand("RSMAPS_sp_GuardarUsuario", conexion);
                    cmd.Parameters.AddWithValue("nombres", modelo.nombres ?? string.Empty);
                    cmd.Parameters.AddWithValue("aPaterno", modelo.aPaterno ?? string.Empty);
                    cmd.Parameters.AddWithValue("aMaterno", modelo.aMaterno ?? string.Empty);
                    cmd.Parameters.AddWithValue("nick", string.IsNullOrWhiteSpace(modelo.nick) ? "nick" : modelo.nick);
                    cmd.Parameters.AddWithValue("contra", modelo.contra ?? string.Empty);
                    cmd.Parameters.AddWithValue("telefono", modelo.telefono ?? string.Empty);
                    cmd.Parameters.AddWithValue("correo", modelo.correo ?? string.Empty);
                    cmd.Parameters.AddWithValue("foto", modelo.foto ?? string.Empty);
                    cmd.Parameters.AddWithValue("obs", modelo.obs ?? string.Empty);
                    cmd.Parameters.AddWithValue("dob", DateTime.Now);
                    cmd.Parameters.AddWithValue("revisado", 1);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // El procedimiento es transaccional: si no lanza excepción,
                    // Usuario + Cuenta INDIVIDUAL + membresía fueron creados juntos.
                    await cmd.ExecuteNonQueryAsync();
                    return modelo;
                }
            }
            catch (Exception ex)
            {
                modelo.correo = "";

                if (ex is SqlException sqlEx &&
                    (sqlEx.Number == 50811 || sqlEx.Number == 2601 || sqlEx.Number == 2627))
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
