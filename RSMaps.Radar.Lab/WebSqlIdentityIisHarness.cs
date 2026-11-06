using maps4.Diagnostics;
using maps4.Repositorios.Implementacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

// Synthetic local IIS Express harness only. Not included in the Web project/publication.
internal static class WebSqlIdentityIisHarness
{
    internal static async Task RunAsync()
    {
        if (Process.GetCurrentProcess().ProcessName != "iisexpress") throw new InvalidOperationException();
        string root = Environment.GetEnvironmentVariable("RADAR_IIS_TEST_ROOT") ?? throw new InvalidOperationException();
        string full = Path.GetFullPath(root);
        if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith("radar-iis-", StringComparison.Ordinal)) throw new InvalidOperationException();
        string data = Path.Combine(full, "private"); WebSqlIdentityRegression.MakePrivate(data);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Path.Combine(full,"site"), WebRootPath = Path.Combine(full,"site","wwwroot") });
        var app = builder.Build(); app.UseStaticFiles();
        Task? test = null;
        app.Lifetime.ApplicationStarted.Register(() => test = Task.Run(async () =>
        {
            string status = "FAILED";
            string database = "RadarWorkerIis_" + Guid.NewGuid().ToString("N");
            bool created = false;
            await using var admin = new SqlConnection(LocalSqlRegression.MasterConnectionString());
            try
            {
                await admin.OpenAsync();
                using var create = admin.CreateCommand(); create.CommandText = $"CREATE DATABASE [{database}]"; await create.ExecuteNonQueryAsync(); created = true;
                string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(RadarMessageProcessingRepository).Assembly.Location)));
                var ctx = new DiagnosticContext("local-iis", "test", "iis-instance", hash, Path.Combine(full,"site"), Path.Combine(full,"site","wwwroot"));
                var manifest = new DiagnosticManifest("iis-run", ctx.Site,ctx.Slot,hash,[ctx.Instance],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddMinutes(5));
                await File.WriteAllTextAsync(Path.Combine(data,"manifest.json"),JsonSerializer.Serialize(manifest));
                var settings = new SqlConnectionStringBuilder(LocalSqlRegression.MasterConnectionString()) { InitialCatalog=database };
                builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:cadenaSQL"]=settings.ConnectionString });
                var repository = new RadarMessageProcessingRepository(builder.Configuration);
                status = await WebSqlIdentityRunner.RunAsync(data,ctx,async token =>
                {
                    await using var connection = repository.CreateIdentityDiagnosticConnection(builder.Configuration);
                    await connection.OpenAsync(token);
                    return await WebSqlIdentityQuery.ReadAsync(connection,database,token);
                },app.Lifetime.ApplicationStopping);
            }
            catch { status="FAILED"; }
            finally
            {
                if(created)
                {
                    using var cleanup=admin.CreateCommand(); cleanup.CommandText=$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
                    await cleanup.ExecuteNonQueryAsync();
                }
                await File.WriteAllTextAsync(Path.Combine(data,"harness-status.json"),JsonSerializer.Serialize(new {Status=status, Process=Process.GetCurrentProcess().ProcessName,Pid=Environment.ProcessId,DatabaseRemoved=created}));
            }
        }));
        await app.RunAsync();
        if(test is not null) await test;
    }
}
