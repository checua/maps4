using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarAgentChatDiscoveryCommandRepository : IRadarAgentChatDiscoveryCommandRepository
{
    private readonly string _cadenaSQL;

    public RadarAgentChatDiscoveryCommandRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<RadarAgentChatDiscoveryCommandState?> ObtenerEstadoAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || idCuenta <= 0 || idAgent == Guid.Empty)
            return null;

        const string sql = @"
SELECT
    d.IdAgent,
    d.ExploracionChatsSolicitadaUtc,
    d.ExploracionChatsCompletadaUtc
FROM dbo.RSMAPS_RadarAgentDevice d
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = d.IdAsesor
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = d.IdAsesor
   AND cu.IdCuenta = d.IdCuenta
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = d.IdCuenta
   AND c.Activo = 1
WHERE d.IdAgent = @idAgent
  AND d.IdCuenta = @idCuenta
  AND u.correo = @correo;";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
        cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo.Trim();

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        return await dr.ReadAsync(cancellationToken) ? Mapear(dr) : null;
    }

    public async Task<RadarAgentChatDiscoveryCommandState?> ObtenerEstadoAgentAsync(
        Guid idAgent,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return null;

        const string sql = @"
SELECT
    d.IdAgent,
    d.ExploracionChatsSolicitadaUtc,
    d.ExploracionChatsCompletadaUtc
FROM dbo.RSMAPS_RadarAgentDevice d
WHERE d.IdAgent = @idAgent
  AND d.Activo = 1
  AND d.RevocadoUtc IS NULL;";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        return await dr.ReadAsync(cancellationToken) ? Mapear(dr) : null;
    }

    public async Task<DateTime?> SolicitarAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || idCuenta <= 0 || idAgent == Guid.Empty)
            return null;

        const string sql = @"
UPDATE d
SET ExploracionChatsSolicitadaUtc = SYSUTCDATETIME()
OUTPUT inserted.ExploracionChatsSolicitadaUtc
FROM dbo.RSMAPS_RadarAgentDevice d
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = d.IdAsesor
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = d.IdAsesor
   AND cu.IdCuenta = d.IdCuenta
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = d.IdCuenta
   AND c.Activo = 1
WHERE d.IdAgent = @idAgent
  AND d.IdCuenta = @idCuenta
  AND u.correo = @correo
  AND d.Activo = 1
  AND d.RevocadoUtc IS NULL;";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
        cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo.Trim();

        object? valor = await cmd.ExecuteScalarAsync(cancellationToken);
        return valor is null or DBNull ? null : Convert.ToDateTime(valor);
    }

    public async Task<bool> CompletarAsync(
        Guid idAgent,
        DateTime solicitudUtc,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return false;

        const string sql = @"
IF EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_RadarAgentDevice
    WHERE IdAgent = @idAgent
      AND Activo = 1
      AND RevocadoUtc IS NULL
      AND ExploracionChatsSolicitadaUtc = @solicitudUtc
      AND ExploracionChatsCompletadaUtc >= @solicitudUtc
)
BEGIN
    SELECT CAST(1 AS bit);
    RETURN;
END;

UPDATE dbo.RSMAPS_RadarAgentDevice
SET ExploracionChatsCompletadaUtc = @solicitudUtc
WHERE IdAgent = @idAgent
  AND Activo = 1
  AND RevocadoUtc IS NULL
  AND ExploracionChatsSolicitadaUtc = @solicitudUtc
  AND (ExploracionChatsCompletadaUtc IS NULL OR ExploracionChatsCompletadaUtc < @solicitudUtc);

SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS bit);";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        cmd.Parameters.Add("@solicitudUtc", SqlDbType.DateTime2).Value = solicitudUtc;

        object? valor = await cmd.ExecuteScalarAsync(cancellationToken);
        return valor != null && valor != DBNull.Value && Convert.ToBoolean(valor);
    }

    private static RadarAgentChatDiscoveryCommandState Mapear(SqlDataReader dr) => new()
    {
        IdAgent = (Guid)dr["IdAgent"],
        SolicitadaUtc = dr["ExploracionChatsSolicitadaUtc"] == DBNull.Value
            ? null
            : Convert.ToDateTime(dr["ExploracionChatsSolicitadaUtc"]),
        CompletadaUtc = dr["ExploracionChatsCompletadaUtc"] == DBNull.Value
            ? null
            : Convert.ToDateTime(dr["ExploracionChatsCompletadaUtc"])
    };
}
