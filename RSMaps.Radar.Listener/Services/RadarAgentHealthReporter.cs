using RSMaps.Radar.Listener.Config;
using RSMaps.Radar.Listener.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public enum RadarAgentHealthSendResult
{
    Accepted,
    CredentialUnavailable,
    Unauthorized,
    RateLimited,
    ServerFailure,
    Timeout,
    Unreachable,
    UnexpectedFailure
}

public sealed class RadarAgentHealthReporter
{
    private static readonly TimeSpan NormalInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromSeconds(120);
    private static int _started;
    private readonly RadarAgentConfig _config;
    private readonly RadarAgentRuntimeHealth _runtime;
    private readonly HttpClient _http;
    private readonly Func<(bool Success, string Token)> _tokenProvider;
    private long _sequence;

    public RadarAgentHealthReporter(
        RadarAgentConfig config,
        RadarAgentRuntimeHealth runtime,
        HttpClient? httpClient = null,
        Func<(bool Success, string Token)>? tokenProvider = null)
    {
        _config = config;
        _runtime = runtime;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _tokenProvider = tokenProvider ?? (() =>
        {
            bool success = RadarAgentCredentialStore.TryLeerToken(_config, out string token, out _);
            return (success, token);
        });
    }

    public static void Start(RadarAgentConfig config)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        try
        {
            var reporter = new RadarAgentHealthReporter(config, RadarAgentRuntimeHealth.Current);
            _ = Task.Run(() => reporter.RunAsync(CancellationToken.None));
        }
        catch (Exception)
        {
            RadarAgentRuntimeHealth.Current.SetReporterError("HEALTH_START_FAILURE");
            Console.WriteLine("[HEALTH_REPORTER] START_FAILURE");
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        TimeSpan delay = TimeSpan.Zero;
        RadarAgentHealthSendResult? previous = null;
        var consecutiveFailures = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);

                RadarAgentHealthSendResult result = await SendOnceAsync(cancellationToken);
                if (previous != result)
                    Console.WriteLine($"[HEALTH_REPORTER] {ToLogCode(result)}");
                previous = result;

                if (result == RadarAgentHealthSendResult.Accepted)
                {
                    consecutiveFailures = 0;
                    delay = NormalInterval;
                }
                else
                {
                    consecutiveFailures++;
                    delay = consecutiveFailures == 1 ? NormalInterval : MaximumBackoff;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cierre cooperativo: nunca propagar cancelación desde el reporter al Listener.
        }
    }

    public async Task<RadarAgentHealthSendResult> SendOnceAsync(
        CancellationToken cancellationToken = default)
    {
        string token = string.Empty;
        try
        {
            (bool credentialAvailable, string acquiredToken) = _tokenProvider();
            token = acquiredToken ?? string.Empty;
            if (!credentialAvailable || string.IsNullOrWhiteSpace(token))
                return Record(RadarAgentHealthSendResult.CredentialUnavailable);

            string baseUrl = RadarAgentBackendClient.BaseUrl;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttps && !baseUri.IsLoopback))
            {
                return Record(RadarAgentHealthSendResult.UnexpectedFailure);
            }

            long sequence = Interlocked.Increment(ref _sequence);
            RadarAgentHealthPayload payload = _runtime.Snapshot(sequence);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/api/radar/agent/health");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(payload);

            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            RadarAgentHealthSendResult result = response.StatusCode switch
            {
                HttpStatusCode.NoContent => RadarAgentHealthSendResult.Accepted,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RadarAgentHealthSendResult.Unauthorized,
                HttpStatusCode.TooManyRequests => RadarAgentHealthSendResult.RateLimited,
                >= HttpStatusCode.InternalServerError => RadarAgentHealthSendResult.ServerFailure,
                _ => RadarAgentHealthSendResult.UnexpectedFailure
            };
            return Record(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Record(RadarAgentHealthSendResult.Timeout);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return Record(RadarAgentHealthSendResult.Unreachable);
        }
        catch (Exception)
        {
            return Record(RadarAgentHealthSendResult.UnexpectedFailure);
        }
        finally
        {
            token = string.Empty;
        }
    }

    private RadarAgentHealthSendResult Record(RadarAgentHealthSendResult result)
    {
        _runtime.SetReporterError(result == RadarAgentHealthSendResult.Accepted
            ? null
            : $"HEALTH_{ToLogCode(result)}");
        return result;
    }

    private static string ToLogCode(RadarAgentHealthSendResult result) => result switch
    {
        RadarAgentHealthSendResult.Accepted => "OK",
        RadarAgentHealthSendResult.CredentialUnavailable => "CREDENTIAL_UNAVAILABLE",
        RadarAgentHealthSendResult.Unauthorized => "UNAUTHORIZED",
        RadarAgentHealthSendResult.RateLimited => "RATE_LIMITED",
        RadarAgentHealthSendResult.ServerFailure => "SERVER_FAILURE",
        RadarAgentHealthSendResult.Timeout => "TIMEOUT",
        RadarAgentHealthSendResult.Unreachable => "UNREACHABLE",
        _ => "UNEXPECTED_FAILURE"
    };
}
