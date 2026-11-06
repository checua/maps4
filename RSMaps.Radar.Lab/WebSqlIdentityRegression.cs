using maps4.Diagnostics;
using maps4.Repositorios.Implementacion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

internal static class WebSqlIdentityRegression
{
    private static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); }
    private static void Denied(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected rejection"); }

    internal static void MakePrivate(string path)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows test required");
        var acl = new DirectorySecurity();
        var user = WindowsIdentity.GetCurrent().User!;
        acl.SetOwner(user); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "radar-worker-test-" + Guid.NewGuid().ToString("N"));
        MakePrivate(root);
        string content = Path.Combine(root, "web"); Directory.CreateDirectory(Path.Combine(content, "wwwroot"));
        string data = Path.Combine(root, "private"); MakePrivate(data);
        var context = new DiagnosticContext("test-site", "test-slot", "instance1", new string('A', 64), content, Path.Combine(content, "wwwroot"));
        var now = DateTimeOffset.UtcNow;
        DiagnosticManifest Manifest() => new(Guid.NewGuid().ToString("N"), context.Site, context.Slot,
            context.AssemblySha256, [context.Instance], now, now.AddMinutes(10));
        async Task Write(DiagnosticManifest m) => await File.WriteAllTextAsync(Path.Combine(data, "manifest.json"), JsonSerializer.Serialize(m));
        int calls = 0;
        Task<Dictionary<string, object?>> Read(CancellationToken _) { Interlocked.Increment(ref calls); return Task.FromResult(new Dictionary<string, object?> { ["DatabaseName"] = "synthetic" }); }
        Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, default) == "DISABLED", "disabled");
        foreach (var m in new[] { Manifest() with { ExpiresUtc = now.AddSeconds(-1) }, Manifest() with { IssuedUtc = now.AddMinutes(1) },
            Manifest() with { ExpiresUtc = now.AddHours(1) }, Manifest() with { Site = "wrong" }, Manifest() with { Slot = "wrong" },
            Manifest() with { AssemblySha256 = new string('B',64) }, Manifest() with { Instances = ["other"] }, Manifest() with { RunId = "../escape" } })
        { await Write(m); Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, default) == "INVALID", "manifest guard"); }
        Check(calls == 0, "invalid connected");
        var valid = Manifest(); await Write(valid);
        var race = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => WebSqlIdentityRunner.RunAsync(data, context, Read, default)));
        Check(race.Count(x => x == "OBSERVED") == 1 && calls == 1, "single execution");
        Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, default) == "CONSUMED", "restart replay");
        File.Delete(Path.Combine(data, "manifest.json")); // Exact local synthetic control only.
        Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, default) == "DISABLED", "disarmed");
        await Write(Manifest());
        Check(await WebSqlIdentityRunner.RunAsync(data, context, async token => { await Task.Delay(10000, token); return []; }, default, TimeSpan.FromMilliseconds(80)) == "CANCELLED_OR_TIMEOUT", "timeout");
        Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, default) == "CONSUMED", "timeout retry");
        await Write(Manifest());
        using (var cancel = new CancellationTokenSource())
        {
            cancel.CancelAfter(80);
            Check(await WebSqlIdentityRunner.RunAsync(data, context, async token => { await Task.Delay(10000, token); return []; }, cancel.Token) == "CANCELLED_OR_TIMEOUT", "cancel");
        }
        const string sentinel = "synthetic-sensitive-value-do-not-emit";
        await Write(Manifest());
        Check(await WebSqlIdentityRunner.RunAsync(data, context, _ => throw new Exception(sentinel), default) == "INCONCLUSIVE", "error sanitization");
        await Write(Manifest());
        Check(await WebSqlIdentityRunner.RunAsync(data, context, _ => Task.FromResult(new Dictionary<string, object?> { ["ConnectionString"] = sentinel }), default) == "INCONCLUSIVE", "field allowlist");
        await Write(Manifest());
        Check(await WebSqlIdentityRunner.RunAsync(data, context, async token => { await Write(Manifest()); return await Read(token); }, default) == "INCONCLUSIVE", "manifest change");
        Check(!Directory.GetFiles(data, "*.json").Any(p => File.ReadAllText(p).Contains(sentinel)), "secret output");
        Denied(() => DiagnosticPrivateFiles.Validate(content, content, context.WebRoot));
        Denied(() => DiagnosticPrivateFiles.Validate(context.WebRoot, content, context.WebRoot));
        if (OperatingSystem.IsWindows())
        {
            string broad = Path.Combine(root, "broad"); MakePrivate(broad);
            var acl = new DirectoryInfo(broad).GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
            new DirectoryInfo(broad).SetAccessControl(acl);
            Denied(() => DiagnosticPrivateFiles.Validate(broad, content, context.WebRoot));
        }
        var nullManifest = Manifest(); await Write(nullManifest);
        Check(await WebSqlIdentityRunner.RunAsync(data, context, _ => Task.FromResult(new Dictionary<string, object?> { ["RSMAPS_RadarAgentPairing.SELECT"] = null }), default) == "OBSERVED", "null allowed");
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(data, nullManifest.RunId + "-instance1.json"))))
            Check(doc.RootElement.GetProperty("Fields").GetProperty("RSMAPS_RadarAgentPairing.SELECT").ValueKind == JsonValueKind.Null, "null preserved");
        await Write(Manifest());
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            int before = calls;
            Check(await WebSqlIdentityRunner.RunAsync(data, context, Read, cancelled.Token) == "CANCELLED" && calls == before, "cancel before SQL");
        }
        await Write(Manifest());
        var oldOut = Console.Out; var oldError = Console.Error;
        using (var captured = new StringWriter())
        {
            try
            {
                Console.SetOut(captured); Console.SetError(captured);
                Check(await WebSqlIdentityRunner.RunAsync(data, context, _ => throw new Exception(sentinel), default) == "INCONCLUSIVE", "secret exception");
            }
            finally { Console.SetOut(oldOut); Console.SetError(oldError); }
            Check(captured.ToString().Length == 0, "diagnostic console leak");
        }
        await Write(Manifest());
        var blackhole = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); blackhole.Start();
        try
        {
            int port = ((System.Net.IPEndPoint)blackhole.LocalEndpoint).Port;
            var settings = new SqlConnectionStringBuilder { DataSource = "127.0.0.1," + port, InitialCatalog = "synthetic", IntegratedSecurity = true, Encrypt = SqlConnectionEncryptOption.Optional };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:cadenaSQL"] = settings.ConnectionString }).Build();
            var repository = new RadarMessageProcessingRepository(cfg);
            Check(await WebSqlIdentityRunner.RunAsync(data, context, async token =>
            {
                await using var connection = repository.CreateIdentityDiagnosticConnection(cfg);
                await connection.OpenAsync(token);
                return new Dictionary<string,object?>();
            }, default, TimeSpan.FromMilliseconds(150)) == "CANCELLED_OR_TIMEOUT", "SQL open cancellation");
        }
        finally { blackhole.Stop(); }
        await ConfigurationAsync(root);
        await SqlAsync();
        await HttpIsolationAsync(content, data);
        Console.WriteLine("WEB_SQL_IDENTITY_MANIFEST_RACE_RESTART_TIMEOUT_CANCEL_PRIVACY_OK");
        Console.WriteLine("WEB_SQL_IDENTITY_CONFIG_SQL_HTTP_OK");
        // Keep only synthetic evidence under a random private temp directory; no production artifacts.
    }

    private static async Task ConfigurationAsync(string root)
    {
        string json = Path.Combine(root, "config.json");
        await File.WriteAllTextAsync(json, "{\"ConnectionStrings\":{\"cadenaSQL\":\"Server=json;Database=synthetic;Integrated Security=true\"}}");
        string? old = Environment.GetEnvironmentVariable("SQLCONNSTR_cadenaSQL");
        try
        {
            Environment.SetEnvironmentVariable("SQLCONNSTR_cadenaSQL", "Server=env;Database=synthetic;Integrated Security=true");
            var config = new ConfigurationBuilder().AddJsonFile(json).AddEnvironmentVariables().AddCommandLine(
                ["--ConnectionStrings:cadenaSQL=Server=arg;Database=synthetic;Integrated Security=true"]).Build();
            var repo = new RadarMessageProcessingRepository(config);
            using var connection = repo.CreateIdentityDiagnosticConnection(config);
            var b = new SqlConnectionStringBuilder(connection.ConnectionString);
            Check(b.DataSource == "arg" && b.InitialCatalog == "synthetic" && b.IntegratedSecurity && b.ConnectRetryCount == 0 && !b.Pooling, "precedence/captured auth");
            config["ConnectionStrings:cadenaSQL"] = "Server=changed;Database=synthetic;Integrated Security=true";
            Denied(() => repo.CreateIdentityDiagnosticConnection(config));
            var env = new ConfigurationBuilder().AddJsonFile(json).AddEnvironmentVariables().Build();
            Check(new SqlConnectionStringBuilder(env.GetConnectionString("cadenaSQL")).DataSource == "env", "env precedence");
            var onlyJson = new ConfigurationBuilder().AddJsonFile(json).Build();
            Check(new SqlConnectionStringBuilder(onlyJson.GetConnectionString("cadenaSQL")).DataSource == "json", "json provider");
            Denied(() => WebSqlIdentityHostedService.ValidateProductionTarget(connection.ConnectionString));
            Denied(() => WebSqlIdentityHostedService.ValidateProductionTarget("Server=dpa6ecad46.database.windows.net;Database=mapsMarkers;Encrypt=false;Integrated Security=true"));
            Denied(() => WebSqlIdentityHostedService.ValidateProductionTarget("Server=dpa6ecad46.database.windows.net;Database=mapsMarkers;TrustServerCertificate=true;Integrated Security=true"));
        }
        finally { Environment.SetEnvironmentVariable("SQLCONNSTR_cadenaSQL", old); }
    }

    private static async Task SqlAsync()
    {
        string database = "RadarWorkerIdentity_" + Guid.NewGuid().ToString("N");
        await using var admin = new SqlConnection(LocalSqlRegression.MasterConnectionString()); await admin.OpenAsync();
        async Task Exec(string sql) { using var cmd = new SqlCommand(sql, admin); await cmd.ExecuteNonQueryAsync(); }
        bool created = false;
        try
        {
            await Exec($"CREATE DATABASE [{database}]"); created = true; await admin.ChangeDatabaseAsync(database);
            await Exec("CREATE TABLE dbo.RSMAPS_RadarMessageProcessing(Id int); CREATE TABLE dbo.RSMAPS_RadarMessageEvent(Id int); CREATE TABLE dbo.RSMAPS_RadarMessageDelivery(Id int); CREATE USER WorkerRestricted WITHOUT LOGIN; GRANT SELECT,INSERT,UPDATE ON dbo.RSMAPS_RadarMessageProcessing TO WorkerRestricted; GRANT INSERT ON dbo.RSMAPS_RadarMessageEvent TO WorkerRestricted; GRANT SELECT ON dbo.RSMAPS_RadarMessageDelivery TO WorkerRestricted;");
            var settings = new SqlConnectionStringBuilder(LocalSqlRegression.MasterConnectionString()) { InitialCatalog = database };
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:cadenaSQL"] = settings.ConnectionString }).Build();
            var repo = new RadarMessageProcessingRepository(config);
            await using var connection = repo.CreateIdentityDiagnosticConnection(config); await connection.OpenAsync();
            var permitted = await WebSqlIdentityQuery.ReadAsync(connection, database, default);
            Check(Convert.ToInt32(permitted["DbOwner"]) == 1 && Convert.ToInt32(permitted["RSMAPS_RadarMessageDelivery.INSERT"]) == 1, "owner permissions");
            using var setup = connection.CreateCommand(); setup.CommandText = "EXECUTE AS USER='WorkerRestricted'"; await setup.ExecuteNonQueryAsync();
            try
            {
                var restricted = await WebSqlIdentityQuery.ReadAsync(connection, database, default);
                Check((string?)restricted["DatabaseUser"] == "WorkerRestricted" && Convert.ToInt32(restricted["RSMAPS_RadarMessageProcessing.UPDATE"]) == 1 && Convert.ToInt32(restricted["DbOwner"]) == 0, "restricted allowed");
                Check(Convert.ToInt32(restricted["RSMAPS_RadarMessageDelivery.INSERT"]) == 0 && (restricted["RSMAPS_RadarAgentPairing.SELECT"] is null or 0), "restricted missing/denied");
            }
            finally { setup.CommandText = "REVERT"; await setup.ExecuteNonQueryAsync(); }
            try { await WebSqlIdentityQuery.ReadAsync(connection, "WrongDatabase", default); throw new Exception("Missing target guard"); } catch (InvalidOperationException) { }
            setup.CommandText = "SELECT (SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageProcessing)+(SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageEvent)+(SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageDelivery)";
            Check(Convert.ToInt32(await setup.ExecuteScalarAsync()) == 0, "diagnostic wrote rows");
            Check(WebSqlIdentityQuery.IdentitySql.TrimStart().StartsWith("SELECT ") && WebSqlIdentityQuery.PermissionSql.StartsWith("SELECT ") && !WebSqlIdentityQuery.PermissionSql.Contains("EXEC "), "fixed SELECT contract");
        }
        finally
        {
            if (created) { await admin.ChangeDatabaseAsync("master"); await Exec($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]"); }
        }
    }

    private static async Task HttpIsolationAsync(string content, string data)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = content, WebRootPath = Path.Combine(content, "wwwroot") });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build(); app.UseStaticFiles(); await app.StartAsync();
        try
        {
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { BaseAddress = new Uri(address) };
            foreach (string path in new[] { "/manifest.json", "/data/radar-sql-identity/manifest.json", "/private/manifest.json", "/../private/manifest.json" })
                Check((await http.GetAsync(path)).StatusCode == System.Net.HttpStatusCode.NotFound, "private HTTP exposure");
        }
        finally { await app.StopAsync(); }
    }
}
