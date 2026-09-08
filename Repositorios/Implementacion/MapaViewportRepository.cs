using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion
{
    public sealed class MapaViewportRepository : IMapaViewportRepository
    {
        private readonly string _cadenaSQL;

        public MapaViewportRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<MapaViewportResultado> ListarAsync(
            decimal north,
            decimal south,
            decimal east,
            decimal west,
            int maxResultados,
            CancellationToken cancellationToken = default)
        {
            int take = Math.Clamp(maxResultados, 1, 2500) + 1;
            var lista = new List<Inmueble>();

            const string sql = @"
SELECT TOP (@Take)
    i.idInmueble,
    i.idInmobiliaria,
    inm.nombre,
    i.idAsesor,
    u.nombres,
    u.aPaterno,
    u.correo,
    i.direccion,
    i.lat,
    i.lng,
    i.idTipo,
    i.telefono,
    i.terreno,
    i.construccion,
    i.precio,
    i.observaciones,
    i.exclusiva,
    i.link,
    CAST(NULL AS varchar(max)) AS contacto_a,
    ISNULL(img.Imagenes, 0) AS imagenes,
    i.Recamaras,
    i.BanosCompletos,
    i.MediosBanos,
    i.Estacionamientos,
    i.Niveles,
    i.AntiguedadAnos,
    am.AmenidadesCsv
FROM dbo.RSMAPS_Inmueble i
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = i.idAsesor
LEFT JOIN dbo.RSMAPS_Inmobiliaria inm ON inm.idInmobiliaria = i.idInmobiliaria
OUTER APPLY
(
    SELECT MAX(ii.Imagenes) AS Imagenes
    FROM dbo.RSMAPS_InmuebleImagenes ii
    WHERE ii.idInmueble = i.idInmueble
) img
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(varchar(max), ia.AmenidadCodigo), ',')
           WITHIN GROUP (ORDER BY ia.AmenidadCodigo) AS AmenidadesCsv
    FROM dbo.RSMAPS_InmuebleAmenidad ia
    INNER JOIN dbo.RSMAPS_Amenidad a
        ON a.Codigo = ia.AmenidadCodigo
       AND a.Activo = 1
       AND a.EsFiltro = 1
    WHERE ia.IdInmueble = i.idInmueble
) am
WHERE i.EstadoCodigo = 'PUBLICADO'
  AND i.VisibilidadCodigo = 'PUBLICO'
  AND i.lat IS NOT NULL
  AND i.lng IS NOT NULL
  AND i.lat BETWEEN @South AND @North
  AND
  (
      (@West <= @East AND i.lng BETWEEN @West AND @East)
      OR
      (@West > @East AND (i.lng >= @West OR i.lng <= @East))
  )
ORDER BY i.idInmueble;";

            await using var conexion = new SqlConnection(_cadenaSQL);
            await conexion.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conexion)
            {
                CommandType = CommandType.Text,
                CommandTimeout = 20
            };

            cmd.Parameters.Add("@Take", SqlDbType.Int).Value = take;
            cmd.Parameters.Add("@North", SqlDbType.Decimal).Value = north;
            cmd.Parameters.Add("@South", SqlDbType.Decimal).Value = south;
            cmd.Parameters.Add("@East", SqlDbType.Decimal).Value = east;
            cmd.Parameters.Add("@West", SqlDbType.Decimal).Value = west;

            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await dr.ReadAsync(cancellationToken))
            {
                lista.Add(Mapear(dr));
            }

            bool truncated = lista.Count > maxResultados;
            if (truncated)
                lista.RemoveAt(lista.Count - 1);

            return new MapaViewportResultado(lista, truncated);
        }

        private static Inmueble Mapear(SqlDataReader dr)
        {
            return new Inmueble
            {
                IdInmueble = Convert.ToInt32(dr["idInmueble"]),
                refInmobiliaria = dr["idInmobiliaria"] == DBNull.Value
                    ? null
                    : new Inmobiliaria
                    {
                        idInmobiliaria = Convert.ToInt32(dr["idInmobiliaria"]),
                        nombre = dr["nombre"].ToString()
                    },
                RefUsuario = new Usuario
                {
                    idAsesor = Convert.ToInt32(dr["idAsesor"]),
                    nombres = dr["nombres"].ToString(),
                    aPaterno = dr["aPaterno"].ToString(),
                    correo = dr["correo"].ToString()
                },
                Direccion = dr["direccion"].ToString(),
                Lat = dr["lat"] == DBNull.Value ? null : Convert.ToDecimal(dr["lat"]),
                Lng = dr["lng"] == DBNull.Value ? null : Convert.ToDecimal(dr["lng"]),
                IdTipo = dr["idTipo"] == DBNull.Value ? null : Convert.ToInt32(dr["idTipo"]),
                Telefono = dr["telefono"].ToString(),
                Terreno = dr["terreno"] == DBNull.Value ? null : Convert.ToSingle(dr["terreno"]),
                Construccion = dr["construccion"] == DBNull.Value ? null : Convert.ToSingle(dr["construccion"]),
                Precio = dr["precio"] == DBNull.Value ? null : Convert.ToSingle(dr["precio"]),
                Observaciones = dr["observaciones"].ToString(),
                Exclusiva = dr["exclusiva"] == DBNull.Value ? null : Convert.ToInt32(dr["exclusiva"]),
                Link = dr["link"].ToString(),
                Contacto = null,
                Imagenes = dr["imagenes"] == DBNull.Value ? 0 : Convert.ToInt32(dr["imagenes"]),
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
            int ordinal = dr.GetOrdinal(columna);
            return dr.IsDBNull(ordinal) ? null : Convert.ToInt32(dr.GetValue(ordinal));
        }
    }
}
