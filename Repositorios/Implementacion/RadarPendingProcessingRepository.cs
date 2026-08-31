using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarPendingProcessingRepository : IRadarPendingProcessingRepository
{
    private readonly string _cadenaSQL;

    public RadarPendingProcessingRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<IReadOnlyList<RadarPendingProcessingItem>> ListarAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return [];

        int limite = Math.Clamp(max, 1, 100);
        var items = new List<RadarPendingProcessingItem>();

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (@max)
    IdRadarMessageProcessing,
    ChatOrigen,
    MessageId,
    Autor,
    Telefono,
    MensajeOriginal,
    Estado,
    IntentosProcesamiento,
    DetectadoUtc,
    ReintentarDespuesUtc,
    LeaseHastaUtc
FROM dbo.RSMAPS_RadarMessageProcessing WITH (READPAST)
WHERE IdAgent = @idAgent
  AND ResultadoCentralJson IS NULL
  AND
  (
      (
          Estado = N'FALLIDO_REINTENTABLE'
          AND (ReintentarDespuesUtc IS NULL OR ReintentarDespuesUtc <= SYSUTCDATETIME())
      )
      OR
      (
          Estado = N'PROCESANDO'
          AND (LeaseHastaUtc IS NULL OR LeaseHastaUtc <= SYSUTCDATETIME())
      )
  )
ORDER BY
    COALESCE(ReintentarDespuesUtc, LeaseHastaUtc, DetectadoUtc),
    IdRadarMessageProcessing;";

        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@max", SqlDbType.Int).Value = limite;
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            items.Add(new RadarPendingProcessingItem
            {
                IdRadarMessageProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]),
                ChatOrigen = dr["ChatOrigen"].ToString() ?? string.Empty,
                MessageId = dr["MessageId"].ToString() ?? string.Empty,
                Autor = dr["Autor"] == DBNull.Value ? null : dr["Autor"].ToString(),
                Telefono = dr["Telefono"] == DBNull.Value ? null : dr["Telefono"].ToString(),
                MensajeOriginal = dr["MensajeOriginal"] == DBNull.Value
                    ? string.Empty
                    : dr["MensajeOriginal"].ToString() ?? string.Empty,
                Estado = dr["Estado"].ToString() ?? string.Empty,
                IntentosProcesamiento = Convert.ToInt32(dr["IntentosProcesamiento"]),
                DetectadoUtc = Convert.ToDateTime(dr["DetectadoUtc"]),
                ReintentarDespuesUtc = dr["ReintentarDespuesUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["ReintentarDespuesUtc"]),
                LeaseHastaUtc = dr["LeaseHastaUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["LeaseHastaUtc"])
            });
        }

        return items;
    }
}