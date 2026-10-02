using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarAgentHealthRepository : IRadarAgentHealthRepository
{
    private readonly string _cadenaSQL;

    public RadarAgentHealthRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<RadarAgentHealthWriteResult> UpsertAsync(
        Guid idAgent,
        RadarAgentHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = new(_cadenaSQL);
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        const string selectSql = @"
SELECT InstanceId, Sequence, InstanceStartedUtc
FROM dbo.RSMAPS_RadarAgentHealth WITH (UPDLOCK, HOLDLOCK)
WHERE IdAgent = @idAgent;";

        Guid? currentInstance = null;
        long currentSequence = 0;
        DateTime currentStartedUtc = DateTime.MinValue;

        await using (SqlCommand select = new(selectSql, connection, transaction))
        {
            select.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                currentInstance = (Guid)reader["InstanceId"];
                currentSequence = Convert.ToInt64(reader["Sequence"]);
                currentStartedUtc = Convert.ToDateTime(reader["InstanceStartedUtc"]);
            }
        }

        RadarAgentHealthWriteResult orderResult = RadarAgentHeartbeatOrderPolicy.Evaluate(
            currentInstance,
            currentSequence,
            currentStartedUtc,
            request);
        if (orderResult != RadarAgentHealthWriteResult.Accepted)
        {
            await transaction.RollbackAsync(cancellationToken);
            return orderResult;
        }

        const string writeSql = @"
IF EXISTS (SELECT 1 FROM dbo.RSMAPS_RadarAgentHealth WHERE IdAgent = @idAgent)
BEGIN
    UPDATE dbo.RSMAPS_RadarAgentHealth
    SET InstanceId = @instanceId,
        Sequence = @sequence,
        InstanceStartedUtc = @instanceStartedUtc,
        RecibidoUtc = SYSUTCDATETIME(),
        AgentUtc = @agentUtc,
        Version = @version,
        ListenerState = @listenerState,
        WhatsAppState = @whatsAppState,
        WhatsAppStateSinceUtc = @whatsAppStateSinceUtc,
        LastSweepStartedUtc = @lastSweepStartedUtc,
        LastSweepCompletedUtc = @lastSweepCompletedUtc,
        ChatsConfigured = @chatsConfigured,
        ChatsReviewed = @chatsReviewed,
        CentralMode = @centralMode,
        CentralState = @centralState,
        LastCentralSuccessUtc = @lastCentralSuccessUtc,
        FallbackEnabled = @fallbackEnabled,
        ErrorCode = @errorCode,
        ActualizadoUtc = SYSUTCDATETIME()
    WHERE IdAgent = @idAgent;
END
ELSE
BEGIN
    INSERT dbo.RSMAPS_RadarAgentHealth
    (
        IdAgent, InstanceId, Sequence, InstanceStartedUtc, RecibidoUtc, AgentUtc,
        Version, ListenerState, WhatsAppState, WhatsAppStateSinceUtc,
        LastSweepStartedUtc, LastSweepCompletedUtc, ChatsConfigured, ChatsReviewed,
        CentralMode, CentralState, LastCentralSuccessUtc, FallbackEnabled, ErrorCode,
        ActualizadoUtc
    )
    VALUES
    (
        @idAgent, @instanceId, @sequence, @instanceStartedUtc, SYSUTCDATETIME(), @agentUtc,
        @version, @listenerState, @whatsAppState, @whatsAppStateSinceUtc,
        @lastSweepStartedUtc, @lastSweepCompletedUtc, @chatsConfigured, @chatsReviewed,
        @centralMode, @centralState, @lastCentralSuccessUtc, @fallbackEnabled, @errorCode,
        SYSUTCDATETIME()
    );
END";

        await using (SqlCommand write = new(writeSql, connection, transaction))
        {
            AddParameters(write, idAgent, request);
            await write.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return RadarAgentHealthWriteResult.Accepted;
    }

    public async Task<List<RadarAgentHealthRecord>> ListForOwnerAsync(
        string correo,
        int idCuenta,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || idCuenta <= 0)
            return [];

        const string sql = @"
SELECT d.IdAgent,
       d.NombreAgent,
       h.InstanceId,
       h.Sequence,
       h.InstanceStartedUtc,
       h.RecibidoUtc,
       h.AgentUtc,
       h.Version,
       h.ListenerState,
       h.WhatsAppState,
       h.WhatsAppStateSinceUtc,
       h.LastSweepStartedUtc,
       h.LastSweepCompletedUtc,
       h.ChatsConfigured,
       h.ChatsReviewed,
       h.CentralMode,
       h.CentralState,
       h.LastCentralSuccessUtc,
       h.FallbackEnabled,
       h.ErrorCode,
       COALESCE(cfg.IntervaloRevisionMs, 60000) AS IntervaloRevisionMs
FROM dbo.RSMAPS_RadarAgentDevice d
INNER JOIN dbo.RSMAPS_Usuario u
    ON u.idAsesor = d.IdAsesor
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = d.IdAsesor
   AND cu.IdCuenta = d.IdCuenta
   AND cu.Activo = 1
   AND cu.RolCodigo = 'PROPIETARIO'
LEFT JOIN dbo.RSMAPS_RadarAgentHealth h
    ON h.IdAgent = d.IdAgent
LEFT JOIN dbo.RSMAPS_RadarAgentConfig cfg
    ON cfg.IdAgent = d.IdAgent
WHERE u.correo = @correo
  AND d.IdCuenta = @idCuenta
  AND d.Activo = 1
  AND d.RevocadoUtc IS NULL
ORDER BY d.NombreAgent;";

        var result = new List<RadarAgentHealthRecord>();
        await using SqlConnection connection = new(_cadenaSQL);
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo.Trim();
        command.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new RadarAgentHealthRecord
            {
                IdAgent = (Guid)reader["IdAgent"],
                NombreAgent = reader["NombreAgent"].ToString() ?? string.Empty,
                IntervaloRevisionMs = Convert.ToInt32(reader["IntervaloRevisionMs"])
            };

            if (reader["InstanceId"] != DBNull.Value)
            {
                item.InstanceId = (Guid)reader["InstanceId"];
                item.Sequence = Convert.ToInt64(reader["Sequence"]);
                item.InstanceStartedUtc = Convert.ToDateTime(reader["InstanceStartedUtc"]);
                item.ReceivedUtc = Convert.ToDateTime(reader["RecibidoUtc"]);
                item.AgentUtc = Convert.ToDateTime(reader["AgentUtc"]);
                item.Version = reader["Version"].ToString() ?? string.Empty;
                item.ListenerState = reader["ListenerState"].ToString() ?? string.Empty;
                item.WhatsAppState = reader["WhatsAppState"].ToString() ?? string.Empty;
                item.WhatsAppStateSinceUtc = ReadNullableDate(reader, "WhatsAppStateSinceUtc");
                item.LastSweepStartedUtc = ReadNullableDate(reader, "LastSweepStartedUtc");
                item.LastSweepCompletedUtc = ReadNullableDate(reader, "LastSweepCompletedUtc");
                item.ChatsConfigured = Convert.ToInt32(reader["ChatsConfigured"]);
                item.ChatsReviewed = Convert.ToInt32(reader["ChatsReviewed"]);
                item.CentralMode = reader["CentralMode"].ToString() ?? string.Empty;
                item.CentralState = reader["CentralState"].ToString() ?? string.Empty;
                item.LastCentralSuccessUtc = ReadNullableDate(reader, "LastCentralSuccessUtc");
                item.FallbackEnabled = Convert.ToBoolean(reader["FallbackEnabled"]);
                item.ErrorCode = reader["ErrorCode"] == DBNull.Value ? null : reader["ErrorCode"].ToString();
            }

            result.Add(item);
        }

        return result;
    }

    private static DateTime? ReadNullableDate(SqlDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToDateTime(reader[name]);

    private static void AddParameters(SqlCommand command, Guid idAgent, RadarAgentHeartbeatRequest request)
    {
        command.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        command.Parameters.Add("@instanceId", SqlDbType.UniqueIdentifier).Value = request.InstanceId;
        command.Parameters.Add("@sequence", SqlDbType.BigInt).Value = request.Sequence;
        command.Parameters.Add("@instanceStartedUtc", SqlDbType.DateTime2).Value = request.InstanceStartedUtc;
        command.Parameters.Add("@agentUtc", SqlDbType.DateTime2).Value = request.AgentUtc;
        command.Parameters.Add("@version", SqlDbType.NVarChar, 64).Value = request.Version.Trim();
        command.Parameters.Add("@listenerState", SqlDbType.VarChar, 24).Value = request.ListenerState;
        command.Parameters.Add("@whatsAppState", SqlDbType.VarChar, 32).Value = request.WhatsAppState;
        command.Parameters.Add("@whatsAppStateSinceUtc", SqlDbType.DateTime2).Value = (object?)request.WhatsAppStateSinceUtc ?? DBNull.Value;
        command.Parameters.Add("@lastSweepStartedUtc", SqlDbType.DateTime2).Value = (object?)request.LastSweepStartedUtc ?? DBNull.Value;
        command.Parameters.Add("@lastSweepCompletedUtc", SqlDbType.DateTime2).Value = (object?)request.LastSweepCompletedUtc ?? DBNull.Value;
        command.Parameters.Add("@chatsConfigured", SqlDbType.SmallInt).Value = request.ChatsConfigured;
        command.Parameters.Add("@chatsReviewed", SqlDbType.SmallInt).Value = request.ChatsReviewed;
        command.Parameters.Add("@centralMode", SqlDbType.VarChar, 20).Value = request.CentralMode;
        command.Parameters.Add("@centralState", SqlDbType.VarChar, 24).Value = request.CentralState;
        command.Parameters.Add("@lastCentralSuccessUtc", SqlDbType.DateTime2).Value = (object?)request.LastCentralSuccessUtc ?? DBNull.Value;
        command.Parameters.Add("@fallbackEnabled", SqlDbType.Bit).Value = request.FallbackEnabled;
        command.Parameters.Add("@errorCode", SqlDbType.VarChar, 64).Value = (object?)request.ErrorCode ?? DBNull.Value;
    }
}
