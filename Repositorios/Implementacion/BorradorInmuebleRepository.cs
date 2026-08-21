using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace maps4.Repositorios.Implementacion
{
    public class BorradorInmuebleRepository : IBorradorInmuebleRepository
    {
        private readonly string _cadenaSQL;

        public BorradorInmuebleRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<int> CrearAsync(string correoAutenticado, decimal lat, decimal lng, int idTipo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_CrearBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;

            SqlParameter latParam = cmd.Parameters.Add("@lat", SqlDbType.Decimal);
            latParam.Precision = 10;
            latParam.Scale = 6;
            latParam.Value = lat;

            SqlParameter lngParam = cmd.Parameters.Add("@lng", SqlDbType.Decimal);
            lngParam.Precision = 10;
            lngParam.Scale = 6;
            lngParam.Value = lng;

            cmd.Parameters.Add("@idTipo", SqlDbType.Int).Value = idTipo;

            SqlParameter idParam = cmd.Parameters.Add("@idInmueble", SqlDbType.Int);
            idParam.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(idParam.Value);
        }

        public async Task<BorradorEdicionViewModel?> ObtenerParaEdicionAsync(string correoAutenticado, int idInmueble)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();

            BorradorEdicionViewModel? modelo;
            using (SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_ObtenerBorradorInmueble", conexion)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
                cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;

                using SqlDataReader dr = await cmd.ExecuteReaderAsync();
                if (!await dr.ReadAsync())
                    return null;

                modelo = new BorradorEdicionViewModel
                {
                    IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                    IdCuenta = Convert.ToInt32(dr["IdCuenta"]),
                    IdAsesor = Convert.ToInt32(dr["idAsesor"]),
                    Direccion = dr["direccion"] == DBNull.Value ? null : dr["direccion"].ToString(),
                    Lat = dr["lat"] == DBNull.Value ? null : Convert.ToDecimal(dr["lat"]),
                    Lng = dr["lng"] == DBNull.Value ? null : Convert.ToDecimal(dr["lng"]),
                    IdTipo = dr["idTipo"] == DBNull.Value ? 0 : Convert.ToInt32(dr["idTipo"]),
                    TipoNombre = dr["TipoNombre"] == DBNull.Value ? null : dr["TipoNombre"].ToString(),
                    Terreno = dr["terreno"] == DBNull.Value ? null : Convert.ToDouble(dr["terreno"]),
                    Construccion = dr["construccion"] == DBNull.Value ? null : Convert.ToDouble(dr["construccion"]),
                    Precio = dr["precio"] == DBNull.Value ? null : Convert.ToDecimal(dr["precio"]),
                    Recamaras = dr["Recamaras"] == DBNull.Value ? null : Convert.ToInt32(dr["Recamaras"]),
                    BanosCompletos = dr["BanosCompletos"] == DBNull.Value ? null : Convert.ToInt32(dr["BanosCompletos"]),
                    MediosBanos = dr["MediosBanos"] == DBNull.Value ? null : Convert.ToInt32(dr["MediosBanos"]),
                    Estacionamientos = dr["Estacionamientos"] == DBNull.Value ? null : Convert.ToInt32(dr["Estacionamientos"]),
                    Niveles = dr["Niveles"] == DBNull.Value ? null : Convert.ToInt32(dr["Niveles"]),
                    AntiguedadAnos = dr["AntiguedadAnos"] == DBNull.Value ? null : Convert.ToInt32(dr["AntiguedadAnos"]),
                    Observaciones = dr["observaciones"] == DBNull.Value ? null : dr["observaciones"].ToString(),
                    NotasPrivadas = dr["NotasPrivadas"] == DBNull.Value ? null : dr["NotasPrivadas"].ToString(),
                    Imagenes = dr["Imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Imagenes"]),
                    EstadoCodigo = dr["EstadoCodigo"] == DBNull.Value ? "BORRADOR" : dr["EstadoCodigo"].ToString() ?? "BORRADOR",
                    VisibilidadCodigo = dr["VisibilidadCodigo"] == DBNull.Value ? "CUENTA" : dr["VisibilidadCodigo"].ToString() ?? "CUENTA",
                    FechaUltimaEdicionUtc = dr["FechaUltimaEdicionUtc"] == DBNull.Value ? null : Convert.ToDateTime(dr["FechaUltimaEdicionUtc"])
                };
            }

            using (SqlCommand cmdAmenidades = new SqlCommand("dbo.RSMAPS_sp_ListarAmenidadesBorrador", conexion)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                cmdAmenidades.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
                cmdAmenidades.Parameters.Add("@idInmueble", SqlDbType.Int).Value = idInmueble;

                using SqlDataReader dr = await cmdAmenidades.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    AmenidadOpcionViewModel amenidad = new()
                    {
                        Codigo = dr["Codigo"].ToString() ?? string.Empty,
                        Nombre = dr["Nombre"].ToString() ?? string.Empty,
                        Grupo = dr["Grupo"].ToString() ?? string.Empty,
                        Orden = Convert.ToInt32(dr["Orden"]),
                        Seleccionada = Convert.ToBoolean(dr["Seleccionada"])
                    };
                    modelo.AmenidadesDisponibles.Add(amenidad);
                    if (amenidad.Seleccionada)
                        modelo.AmenidadesSeleccionadas.Add(amenidad.Codigo);
                }
            }

            return modelo;
        }

        public async Task GuardarAsync(string correoAutenticado, BorradorEdicionViewModel modelo)
        {
            using SqlConnection conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync();
            using SqlTransaction transaccion = conexion.BeginTransaction();

            try
            {
                using (SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_GuardarBorradorInmueble", conexion, transaccion)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
                    cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = modelo.IdInmueble;
                    cmd.Parameters.Add("@direccion", SqlDbType.VarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.Direccion) ? DBNull.Value : modelo.Direccion.Trim();
                    cmd.Parameters.Add("@idTipo", SqlDbType.Int).Value = modelo.IdTipo;
                    cmd.Parameters.Add("@terreno", SqlDbType.Float).Value = modelo.Terreno.HasValue ? modelo.Terreno.Value : DBNull.Value;
                    cmd.Parameters.Add("@construccion", SqlDbType.Float).Value = modelo.Construccion.HasValue ? modelo.Construccion.Value : DBNull.Value;

                    SqlParameter precio = cmd.Parameters.Add("@precio", SqlDbType.Decimal);
                    precio.Precision = 18;
                    precio.Scale = 2;
                    precio.Value = modelo.Precio.HasValue ? modelo.Precio.Value : DBNull.Value;

                    cmd.Parameters.Add("@observaciones", SqlDbType.VarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.Observaciones) ? DBNull.Value : modelo.Observaciones.Trim();
                    cmd.Parameters.Add("@notasPrivadas", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(modelo.NotasPrivadas) ? DBNull.Value : modelo.NotasPrivadas.Trim();
                    await cmd.ExecuteNonQueryAsync();
                }

                if (modelo.CaracteristicasCargadas)
                {
                    using SqlCommand cmd = new SqlCommand("dbo.RSMAPS_sp_GuardarCaracteristicasBorrador", conexion, transaccion)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correoAutenticado;
                    cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = modelo.IdInmueble;
                    AgregarSmallInt(cmd, "@recamaras", modelo.Recamaras);
                    AgregarSmallInt(cmd, "@banosCompletos", modelo.BanosCompletos);
                    AgregarSmallInt(cmd, "@mediosBanos", modelo.MediosBanos);
                    AgregarSmallInt(cmd, "@estacionamientos", modelo.Estacionamientos);
                    AgregarSmallInt(cmd, "@niveles", modelo.Niveles);
                    AgregarSmallInt(cmd, "@antiguedadAnos", modelo.AntiguedadAnos);

                    string[] amenidades = (modelo.AmenidadesSeleccionadas ?? new List<string>())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    cmd.Parameters.Add("@amenidadesJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(amenidades);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaccion.Commit();
            }
            catch
            {
                try { transaccion.Rollback(); } catch (InvalidOperationException) { }
                throw;
            }
        }

        private static void AgregarSmallInt(SqlCommand cmd, string nombre, int? valor)
        {
            SqlParameter p = cmd.Parameters.Add(nombre, SqlDbType.SmallInt);
            p.Value = valor.HasValue ? valor.Value : DBNull.Value;
        }
    }
}
