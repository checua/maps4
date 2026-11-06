using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

// Validate the wire envelope before defaults can turn a damaged cache into an empty success.
// This deliberately does not re-score requests or enforce newer business rules on legacy results.
public static class RadarCentralResultReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static bool TryRead(string? json, [NotNullWhen(true)] out RadarInterpretationResult? result,
        string? expectedMessageId = null, string? expectedChat = null)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var properties = document.RootElement.EnumerateObject().ToArray();
            var motors = properties.Where(p => p.Name.Equals("motor", StringComparison.OrdinalIgnoreCase)).ToArray();
            var requests = properties.Where(p => p.Name.Equals("solicitudes", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (motors.Length != 1 || motors[0].Value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(motors[0].Value.GetString()) || requests.Length != 1 ||
                requests[0].Value.ValueKind != JsonValueKind.Array) return false;

            var parsed = JsonSerializer.Deserialize<RadarInterpretationResult>(json, Options);
            if (parsed?.Solicitudes is null || parsed.Solicitudes.Any(s =>
                s is null || string.IsNullOrWhiteSpace(s.MessageId) || string.IsNullOrWhiteSpace(s.ChatOrigen) ||
                (expectedMessageId is not null && !string.Equals(s.MessageId.Trim(), expectedMessageId.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (expectedChat is not null && !string.Equals(s.ChatOrigen.Trim(), expectedChat.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                s.TiposPropiedad is null || s.SubtiposPropiedad is null || s.Zonas is null ||
                s.ModalidadesPago is null || s.RestriccionesDurasNoVerificables is null)) return false;
            result = parsed;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
