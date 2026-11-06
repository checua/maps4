using Microsoft.Data.SqlClient;
using System.Text.Json;

internal static class SqlIdentityDiagnostic
{
    // No SQL text, parameters, connection properties or free-form exceptions are emitted.
    private const string Query = """
        SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
        DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS OriginalLogin,
        SUSER_SNAME() AS ExecutionLogin, USER_NAME() AS DatabaseUser,
        IS_MEMBER('db_owner') AS DbOwner,
        HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL') AS DatabaseControl,
        HAS_PERMS_BY_NAME('dbo','SCHEMA','ALTER') AS AlterSchema,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageProcessing','OBJECT','SELECT') AS ProcessingSelect,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageProcessing','OBJECT','INSERT') AS ProcessingInsert,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageProcessing','OBJECT','UPDATE') AS ProcessingUpdate,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageProcessing','OBJECT','ALTER') AS ProcessingAlter,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageEvent','OBJECT','INSERT') AS EventInsert,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageDelivery','OBJECT','SELECT') AS DeliverySelect,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageDelivery','OBJECT','INSERT') AS DeliveryInsert,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarMessageDelivery','OBJECT','UPDATE') AS DeliveryUpdate,
        HAS_PERMS_BY_NAME('dbo.RSMAPS_RadarReactivateProcessing','OBJECT','EXECUTE') AS AdminReactivateExecute;
        """;

    public static async Task RunAsync()
    {
        if (Environment.GetEnvironmentVariable("RADAR_SQL_IDENTITY_AUTHORIZED") != "1")
            throw new InvalidOperationException();
        string[] names = ["SQLCONNSTR_cadenaSQL", "SQLAZURECONNSTR_cadenaSQL",
            "CUSTOMCONNSTR_cadenaSQL", "ConnectionStrings__cadenaSQL", "ConnectionStrings:cadenaSQL"];
        var present = names.Where(n => Environment.GetEnvironmentVariable(n) is not null).ToArray();
        // Ambiguous aliases must be resolved by inspecting actual application providers.
        if (present.Length != 1) throw new InvalidOperationException();
        string? value = Environment.GetEnvironmentVariable(present[0]);
        try
        {
            var settings = new SqlConnectionStringBuilder(value);
            if (!string.Equals(settings.InitialCatalog, "mapsMarkers", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException();
            settings.PersistSecurityInfo = false;
            settings.ConnectTimeout = 10;
            settings.Pooling = false;
            await using var connection = new SqlConnection(settings.ConnectionString);
            settings.Clear();
            await connection.OpenAsync();
            Console.WriteLine(await ReadAsync(connection));
        }
        finally { value = null; }
    }

    private static async Task<string> ReadAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(Query, connection) { CommandTimeout = 10 };
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException();
        var result = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            result.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
        return JsonSerializer.Serialize(result);
    }

    public static async Task TestAsync()
    {
        string master = LocalSqlRegression.MasterConnectionString();
        string database = "RadarIdentityTest_" + Guid.NewGuid().ToString("N");
        await using var admin = new SqlConnection(master);
        await admin.OpenAsync();
        async Task Execute(string sql)
        {
            await using var command = new SqlCommand(sql, admin);
            await command.ExecuteNonQueryAsync();
        }
        bool created = false;
        try
        {
            await Execute($"CREATE DATABASE [{database}]");
            created = true;
            await admin.ChangeDatabaseAsync(database);
            await Execute("CREATE TABLE dbo.RSMAPS_RadarMessageProcessing(Id int); CREATE TABLE dbo.RSMAPS_RadarMessageEvent(Id int); CREATE TABLE dbo.RSMAPS_RadarMessageDelivery(Id int); CREATE USER IdentityAppTest WITHOUT LOGIN; GRANT SELECT,INSERT,UPDATE ON dbo.RSMAPS_RadarMessageProcessing TO IdentityAppTest; GRANT INSERT ON dbo.RSMAPS_RadarMessageEvent TO IdentityAppTest;");
            using var privileged = JsonDocument.Parse(await ReadAsync(admin));
            if (privileged.RootElement.GetProperty("DbOwner").GetInt32() != 1) throw new InvalidOperationException();
            await Execute("EXECUTE AS USER='IdentityAppTest'");
            string output;
            try { output = await ReadAsync(admin); }
            finally { await Execute("REVERT"); }
            using var restricted = JsonDocument.Parse(output);
            var user = restricted.RootElement;
            if (user.GetProperty("DatabaseUser").GetString() != "IdentityAppTest" ||
                user.GetProperty("DbOwner").GetInt32() != 0 ||
                user.GetProperty("ProcessingUpdate").GetInt32() != 1 ||
                user.GetProperty("ProcessingAlter").GetInt32() != 0 ||
                user.GetProperty("DatabaseControl").GetInt32() != 0)
                throw new InvalidOperationException();
            if (user.EnumerateObject().Count() != 17 || output.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException();
            Console.WriteLine("SQL_IDENTITY_ADMIN_AND_RESTRICTED_USER_OK");
            Console.WriteLine("SQL_IDENTITY_OUTPUT_ALLOWLIST_OK");
        }
        finally
        {
            await admin.ChangeDatabaseAsync("master");
            if (created) await Execute($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }
}
