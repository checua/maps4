using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public class InmuebleFotoRepository : IInmuebleFotoRepository
    {
        private readonly string _cadenaSQL;

        public InmuebleFotoRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<InmuebleFotoViewModel>> ListarAsync(string correoAutenticado, int idInmueble)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ListarFotosBorradorWeb", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;

            List<InmuebleFotoViewModel> fotos = new();
            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
                fotos.Add(Leer(dr));

            return fotos;
        }

        public async Task<InmuebleFotoViewModel?> ObtenerAsync(string correoAutenticado, long idImagen)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ObtenerFotoPrivada", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idImagen", SqlDbType.BigInt).Value = idImagen;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            return await dr.ReadAsync() ? Leer(dr) : null;
        }

        public async Task<InmuebleFotoViewModel?> ObtenerPublicaPorOrdenAsync(int idInmueble, int orden)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ObtenerFotoPublicaPorOrden", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@orden", SqlDbType.Int).Value = orden;

            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            return await dr.ReadAsync() ? Leer(dr) : null;
        }

        public async Task<long> RegistrarAsync(string correoAutenticado, int idInmueble, FotoAlmacenada foto)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_RegistrarFotoBorrador", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@claveAlmacenamiento", SqlDbType.NVarChar, 500).Value = foto.ClaveAlmacenamiento;
            cmd.Parameters.Add("@nombreOriginal", SqlDbType.NVarChar, 255).Value = string.IsNullOrWhiteSpace(foto.NombreOriginal) ? DBNull.Value : foto.NombreOriginal;
            cmd.Parameters.Add("@mimeType", SqlDbType.VarChar, 100).Value = foto.MimeType;
            cmd.Parameters.Add("@bytes", SqlDbType.BigInt).Value = foto.Bytes;

            SqlParameter id = cmd.Parameters.Add("@idImagen", SqlDbType.BigInt);
            id.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt64(id.Value);
        }

        public async Task EstablecerPortadaAsync(string correoAutenticado, int idInmueble, long idImagen)
        {
            await EjecutarAccionAsync("dbo.RSMAPS_sp_EstablecerPortadaBorrador", correoAutenticado, idInmueble, idImagen);
        }

        public async Task EliminarMetadataAsync(string correoAutenticado, int idInmueble, long idImagen)
        {
            await EjecutarAccionAsync("dbo.RSMAPS_sp_EliminarFotoBorrador", correoAutenticado, idInmueble, idImagen);
        }

        public async Task MoverAsync(string correoAutenticado, int idInmueble, long idImagen, int direccion)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_MoverFotoBorradorWeb", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@idImagen", SqlDbType.BigInt).Value = idImagen;
            cmd.Parameters.Add("@direccion", SqlDbType.Int).Value = direccion;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task EjecutarAccionAsync(string procedimiento, string correoAutenticado, int idInmueble, long idImagen)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand(procedimiento, conexion)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;
            cmd.Parameters.Add("@idImagen", SqlDbType.BigInt).Value = idImagen;
            await cmd.ExecuteNonQueryAsync();
        }

        private static InmuebleFotoViewModel Leer(SqlDataReader dr)
        {
            return new InmuebleFotoViewModel
            {
                IdImagen = Convert.ToInt64(dr["IdImagen"]),
                IdInmueble = Convert.ToInt32(dr["IdInmueble"]),
                ClaveAlmacenamiento = dr["ClaveAlmacenamiento"].ToString() ?? string.Empty,
                NombreOriginal = dr["NombreOriginal"] == DBNull.Value ? null : dr["NombreOriginal"].ToString(),
                MimeType = dr["MimeType"].ToString() ?? "image/jpeg",
                Bytes = Convert.ToInt64(dr["Bytes"]),
                Orden = Convert.ToInt32(dr["Orden"]),
                EsPortada = Convert.ToBoolean(dr["EsPortada"]),
                FechaAltaUtc = Convert.ToDateTime(dr["FechaAltaUtc"])
            };
        }
    }
}
