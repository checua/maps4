﻿using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class UsuarioRepository : IGenericRepository<Usuario>
    {
        private readonly string _cadenaSQL = "";

        public UsuarioRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL");
        }

        public async Task<List<Usuario>> Lista()
        {
            List<Usuario> _lista = new List<Usuario>();

            using (var conexion = new SqlConnection(_cadenaSQL))
            {
                conexion.Open();
                SqlCommand cmd = new SqlCommand("sp_ListaUsuarioLogin", conexion);
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
    }
}
