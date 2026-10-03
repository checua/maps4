using maps4.Controllers;
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;

internal static class RadarRemoteHealthRegression
{
    public static async Task RunAsync()
    {
        DateTime now = DateTime.UtcNow;
        ValidateContract(now);
        ValidateHealthThresholds(now);
        ValidateReplayAndRestart(now);
        await ValidateApiAuthenticationAsync(now);
        await ValidatePanelTenantScopeAsync(now);
        ValidateInformationScopeAndSql();

        Console.WriteLine("RADAR_REMOTE_HEALTH_CONTRACT_OK");
        Console.WriteLine("RADAR_REMOTE_HEALTH_AUTH_OK");
        Console.WriteLine("RADAR_REMOTE_HEALTH_TENANT_ISOLATION_OK");
        Console.WriteLine("RADAR_REMOTE_HEALTH_REPLAY_RESTART_OK");
        Console.WriteLine("RADAR_REMOTE_HEALTH_CONCURRENT_UPSERT_DESIGN_OK");
        Console.WriteLine("RADAR_REMOTE_HEALTH_REGRESSION_OK");
    }

    private static void ValidateContract(DateTime now)
    {
        RadarAgentHeartbeatRequest valid = CreateValid(now);
        Assert(Validate(valid).Count == 0, "El contrato válido fue rechazado.");

        valid.ErrorCode = "texto libre no permitido";
        Assert(Validate(valid).Count > 0, "Se aceptó errorCode libre.");

        valid = CreateValid(now);
        valid.ChatsReviewed = valid.ChatsConfigured + 1;
        Assert(Validate(valid).Count > 0, "Se aceptó chatsReviewed mayor que chatsConfigured.");

        string[] forbidden = ["Message", "Texto", "Telefono", "Token", "Credential", "Path", "Exception", "HostName"];
        string[] properties = typeof(RadarAgentHeartbeatRequest).GetProperties().Select(x => x.Name).ToArray();
        Assert(!properties.Any(x => forbidden.Any(f => x.Contains(f, StringComparison.OrdinalIgnoreCase))),
            "El DTO contiene un campo sensible.");
        JsonUnmappedMemberHandlingAttribute? strictJson = typeof(RadarAgentHeartbeatRequest)
            .GetCustomAttributes(typeof(JsonUnmappedMemberHandlingAttribute), true)
            .Cast<JsonUnmappedMemberHandlingAttribute>()
            .SingleOrDefault();
        Assert(strictJson?.UnmappedMemberHandling == JsonUnmappedMemberHandling.Disallow,
            "El contrato acepta propiedades JSON desconocidas.");
    }

    private static void ValidateHealthThresholds(DateTime now)
    {
        Assert(RadarAgentHealthPolicy.Evaluate(null, now) == RadarAgentHealthLevel.NoData,
            "Ausencia histórica no se distinguió.");
        RadarAgentHealthRecord item = CreateRecord(now);
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Online, "Online no detectado.");
        item.ReceivedUtc = now.AddSeconds(-121);
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Degraded, "Señal intermedia no degradó.");
        item.ReceivedUtc = now.AddSeconds(-181);
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.NoCommunication, "Silencio no detectado.");
        item = CreateRecord(now);
        item.WhatsAppState = "WaitingForReady";
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Degraded, "WhatsApp degradado no detectado.");
        item = CreateRecord(now);
        item.LastSweepCompletedUtc = now.AddMinutes(-46);
        item.IntervaloRevisionMs = 1_200_000;
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Degraded, "Sweep vencido no detectado.");
        item = CreateRecord(now);
        item.ChatsReviewed = 4;
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Degraded,
            "Un barrido de 4/7 chats no debe mostrarse como Online.");
        item = CreateRecord(now);
        item.ChatsConfigured = 0;
        item.ChatsReviewed = 0;
        Assert(RadarAgentHealthPolicy.Evaluate(item, now) == RadarAgentHealthLevel.Degraded,
            "Un Agent sin chats configurados no debe mostrarse como Online.");
    }

    private static void ValidateReplayAndRestart(DateTime now)
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        RadarAgentHeartbeatRequest incoming = CreateValid(now);
        incoming.InstanceId = first;
        incoming.Sequence = 11;
        Assert(RadarAgentHeartbeatOrderPolicy.Evaluate(first, 10, now.AddHours(-1), incoming)
               == RadarAgentHealthWriteResult.Accepted, "Secuencia nueva rechazada.");
        incoming.Sequence = 10;
        Assert(RadarAgentHeartbeatOrderPolicy.Evaluate(first, 10, now.AddHours(-1), incoming)
               == RadarAgentHealthWriteResult.OutOfOrder, "Replay no rechazado.");
        incoming.InstanceId = second;
        incoming.Sequence = 1;
        incoming.InstanceStartedUtc = now;
        Assert(RadarAgentHeartbeatOrderPolicy.Evaluate(first, 10, now.AddHours(-1), incoming)
               == RadarAgentHealthWriteResult.Accepted, "Reinicio nuevo rechazado.");
        incoming.InstanceId = first;
        incoming.InstanceStartedUtc = now.AddHours(-1);
        Assert(RadarAgentHeartbeatOrderPolicy.Evaluate(second, 2, now, incoming)
               == RadarAgentHealthWriteResult.PreviousInstance, "Instancia previa tardía no rechazada.");
    }

    private static async Task ValidateApiAuthenticationAsync(DateTime now)
    {
        FakePairingRepository pairing = new();
        FakeHealthRepository health = new();
        RadarAgentHealthApiController controller = new(pairing, health)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        IActionResult result = await controller.Post(CreateValid(now), CancellationToken.None);
        Assert(result is UnauthorizedObjectResult, "API aceptó request sin bearer.");

        controller.HttpContext.Request.Headers.Authorization = "Bearer test-token";
        pairing.Authentication = new RadarAgentAuthenticationResult
        {
            IdAgent = Guid.NewGuid(), IdCuenta = 10, IdAsesor = 20, RolCodigo = "PROPIETARIO"
        };
        result = await controller.Post(CreateValid(now), CancellationToken.None);
        Assert(result is NoContentResult, "API no devolvió 204.");
        Assert(health.LastAgentId == pairing.Authentication.IdAgent, "IdAgent provino del payload o no de autenticación.");

        health.WriteResult = RadarAgentHealthWriteResult.OutOfOrder;
        result = await controller.Post(CreateValid(now), CancellationToken.None);
        Assert(result is ConflictObjectResult, "API no rechazó replay.");

        Assert(typeof(RadarAgentHealthApiController).GetMethod("Post")!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Length == 1,
            "Endpoint sin rate limiting.");
    }

    private static async Task ValidatePanelTenantScopeAsync(DateTime now)
    {
        FakeHealthRepository health = new();
        health.Records.Add(CreateRecord(now));
        RadarAgentHealthController controller = new(health)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateOwnerContext("owner@example.invalid", 77) }
        };
        IActionResult result = await controller.Index(CancellationToken.None);
        Assert(result is ViewResult, "Panel no devolvió vista.");
        Assert(health.LastCorreo == "owner@example.invalid" && health.LastCuenta == 77,
            "Panel no aplicó scope obligatorio de cuenta.");
        Assert(controller.Response.Headers.CacheControl.ToString().Contains("no-store"), "Panel permite cache.");

        AuthorizeAttribute? authorize = typeof(RadarAgentHealthController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().SingleOrDefault();
        Assert(authorize?.Roles == "PROPIETARIO", "Panel no exige PROPIETARIO.");
    }

    private static void ValidateInformationScopeAndSql()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "sql", "RSMaps2", "57_radar_agent_health.sql"));
        Assert(sql.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) == false,
            "El script de esquema no debe implementar el upsert de runtime.");
        Assert(sql.Contains("FOREIGN KEY (IdAgent)", StringComparison.OrdinalIgnoreCase), "SQL sin FK Agent.");
        Assert(sql.Contains("CHECK (Sequence > 0)", StringComparison.OrdinalIgnoreCase), "SQL sin check de secuencia.");

        string repository = File.ReadAllText(Path.Combine(root, "Repositorios", "Implementacion", "RadarAgentHealthRepository.cs"));
        Assert(repository.Contains("IsolationLevel.Serializable", StringComparison.Ordinal), "Upsert sin aislamiento serializable.");
        Assert(repository.Contains("UPDLOCK, HOLDLOCK", StringComparison.Ordinal), "Upsert sin locks concurrentes.");
        Assert(repository.Contains("cu.RolCodigo = 'PROPIETARIO'", StringComparison.Ordinal), "Consulta sin rol propietario.");
        Assert(repository.Contains("d.IdCuenta = @idCuenta", StringComparison.Ordinal), "Consulta sin filtro tenant.");
    }

    private static RadarAgentHeartbeatRequest CreateValid(DateTime now) => new()
    {
        InstanceId = Guid.NewGuid(), Sequence = 1, InstanceStartedUtc = now.AddHours(-1), AgentUtc = now,
        Version = "test-commit", ListenerState = "Running", WhatsAppState = "Ready",
        WhatsAppStateSinceUtc = now.AddMinutes(-30), LastSweepStartedUtc = now.AddMinutes(-2),
        LastSweepCompletedUtc = now.AddMinutes(-1), ChatsConfigured = 7, ChatsReviewed = 7,
        CentralMode = "Central", CentralState = "Healthy", LastCentralSuccessUtc = now.AddMinutes(-1),
        FallbackEnabled = false
    };

    private static RadarAgentHealthRecord CreateRecord(DateTime now) => new()
    {
        IdAgent = Guid.NewGuid(), InstanceId = Guid.NewGuid(), ReceivedUtc = now.AddSeconds(-60),
        ListenerState = "Running", WhatsAppState = "Ready", LastSweepCompletedUtc = now.AddMinutes(-20),
        ChatsConfigured = 7, ChatsReviewed = 7, CentralState = "Healthy", IntervaloRevisionMs = 1_200_000
    };

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }

    private static DefaultHttpContext CreateOwnerContext(string correo, int idCuenta)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Name, correo),
            new Claim(ClaimTypes.Role, "PROPIETARIO"),
            new Claim("IdCuenta", idCuenta.ToString())
        ], "test"));
        return context;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "maps4.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("No se encontró el repositorio.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeHealthRepository : IRadarAgentHealthRepository
    {
        public RadarAgentHealthWriteResult WriteResult { get; set; } = RadarAgentHealthWriteResult.Accepted;
        public Guid LastAgentId { get; private set; }
        public string? LastCorreo { get; private set; }
        public int LastCuenta { get; private set; }
        public List<RadarAgentHealthRecord> Records { get; } = [];
        public Task<RadarAgentHealthWriteResult> UpsertAsync(Guid idAgent, RadarAgentHeartbeatRequest request, CancellationToken cancellationToken = default)
        { LastAgentId = idAgent; return Task.FromResult(WriteResult); }
        public Task<List<RadarAgentHealthRecord>> ListForOwnerAsync(string correo, int idCuenta, CancellationToken cancellationToken = default)
        { LastCorreo = correo; LastCuenta = idCuenta; return Task.FromResult(Records); }
    }

    private sealed class FakePairingRepository : IRadarAgentPairingRepository
    {
        public RadarAgentAuthenticationResult? Authentication { get; set; }
        public Task<RadarAgentAuthenticationResult?> ValidarCredencialAsync(string credencial, CancellationToken cancellationToken = default) => Task.FromResult(Authentication);
        public Task<RadarAgentPairingCreateResult> CrearCodigoAsync(string correo, int idCuenta, string nombreAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RadarAgentPairingExchangeResult?> ConsumirCodigoAsync(string codigo, string nombreAgent, string? equipoNombre, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<RadarAgentDeviceListItem>> ListarAgentsAsync(string correo, int idCuenta, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevocarAgentAsync(string correo, int idCuenta, Guid idAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RadarAgentConfiguration?> ObtenerConfiguracionAsync(string correo, int idCuenta, Guid idAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RadarAgentConfiguration> ObtenerConfiguracionAgentAsync(Guid idAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> GuardarConfiguracionAsync(string correo, int idCuenta, RadarAgentConfiguration configuracion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
