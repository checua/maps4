using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

internal static class RadarAgentHealthReporterRegression
{
    public static async Task RunAsync()
    {
        string? previousMode = Environment.GetEnvironmentVariable("RADAR_INTELLIGENCE_MODE");
        string? previousFallback = Environment.GetEnvironmentVariable("RADAR_CENTRAL_FALLBACK_LOCAL");
        try
        {
            Environment.SetEnvironmentVariable("RADAR_INTELLIGENCE_MODE", "central");
            Environment.SetEnvironmentVariable("RADAR_CENTRAL_FALLBACK_LOCAL", "0");

            await ValidateRuntimeAndPayloadAsync();
            await ValidateTransportFailuresAsync();
            await ValidateCancellationAsync();

            Console.WriteLine("RADAR_AGENT_RUNTIME_HEALTH_REGRESSION_OK");
            Console.WriteLine("RADAR_AGENT_HEALTH_REPORTER_REGRESSION_OK");
        }
        finally
        {
            Environment.SetEnvironmentVariable("RADAR_INTELLIGENCE_MODE", previousMode);
            Environment.SetEnvironmentVariable("RADAR_CENTRAL_FALLBACK_LOCAL", previousFallback);
        }
    }

    private static async Task ValidateRuntimeAndPayloadAsync()
    {
        var runtime = new RadarAgentRuntimeHealth(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            "test-build+abcdef0");
        runtime.SetListenerState("Running");
        runtime.SetWhatsAppState("Ready");
        runtime.StartSweep(7, new DateTime(2026, 10, 1, 12, 1, 0, DateTimeKind.Utc));
        runtime.CompleteSweep(7, new DateTime(2026, 10, 1, 12, 1, 5, DateTimeKind.Utc));
        RadarAgentHealthPayload completed = runtime.Snapshot(1);

        runtime.StartSweep(7, new DateTime(2026, 10, 1, 12, 2, 0, DateTimeKind.Utc));
        RadarAgentHealthPayload interrupted = runtime.Snapshot(2);
        Require(interrupted.LastSweepStartedUtc > completed.LastSweepStartedUtc,
            "El nuevo barrido no registró inicio.");
        Require(interrupted.LastSweepCompletedUtc == completed.LastSweepCompletedUtc,
            "Un barrido interrumpido no debe aparecer como completado.");

        Parallel.For(0, 2_000, i =>
        {
            runtime.SetWhatsAppState(i % 2 == 0 ? "Ready" : "WaitingForReady");
            runtime.SetCentralState(i % 3 == 0 ? "Healthy" : "Degraded",
                i % 3 == 0 ? null : "CENTRAL_TEST");
            _ = runtime.Snapshot(i + 3);
        });

        runtime.SetWhatsAppState("Ready");
        runtime.SetCentralState("Healthy", successfulUtc: DateTime.UtcNow);
        runtime.CompleteSweep(7);

        var handler = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var reporter = new RadarAgentHealthReporter(
            new RadarAgentConfig(),
            runtime,
            http,
            () => (true, "unit-credential"));

        Require(await reporter.SendOnceAsync() == RadarAgentHealthSendResult.Accepted,
            "El primer heartbeat válido no fue aceptado.");
        Require(await reporter.SendOnceAsync() == RadarAgentHealthSendResult.Accepted,
            "El segundo heartbeat válido no fue aceptado.");
        Require(handler.Payloads.Count == 2, "No se capturaron dos payloads.");

        RadarAgentHealthPayload first = Deserialize(handler.Payloads[0]);
        RadarAgentHealthPayload second = Deserialize(handler.Payloads[1]);
        Require(first.Sequence == 1 && second.Sequence == 2,
            "La secuencia del reporter no es estrictamente creciente.");
        Require(first.CentralMode == "Central" && !first.FallbackEnabled,
            "Central/fallback no reflejan la configuración efectiva.");
        Require(first.ChatsConfigured == 7 && first.ChatsReviewed == 7,
            "El snapshot no refleja el último barrido completo.");

        string json = handler.Payloads[0];
        foreach (string forbidden in new[]
                 {
                     "telefono", "message", "chatOrigen", "texto", "token", "credential",
                     "hostname", "path", "exception", "cookie", "storage"
                 })
        {
            Require(!json.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"El payload contiene un campo sensible: {forbidden}.");
        }
    }

    private static async Task ValidateTransportFailuresAsync()
    {
        var cases = new (Func<HttpRequestMessage, HttpResponseMessage>? Response,
            Exception? Exception, RadarAgentHealthSendResult Expected)[]
        {
            (_ => new HttpResponseMessage(HttpStatusCode.Forbidden), null,
                RadarAgentHealthSendResult.Unauthorized),
            (_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests), null,
                RadarAgentHealthSendResult.RateLimited),
            (_ => new HttpResponseMessage(HttpStatusCode.InternalServerError), null,
                RadarAgentHealthSendResult.ServerFailure),
            (null, new TaskCanceledException(), RadarAgentHealthSendResult.Timeout),
            (null, new HttpRequestException(), RadarAgentHealthSendResult.Unreachable)
        };

        foreach (var item in cases)
        {
            var runtime = new RadarAgentRuntimeHealth(version: "test");
            var handler = new CaptureHandler(item.Response, item.Exception);
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            var reporter = new RadarAgentHealthReporter(
                new RadarAgentConfig(), runtime, http, () => (true, "unit-credential"));
            RadarAgentHealthSendResult actual = await reporter.SendOnceAsync();
            Require(actual == item.Expected,
                $"Fallo de transporte {item.Expected} produjo {actual}.");
        }

        var repeatedHandler = new CaptureHandler(
            null,
            new HttpRequestException("simulated-unreachable"));
        using (var repeatedHttp = new HttpClient(repeatedHandler))
        {
            var repeatedRuntime = new RadarAgentRuntimeHealth(version: "test");
            repeatedRuntime.StartSweep(3);
            repeatedRuntime.CompleteSweep(3);
            var repeatedReporter = new RadarAgentHealthReporter(
                new RadarAgentConfig(), repeatedRuntime, repeatedHttp,
                () => (true, "unit-credential"));
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Require(await repeatedReporter.SendOnceAsync() == RadarAgentHealthSendResult.Unreachable,
                    "El endpoint inaccesible no se aisló en intentos repetidos.");
            }
            Require(repeatedHandler.Payloads.Count == 3,
                "Los intentos aislados no conservaron una secuencia de payloads independiente.");
            long[] sequences = repeatedHandler.Payloads.Select(x => Deserialize(x).Sequence).ToArray();
            Require(sequences.SequenceEqual(new long[] { 1, 2, 3 }),
                "La secuencia dejó de ser monotónica durante fallos repetidos.");
            Require(repeatedRuntime.Snapshot(99).LastSweepCompletedUtc.HasValue,
                "El fallo de telemetría alteró el estado funcional del barrido.");
        }

        var noCredential = new RadarAgentHealthReporter(
            new RadarAgentConfig(),
            new RadarAgentRuntimeHealth(version: "test"),
            new HttpClient(new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent))),
            () => (false, string.Empty));
        Require(await noCredential.SendOnceAsync() == RadarAgentHealthSendResult.CredentialUnavailable,
            "La ausencia de credencial no se aisló correctamente.");
    }

    private static async Task ValidateCancellationAsync()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CaptureHandler(_ =>
        {
            sent.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler);
        var reporter = new RadarAgentHealthReporter(
            new RadarAgentConfig(),
            new RadarAgentRuntimeHealth(version: "test"),
            http,
            () => (true, "unit-credential"));
        using var cts = new CancellationTokenSource();
        Task loop = reporter.RunAsync(cts.Token);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await loop.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static RadarAgentHealthPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<RadarAgentHealthPayload>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Payload vacío.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _response;
        private readonly Exception? _exception;
        public List<string> Payloads { get; } = [];

        public CaptureHandler(
            Func<HttpRequestMessage, HttpResponseMessage>? response,
            Exception? exception = null)
        {
            _response = response;
            _exception = exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                Payloads.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            if (_exception is not null)
                throw _exception;
            return _response!(request);
        }
    }
}
