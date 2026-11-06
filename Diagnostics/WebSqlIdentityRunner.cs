using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace maps4.Diagnostics;

internal sealed record DiagnosticManifest(string RunId, string Site, string Slot, string AssemblySha256,
    string[] Instances, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc);
internal sealed record DiagnosticContext(string Site, string Slot, string Instance, string AssemblySha256,
    string ContentRoot, string WebRoot);

internal static class WebSqlIdentityRunner
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static bool SafeId(string? value) => value is not null && Regex.IsMatch(value, "\\A[a-zA-Z0-9_-]{1,80}\\z");

    internal static async Task<string> RunAsync(string directory, DiagnosticContext context,
        Func<CancellationToken, Task<Dictionary<string, object?>>> read,
        CancellationToken stopping, TimeSpan? testBudget = null)
    {
        string? output = null;
        try
        {
            if (stopping.IsCancellationRequested) return "CANCELLED";
            if (!Directory.Exists(directory)) return "DISABLED";
            DiagnosticPrivateFiles.Validate(directory, context.ContentRoot, context.WebRoot);
            string manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath)) return "DISABLED";
            DiagnosticPrivateFiles.ValidateFile(manifestPath);
            if (new FileInfo(manifestPath).Length > 8192) return "INVALID";
            byte[] bytes = await File.ReadAllBytesAsync(manifestPath, stopping);
            var m = JsonSerializer.Deserialize<DiagnosticManifest>(bytes, Json);
            var now = DateTimeOffset.UtcNow;
            if (m is null || !SafeId(m.RunId) || !SafeId(context.Instance) || m.Instances is null ||
                m.Instances.Length is < 1 or > 20 || m.Instances.Any(x => !SafeId(x)) ||
                !m.Instances.Contains(context.Instance, StringComparer.Ordinal) || m.Site != context.Site || m.Slot != context.Slot ||
                m.AssemblySha256 != context.AssemblySha256 ||
                !Regex.IsMatch(m.AssemblySha256 ?? "", "\\A[A-F0-9]{64}\\z") ||
                m.IssuedUtc > now || m.ExpiresUtc <= now || m.ExpiresUtc <= m.IssuedUtc ||
                m.ExpiresUtc - m.IssuedUtc > TimeSpan.FromMinutes(15)) return "INVALID";
            string stem = m.RunId + "-" + context.Instance;
            string marker = Path.Combine(directory, stem + ".consumed");
            // Persistent at-most-once across process restarts, including failed attempts.
            try { using var claim = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None); claim.Flush(true); }
            catch (IOException) { return "CONSUMED"; }
            DiagnosticPrivateFiles.ValidateFile(marker);
            output = Path.Combine(directory, stem + ".json");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            var budget = testBudget ?? TimeSpan.FromSeconds(25);
            if (budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(25)) throw new InvalidOperationException();
            var remaining = m.ExpiresUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) throw new OperationCanceledException();
            deadline.CancelAfter(remaining < budget ? remaining : budget);
            Dictionary<string, object?>? fields = null;
            string status;
            try
            {
                fields = await read(deadline.Token).WaitAsync(deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                // Disarming or editing the manifest during the query invalidates evidence.
                byte[] current = await File.ReadAllBytesAsync(manifestPath, deadline.Token);
                if (!bytes.SequenceEqual(current))
                    throw new InvalidOperationException();
                status = "OBSERVED";
            }
            catch (OperationCanceledException) { status = "CANCELLED_OR_TIMEOUT"; fields = null; }
            catch { status = "INCONCLUSIVE"; fields = null; }
            // Explicit field allowlist; no exception messages or connection configuration.
            if (fields is not null && fields.Keys.Any(k => !AllowedField(k)))
            { fields = null; status = "INCONCLUSIVE"; }
            var record = new { Status = status, m.RunId, context.Instance, context.Site, context.Slot,
                context.AssemblySha256, Utc = DateTimeOffset.UtcNow, Pid = Environment.ProcessId,
                ProcessStartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(), Fields = fields };
            byte[] result = JsonSerializer.SerializeToUtf8Bytes(record);
            if (result.Length > 32768) throw new InvalidOperationException();
            DiagnosticPrivateFiles.Validate(directory, context.ContentRoot, context.WebRoot);
            string temp = output + ".pending";
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { DiagnosticPrivateFiles.ValidateFile(temp); await file.WriteAsync(result, CancellationToken.None); file.Flush(true); }
            DiagnosticPrivateFiles.ValidateFile(temp);
            File.Move(temp, output, false);
            return status;
        }
        catch { return "INCONCLUSIVE"; } // Deliberately no log sink.
    }

    private static bool AllowedField(string key) => key is "ServerName" or "DatabaseName" or "OriginalLogin" or
        "ExecutionLogin" or "DatabaseUser" or "AuthScheme" or "SessionId" or "DbOwner" or "DatabaseControl" or
        "AlterSchema" or "ProviderType" or "ProviderPosition" ||
        WebSqlIdentityQuery.Permissions.Any(p => key == p.Object + "." + p.Permission);
}
