using System.Diagnostics;
using System.Security.Cryptography;
using maps4.Repositorios.Contrato;
using maps4.Repositorios.Implementacion;
using Microsoft.Data.SqlClient;

namespace maps4.Diagnostics;

internal sealed class WebSqlIdentityHostedService(IServiceScopeFactory scopes, IConfiguration configuration,
    IWebHostEnvironment environment, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken);
            // No local/console/SCM activation. This class never accepts a manual SQL connection.
            if (Process.GetCurrentProcess().ProcessName != "w3wp" ||
                Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") != "rsmap") return;
            string? instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!WebSqlIdentityRunner.SafeId(instance) || string.IsNullOrWhiteSpace(home)) return;
            string slot = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? "Production";
            if (slot != "Production") return;
            string directory = Path.Combine(home, "data", "radar-sql-identity");
            if (!File.Exists(Path.Combine(directory, "manifest.json"))) return;
            string assemblyPath = typeof(WebSqlIdentityHostedService).Assembly.Location;
            string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath, stoppingToken)));
            var context = new DiagnosticContext("rsmap", slot, instance!, hash, environment.ContentRootPath,
                environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
            await WebSqlIdentityRunner.RunAsync(directory, context, async token =>
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IRadarMessageProcessingRepository>()
                    as RadarMessageProcessingRepository ?? throw new InvalidOperationException();
                using var connection = repository.CreateIdentityDiagnosticConnection(configuration);
                ValidateProductionTarget(connection.ConnectionString);
                using var openDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                openDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                await connection.OpenAsync(openDeadline.Token);
                var result = await WebSqlIdentityQuery.ReadAsync(connection, "mapsMarkers", token);
                repository.AssertIdentityDiagnosticConfiguration(configuration);
                if (configuration is not IConfigurationRoot root) throw new InvalidOperationException();
                var providers = root.Providers.ToArray();
                for (int i = providers.Length - 1; i >= 0; i--)
                {
                    if (!providers[i].TryGet("ConnectionStrings:cadenaSQL", out _)) continue;
                    result.Add("ProviderType", providers[i].GetType().FullName);
                    result.Add("ProviderPosition", i);
                    return result;
                }
                throw new InvalidOperationException();
            }, stoppingToken);
        }
        catch { /* A diagnostic failure must never log secrets or stop the Web. */ }
    }

    internal static void ValidateProductionTarget(string captured)
    {
        var b = new SqlConnectionStringBuilder(captured);
        string server = b.DataSource;
        if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) server = server[4..];
        if (server.EndsWith(",1433", StringComparison.Ordinal)) server = server[..^5];
        if (!server.Equals("dpa6ecad46.database.windows.net", StringComparison.OrdinalIgnoreCase) ||
            b.InitialCatalog != "mapsMarkers" || b.TrustServerCertificate ||
            b.Encrypt == SqlConnectionEncryptOption.Optional || b.AttachDBFilename.Length != 0)
            throw new InvalidOperationException();
    }
}
