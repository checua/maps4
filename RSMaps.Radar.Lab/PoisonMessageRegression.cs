using maps4.Controllers;
using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

internal static class PoisonMessageRegression
{
    public static async Task RunAsync()
    {
        foreach (var status in Enum.GetValues<RadarMessageProcessingClaimStatus>())
        {
            var repository = new StubRepository { Status = status };
            var processing = new StubProcessing();
            var controller = Create(repository, processing);
            var result = (ObjectResult)await controller.ProcesarAgent(Message(), default);
            int expected = status switch
            {
                RadarMessageProcessingClaimStatus.Completed or RadarMessageProcessingClaimStatus.Acquired => 200,
                RadarMessageProcessingClaimStatus.PermanentFailure => 422,
                _ => 503
            };
            Check(result.StatusCode == expected, "HTTP claim status mapping");
            Check(processing.Calls == (status == RadarMessageProcessingClaimStatus.Acquired ? 1 : 0), "No work for replay/busy/permanent");
            Check(repository.Completions == (status == RadarMessageProcessingClaimStatus.Acquired ? 1 : 0), "Only acquired result can complete");
        }
        foreach (Exception error in new Exception[] { new HttpRequestException(), new JsonException(), new TimeoutException(), new InvalidOperationException(), new RadarPermanentMessageException() })
        {
            var repository = new StubRepository();
            var controller = Create(repository, new StubProcessing { Error = error });
            var result = (ObjectResult)await controller.ProcesarAgent(Message(), default);
            bool permanent = error is RadarPermanentMessageException;
            Check(result.StatusCode == (permanent ? 422 : 503), "Failure classification");
            Check(repository.ExplicitPermanent == permanent && repository.Completions == 0, "Failure never completes processing");
        }
        var exhausted = new StubRepository { Failure = RadarMessageProcessingFailureStatus.PermanentFailure };
        var finalController = Create(exhausted, new StubProcessing { Error = new HttpRequestException() });
        Check(((ObjectResult)await finalController.ProcesarAgent(Message(), default)).StatusCode == 422, "Last transient failure becomes terminal");
        Check(!finalController.Response.Headers.ContainsKey("Retry-After"), "No retry header for terminal failure");
        foreach (var failureStatus in Enum.GetValues<RadarMessageProcessingFailureStatus>())
        {
            var missingConfig = new StubRepository { Failure = failureStatus };
            var p = new StubProcessing();
            var controller = Create(missingConfig, p, configured: false);
            var response = (ObjectResult)await controller.ProcesarAgent(Message(), default);
            Check(response.StatusCode == (failureStatus == RadarMessageProcessingFailureStatus.PermanentFailure ? 422 : 503), "Configuration outage remains bounded by durable outcome");
            Check(p.Calls == 0 && missingConfig.Completions == 0 && missingConfig.Failures == 1 && !missingConfig.ExplicitPermanent,
                "Configuration absence is transient and cannot complete processing");
        }
        var storageOutage = new StubRepository { RegistrationError = new IOException("Synthetic storage outage") };
        Check(((ObjectResult)await Create(storageOutage, new StubProcessing(), false).ProcesarAgent(Message(), default)).StatusCode == 503,
            "Failed persistence never fabricates permanent acknowledgement");
        Check(storageOutage.Completions == 0, "Persistence outage cannot complete");
        var authenticationOutage = new StubRepository();
        var unavailableAuth = Create(authenticationOutage, new StubProcessing(), authenticationError: new IOException("Synthetic authentication storage failure"));
        Check(((ObjectResult)await unavailableAuth.ProcesarAgent(Message(), default)).StatusCode == 503 && authenticationOutage.Claims == 0 && authenticationOutage.Failures == 0,
            "Authentication storage failure is sanitized and cannot consume a message attempt");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            var repository = new StubRepository();
            var controller = Create(repository, new StubProcessing { WaitForBudget = true });
            Check(((ObjectResult)await controller.ProcesarAgent(Message(), cancelled.Token)).StatusCode == 503,
                "Disconnected HTTP request keeps retry on remaining budget");
            Check(repository.CleanupCancellable && repository.Failures == 1, "Cleanup is bounded independently from HTTP cancellation");
        }
        foreach (string? corrupt in new string?[] { null, "{invalid", "null", "{}", "[]", "{\"motor\":\"LAB\"}",
            "{\"motor\":\"LAB\",\"solicitudes\":null}", "{\"motor\":\"LAB\",\"solicitudes\":[null]}",
            "{\"motor\":\"LAB\",\"solicitudes\":[{}]}" })
        {
            var cached = new StubRepository { Status = RadarMessageProcessingClaimStatus.Completed, Json = corrupt };
            var p = new StubProcessing();
            Check(((ObjectResult)await Create(cached, p).ProcesarAgent(Message(), default)).StatusCode == 409 && p.Calls == 0, "Corrupt replay never reclaims");
            using var malformedResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corrupt ?? "") };
            Check(await RadarCentralIntelligenceClient.LeerRespuestaAsync(malformedResponse) is null, "Corrupt HTTP success never enters downstream");
            Check(!RadarCentralResultReader.TryRead(corrupt, out _), "Workflow recovery rejects corrupt result without ACK");
        }
        Check(RadarCentralResultReader.TryRead("{\"Motor\":\"LEGACY\",\"Solicitudes\":[]}", out _), "Legacy PascalCase envelope accepted");
        Check(RadarCentralResultReader.TryRead("{\"motor\":\"LAB\",\"solicitudes\":[]}", out _), "Explicit empty result remains valid");
        var identity = Message();
        string validLegacy = JsonSerializer.Serialize(new RadarInterpretationResult
        {
            Motor = "LEGACY", Solicitudes = [new() { MessageId = identity.MessageId, ChatOrigen = identity.ChatOrigen }]
        });
        Check(RadarCentralResultReader.TryRead(validLegacy, out _, identity.MessageId, identity.ChatOrigen), "Legacy request without actionability remains compatible");
        Check(!RadarCentralResultReader.TryRead(validLegacy, out _, "different-message", identity.ChatOrigen), "Wrong cached message identity cannot produce empty-request ACK");
        Check(!RadarCentralResultReader.TryRead(validLegacy, out _, identity.MessageId, "different-chat"), "Wrong cached chat is investigated");
        using (var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity))
        {
            response.Headers.Add("X-Radar-Processing", "permanent-failure");
            response.Content = JsonContent.Create(new RadarCentralFailure { Code = RadarCentralFailure.PermanentCode, Permanent = true, Retry = false });
            bool caught = false;
            try { await RadarCentralIntelligenceClient.LeerRespuestaAsync(response); }
            catch (RadarCentralPermanentFailureException) { caught = true; }
            Check(caught, "Listener recognizes typed terminal response");
        }
        foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway, HttpStatusCode.UnprocessableEntity })
        {
            using var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
            Check(await RadarCentralIntelligenceClient.LeerRespuestaAsync(response) is null, "Ordinary errors remain retryable");
        }
        foreach (string malformed in new[] { "{bad", "{}", "{\"code\":\"RADAR_PROCESSING_PERMANENT_FAILURE\",\"permanent\":true}",
            "{\"code\":\"UNKNOWN\",\"permanent\":true,\"retry\":false}" })
        {
            using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { Content = new StringContent(malformed) };
            response.Headers.Add("X-Radar-Processing", "permanent-failure");
            Check(await RadarCentralIntelligenceClient.LeerRespuestaAsync(response) is null, "Malformed terminal contract cannot suppress a message");
        }
        string? oldMode = Environment.GetEnvironmentVariable("RADAR_INTELLIGENCE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("RADAR_INTELLIGENCE_MODE", "central");
            Check(!RadarCentralIntelligenceClient.FallbackLocalHabilitado, "No local fallback for durable central processing");
        }
        finally { Environment.SetEnvironmentVariable("RADAR_INTELLIGENCE_MODE", oldMode); }
        Console.WriteLine("POISON_MESSAGE_REGRESSION_OK");
    }

    // Separate slow mode: exercises the real 75-second production budget with in-memory stubs.
    public static async Task RunTimeoutAsync()
    {
        var repository = new StubRepository { Failure = RadarMessageProcessingFailureStatus.PermanentFailure };
        var processing = new StubProcessing { IgnoreCancellation = true };
        var controller = Create(repository, processing);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var response = (ObjectResult)await controller.ProcesarAgent(Message(), default).WaitAsync(TimeSpan.FromSeconds(90));
        Check(response.StatusCode == 422 && !repository.ExplicitPermanent && repository.Failures == 1 && repository.Completions == 0,
            "Real timeout is transient, but exhausted durable budget returns terminal failure");
        Check(controller.Response.Headers["X-Radar-Processing"] == "permanent-failure" && !controller.Response.Headers.ContainsKey("Retry-After"),
            "Final timeout does not request retries or successful ACK");
        Check(repository.LastError == "Presupuesto temporal central agotado." && watch.Elapsed >= TimeSpan.FromSeconds(70), "Real timeout catch branch executed");
        Check(!processing.LateResult.Task.IsCompleted, "Controller returned without waiting for non-cooperative dependency");
        processing.LateResult.SetResult(new RadarInterpretationResult { Motor = "LATE" });
        await Task.Delay(20);
        Check(repository.Completions == 0 && repository.Failures == 1, "Late result cannot complete or overwrite terminal processing");
        Console.WriteLine($"POISON_CONTROLLER_TIMEOUT_REGRESSION_OK elapsed_seconds={watch.Elapsed.TotalSeconds:F1}");
    }

    internal static RadarMessage Message() => new() { ChatOrigen = "ISOLATED-LAB", MessageId = Guid.NewGuid().ToString("N"), TextoOriginal = "Solicitud sintética" };
    internal static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Poison regression: " + name); }
    private static RadarIntelligenceController Create(StubRepository repository, StubProcessing processing, bool configured = true, Exception? authenticationError = null)
    {
        var pairing = DispatchProxy.Create<IRadarAgentPairingRepository, PairingProxy>();
        ((PairingProxy)(object)pairing).Error = authenticationError;
        var controller = new RadarIntelligenceController(pairing, repository, new StubIntelligence { Configurada = configured }, processing)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Authorization = "Bearer isolated-test-placeholder";
        return controller;
    }
    public class PairingProxy : DispatchProxy
    {
        public Exception? Error;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "ValidarCredencialAsync"
            ? Error is null ? Task.FromResult<RadarAgentAuthenticationResult?>(new() { IdAgent = Guid.NewGuid(), Correo = "lab@example.invalid" }) :
                Task.FromException<RadarAgentAuthenticationResult?>(Error)
            : throw new NotSupportedException();
    }
    private sealed class StubIntelligence : IRadarCentralIntelligenceService
    {
        public bool Configurada { get; init; } = true;
        public Task<RadarInterpretationResult> InterpretarAsync(RadarMessage message, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class StubProcessing : IRadarCentralProcessingService
    {
        public int Calls;
        public Exception? Error;
        public bool WaitForBudget;
        public bool IgnoreCancellation;
        public TaskCompletionSource<RadarInterpretationResult> LateResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RadarInterpretationResult> ProcesarAsync(string correo, RadarMessage message, CancellationToken token = default)
        {
            Calls++;
            if (IgnoreCancellation) return LateResult.Task;
            if (WaitForBudget) return WaitAsync(token);
            return Error is null ? Task.FromResult(new RadarInterpretationResult { Motor = "LAB" }) : Task.FromException<RadarInterpretationResult>(Error);
        }
        private static async Task<RadarInterpretationResult> WaitAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unexpected completion of infinite delay.");
        }
    }
    private sealed class StubRepository : IRadarMessageProcessingRepository
    {
        public RadarMessageProcessingClaimStatus Status = RadarMessageProcessingClaimStatus.Acquired;
        public RadarMessageProcessingFailureStatus Failure = RadarMessageProcessingFailureStatus.Retryable;
        public string? Json = "{\"motor\":\"LAB\",\"solicitudes\":[]}";
        public int Completions;
        public bool ExplicitPermanent;
        public int Failures;
        public int Claims;
        public bool CleanupCancellable;
        public string? LastError;
        public Exception? RegistrationError;
        public Task<RadarMessageProcessingClaimResult> ReclamarAsync(Guid id, RadarMessage message, TimeSpan duration, CancellationToken token = default)
        {
            Claims++;
            return Task.FromResult(new RadarMessageProcessingClaimResult
            { Status = Status, IdRadarMessageProcessing = 1, LeaseToken = Guid.NewGuid(), IntentosProcesamiento = 5, ResultadoCentralJson = Json });
        }
        public Task CompletarAsync(long id, Guid lease, string? motor, string json, CancellationToken token = default) { Completions++; return Task.CompletedTask; }
        public Task<bool> MarcarTerminadoAsync(Guid id, string chat, string message, string disposition, CancellationToken token = default) => throw new InvalidOperationException("Failure must never ACK");
        public Task<RadarMessageProcessingFailureStatus> RegistrarFalloAsync(long id, Guid lease, string error, TimeSpan delay, bool permanente = false, CancellationToken cancellationToken = default)
        {
            ExplicitPermanent = permanente;
            Failures++;
            CleanupCancellable = cancellationToken.CanBeCanceled && !cancellationToken.IsCancellationRequested;
            LastError = error;
            return RegistrationError is null ? Task.FromResult(permanente ? RadarMessageProcessingFailureStatus.PermanentFailure : Failure) :
                Task.FromException<RadarMessageProcessingFailureStatus>(RegistrationError);
        }
    }
}
