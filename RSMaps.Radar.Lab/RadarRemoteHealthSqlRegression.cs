using maps4.Models;
using maps4.Repositorios.Implementacion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

internal static class RadarRemoteHealthSqlRegression
{
    public static async Task RunAsync()
    {
        const string server = @".\RSMAPSDEV";
        string database = $"RSMaps_RadarHealth_Test_{Guid.NewGuid():N}";
        string masterConnection = BuildConnection(server, "master");
        string testConnection = BuildConnection(server, database);
        bool created = false;

        try
        {
            await ExecuteAsync(masterConnection, $"CREATE DATABASE [{database}];");
            created = true;
            Console.WriteLine($"LOCAL_SQL_TEST_DATABASE={database}");

            await ExecuteAsync(testConnection, @"
CREATE TABLE dbo.RSMAPS_RadarAgentDevice
(
    IdAgent uniqueidentifier NOT NULL CONSTRAINT PK_TestRadarAgentDevice PRIMARY KEY
);");

            string root = FindRepositoryRoot();
            string script = await File.ReadAllTextAsync(
                Path.Combine(root, "sql", "RSMaps2", "57_radar_agent_health.sql"));
            script = script.Replace(
                "IF DB_NAME() <> N'mapsMarkers'",
                $"IF DB_NAME() <> N'{database}'",
                StringComparison.Ordinal);

            await ExecuteAsync(testConnection, script);
            await ExecuteAsync(testConnection, script);
            await ValidateSchemaAsync(testConnection);
            await ValidateRepositoryAsync(testConnection);
            await ValidateRollbackAsync(testConnection);

            Console.WriteLine("RADAR_REMOTE_HEALTH_SQL_INSTALL_OK");
            Console.WriteLine("RADAR_REMOTE_HEALTH_SQL_IDEMPOTENT_OK");
            Console.WriteLine("RADAR_REMOTE_HEALTH_SQL_CONCURRENT_UPSERT_OK");
            Console.WriteLine("RADAR_REMOTE_HEALTH_SQL_RESTART_REPLAY_OK");
            Console.WriteLine("RADAR_REMOTE_HEALTH_SQL_ROLLBACK_OK");
        }
        finally
        {
            if (created)
            {
                await ExecuteAsync(masterConnection, $@"
ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [{database}];");
                Console.WriteLine("LOCAL_SQL_TEST_DATABASE_REMOVED=YES");
            }
        }
    }

    private static async Task ValidateSchemaAsync(string connectionString)
    {
        const string sql = @"
SELECT
    CASE WHEN OBJECT_ID('dbo.RSMAPS_RadarAgentHealth', 'U') IS NOT NULL THEN 1 ELSE 0 END,
    (SELECT COUNT(*) FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.RSMAPS_RadarAgentHealth') AND type = 'PK'),
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.RSMAPS_RadarAgentHealth')),
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.RSMAPS_RadarAgentHealth')),
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.RSMAPS_RadarAgentHealth') AND name = 'IX_RSMAPS_RadarAgentHealth_RecibidoUtc'),
    (SELECT scale FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RSMAPS_RadarAgentHealth') AND name = 'InstanceStartedUtc');";

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "No se obtuvo metadata SQL.");
        Assert(reader.GetInt32(0) == 1, "Tabla no instalada.");
        Assert(reader.GetInt32(1) == 1, "PK ausente.");
        Assert(reader.GetInt32(2) == 1, "FK ausente.");
        Assert(reader.GetInt32(3) >= 7, "Checks incompletos.");
        Assert(reader.GetInt32(4) == 1, "Índice RecibidoUtc ausente.");
        Assert(reader.GetByte(5) == 7, "InstanceStartedUtc debe preservar precisión subsegundo.");
    }

    private static async Task ValidateRepositoryAsync(string connectionString)
    {
        Guid agentId = Guid.NewGuid();
        await ExecuteAsync(connectionString,
            "INSERT dbo.RSMAPS_RadarAgentDevice(IdAgent) VALUES (@idAgent);",
            new SqlParameter("@idAgent", SqlDbType.UniqueIdentifier) { Value = agentId });

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:cadenaSQL"] = connectionString
            })
            .Build();
        var firstRepository = new RadarAgentHealthRepository(configuration);
        var secondRepository = new RadarAgentHealthRepository(configuration);
        DateTime started = DateTime.UtcNow.AddMinutes(-2);
        Guid firstInstance = Guid.NewGuid();
        RadarAgentHeartbeatRequest first = CreateRequest(firstInstance, 1, started);

        RadarAgentHealthWriteResult[] concurrent = await Task.WhenAll(
            firstRepository.UpsertAsync(agentId, first),
            secondRepository.UpsertAsync(agentId, first));
        Assert(concurrent.Count(x => x == RadarAgentHealthWriteResult.Accepted) == 1, "Concurrencia aceptó más de un heartbeat idéntico.");
        Assert(concurrent.Count(x => x == RadarAgentHealthWriteResult.OutOfOrder) == 1, "Concurrencia no rechazó el duplicado.");

        RadarAgentHeartbeatRequest next = CreateRequest(firstInstance, 2, started);
        Assert(await firstRepository.UpsertAsync(agentId, next) == RadarAgentHealthWriteResult.Accepted, "Secuencia creciente rechazada.");
        Assert(await firstRepository.UpsertAsync(agentId, first) == RadarAgentHealthWriteResult.OutOfOrder, "Secuencia vieja aceptada.");

        Guid secondInstance = Guid.NewGuid();
        RadarAgentHeartbeatRequest restarted = CreateRequest(secondInstance, 1, started.AddMinutes(1));
        Assert(await firstRepository.UpsertAsync(agentId, restarted) == RadarAgentHealthWriteResult.Accepted, "Nueva instancia rechazada.");
        RadarAgentHeartbeatRequest delayedOld = CreateRequest(firstInstance, 99, started);
        Assert(await firstRepository.UpsertAsync(agentId, delayedOld) == RadarAgentHealthWriteResult.PreviousInstance,
            "Instancia anterior tardía sobrescribió la nueva.");

        // Restart within the same second must retain timestamp ordering in SQL Server.
        Guid quickInstance = Guid.NewGuid();
        RadarAgentHeartbeatRequest rapidRestart = CreateRequest(
            quickInstance, 1, restarted.InstanceStartedUtc.AddTicks(1_000));
        Assert(await firstRepository.UpsertAsync(agentId, rapidRestart) == RadarAgentHealthWriteResult.Accepted,
            "Reinicio dentro del mismo segundo rechazado por pérdida de precisión SQL.");
        Assert(await firstRepository.UpsertAsync(agentId, restarted) == RadarAgentHealthWriteResult.PreviousInstance,
            "Heartbeat tardío del proceso anterior fue aceptado tras reinicio rápido.");

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(@"
SELECT InstanceId, Sequence,
       ABS(DATEDIFF(second, RecibidoUtc, SYSUTCDATETIME())) AS ClockDelta
FROM dbo.RSMAPS_RadarAgentHealth
WHERE IdAgent = @idAgent;", connection);
        command.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = agentId;
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Snapshot final ausente.");
        Assert(reader.GetGuid(0) == quickInstance && reader.GetInt64(1) == 1,
            "Resultado final no determinista.");
        Assert(reader.GetInt32(2) <= 5, "RecibidoUtc no usa reloj del servidor.");
    }

    private static async Task ValidateRollbackAsync(string connectionString)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using SqlCommand command = new(@"
INSERT dbo.RSMAPS_RadarAgentDevice(IdAgent) VALUES (@idAgent);", connection, transaction);
        Guid rolledBackAgent = Guid.NewGuid();
        command.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = rolledBackAgent;
        await command.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();

        await using SqlCommand verify = new(
            "SELECT COUNT(*) FROM dbo.RSMAPS_RadarAgentDevice WHERE IdAgent = @idAgent;", connection);
        verify.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = rolledBackAgent;
        Assert(Convert.ToInt32(await verify.ExecuteScalarAsync()) == 0, "Rollback local no revirtió la escritura.");
    }

    private static RadarAgentHeartbeatRequest CreateRequest(Guid instanceId, long sequence, DateTime started) => new()
    {
        InstanceId = instanceId,
        Sequence = sequence,
        InstanceStartedUtc = started,
        AgentUtc = DateTime.UtcNow,
        Version = "sql-regression",
        ListenerState = "Running",
        WhatsAppState = "Ready",
        LastSweepStartedUtc = DateTime.UtcNow.AddMinutes(-1),
        LastSweepCompletedUtc = DateTime.UtcNow,
        ChatsConfigured = 7,
        ChatsReviewed = 7,
        CentralMode = "Central",
        CentralState = "Healthy",
        FallbackEnabled = false
    };

    private static async Task ExecuteAsync(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection) { CommandTimeout = 30 };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnection(string server, string database) =>
        $"Server={server};Database={database};Integrated Security=true;Encrypt=false;TrustServerCertificate=true;Connect Timeout=10";

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "maps4.csproj"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("No se encontró el repositorio.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
