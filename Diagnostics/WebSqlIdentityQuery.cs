using Microsoft.Data.SqlClient;

namespace maps4.Diagnostics;

// Fixed metadata only. Never executes business commands or caller-supplied SQL.
internal static class WebSqlIdentityQuery
{
    internal const string IdentitySql = """
        SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
        DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS OriginalLogin,
        SUSER_SNAME() AS ExecutionLogin, USER_NAME() AS DatabaseUser,
        CONVERT(nvarchar(40), CONNECTIONPROPERTY('auth_scheme')) AS AuthScheme,
        @@SPID AS SessionId, IS_MEMBER('db_owner') AS DbOwner,
        HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL') AS DatabaseControl,
        HAS_PERMS_BY_NAME('dbo','SCHEMA','ALTER') AS AlterSchema;
        """;

    internal static readonly (string Object, string Permission)[] Permissions =
    [
        ("RSMAPS_RadarMessageProcessing", "SELECT"), ("RSMAPS_RadarMessageProcessing", "INSERT"),
        ("RSMAPS_RadarMessageProcessing", "UPDATE"), ("RSMAPS_RadarMessageProcessing", "ALTER"),
        ("RSMAPS_RadarMessageEvent", "INSERT"),
        ("RSMAPS_RadarMessageDelivery", "SELECT"), ("RSMAPS_RadarMessageDelivery", "INSERT"), ("RSMAPS_RadarMessageDelivery", "UPDATE"),
        ("RSMAPS_RadarAgentPairing", "SELECT"), ("RSMAPS_RadarAgentPairing", "INSERT"), ("RSMAPS_RadarAgentPairing", "UPDATE"),
        ("RSMAPS_RadarAgentDevice", "SELECT"), ("RSMAPS_RadarAgentDevice", "INSERT"), ("RSMAPS_RadarAgentDevice", "UPDATE"),
        ("RSMAPS_RadarAgentConfig", "SELECT"), ("RSMAPS_RadarAgentConfig", "INSERT"), ("RSMAPS_RadarAgentConfig", "UPDATE"),
        ("RSMAPS_RadarAgentChatDiscovery", "SELECT"), ("RSMAPS_RadarAgentChatDiscovery", "INSERT"), ("RSMAPS_RadarAgentChatDiscovery", "UPDATE"),
        ("RSMAPS_Usuario", "SELECT"), ("RSMAPS_CuentaUsuario", "SELECT"), ("RSMAPS_Cuenta", "SELECT"),
        ("RSMAPS_sp_ListaInmueblesAutorizados", "EXECUTE"),
        ("RSMAPS_Inmueble", "SELECT"), ("RSMAPS_InmuebleAmenidad", "SELECT"),
        ("RSMAPS_RadarReactivateProcessing", "EXECUTE")
    ];

    internal static string PermissionSql => "SELECT " + string.Join(",", Permissions.Select((p, i) =>
        $"HAS_PERMS_BY_NAME('dbo.{p.Object}','OBJECT','{p.Permission}') AS P{i}")) + ";";

    internal static async Task<Dictionary<string, object?>> ReadAsync(SqlConnection connection,
        string expectedDatabase, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = "SELECT DB_NAME();";
        if (!string.Equals((string?)await command.ExecuteScalarAsync(deadline.Token), expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException();
        var result = new Dictionary<string, object?>();
        command.CommandText = IdentitySql;
        using (var reader = await command.ExecuteReaderAsync(deadline.Token))
        {
            if (!await reader.ReadAsync(deadline.Token)) throw new InvalidOperationException();
            for (int i = 0; i < reader.FieldCount; i++)
                result.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
        }
        command.CommandText = PermissionSql;
        using (var reader = await command.ExecuteReaderAsync(deadline.Token))
        {
            if (!await reader.ReadAsync(deadline.Token)) throw new InvalidOperationException();
            for (int i = 0; i < Permissions.Length; i++)
                result.Add(Permissions[i].Object + "." + Permissions[i].Permission,
                    reader.IsDBNull(i) ? null : reader.GetInt32(i));
        }
        return result;
    }
}
