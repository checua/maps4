using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarPendingDeliveryRepository : IRadarPendingDeliveryRepository
{
    private readonly string _cadenaSQL;

    public RadarPendingDeliveryRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<IReadOnlyList<RadarPendingDeliveryItem>> ListarAsync(
        Guid idAgent,
        int maxResultados,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            throw new ArgumentException("Se requiere un IdAgent válido.", nameof(idAgent));

        int max = Math.Clamp(maxResultados, 1, 100);
        var items = new List<RadarPendingDeliveryItem>();

        const string sql = @"
SELECT TOP (@max)
    d.IdRadarMessageDelivery,
    p.ChatOrigen,
    p.MessageId,
    d.SolicitudIndice,
    d.ClaveEntrega,
    d.IdInmueble,
    d.Puntuacion,
    d.Estado,
    d.IntentosEntrega,
    d.PayloadAlerta
FROM dbo.RSMAPS_RadarMessageDelivery d
INNER JOIN dbo.RSMAPS_RadarMessageProcessing p
    ON p.IdRadarMessageProcessing = d.IdRadarMessageProcessing
WHERE p.IdAgent = @idAgent
  AND p.Estado = N'COMPLETADO'
  AND d.Estado IN (N'PENDIENTE', N'FALLIDO_REINTENTABLE')
ORDER BY
    COALESCE(d.UltimoIntentoUtc, d.CreadoUtc),
    d.IdRadarMessageDelivery;";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@max", SqlDbType.Int).Value = max;
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            items.Add(new RadarPendingDeliveryItem
            {
                IdRadarMessageDelivery = Convert.ToInt64(dr["IdRadarMessageDelivery"]),
                ChatOrigen = dr["ChatOrigen"].ToString() ?? string.Empty,
                MessageId = dr["MessageId"].ToString() ?? string.Empty,
                SolicitudIndice = Convert.ToInt32(dr["SolicitudIndice"]),
                ClaveEntrega = dr["ClaveEntrega"].ToString() ?? string.Empty,
                IdInmueble = dr["IdInmueble"] == DBNull.Value ? null : Convert.ToInt32(dr["IdInmueble"]),
                Puntuacion = dr["Puntuacion"] == DBNull.Value ? null : Convert.ToInt32(dr["Puntuacion"]),
                Estado = dr["Estado"].ToString() ?? string.Empty,
                IntentosEntrega = Convert.ToInt32(dr["IntentosEntrega"]),
                PayloadAlerta = dr["PayloadAlerta"] == DBNull.Value ? null : dr["PayloadAlerta"].ToString()
            });
        }

        return items;
    }
}