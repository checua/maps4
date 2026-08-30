using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public sealed class CentralRadarInterpreter : IRadarInterpreter
{
    private readonly IRadarInterpreter? _fallbackLocal;

    public CentralRadarInterpreter(IRadarInterpreter? fallbackLocal = null)
    {
        _fallbackLocal = fallbackLocal;
    }

    public async Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        RadarInterpretationResult? central =
            await RadarCentralIntelligenceClient.ProcesarAsync(mensaje, cancellationToken);

        if (central is not null)
        {
            central.Motor = string.IsNullOrWhiteSpace(central.Motor)
                ? "CENTRAL:RSMAPS"
                : $"CENTRAL:{central.Motor}";
            return central;
        }

        if (_fallbackLocal is not null)
        {
            Console.WriteLine("  ↳ Intelligence central no disponible; usando fallback local temporal.");
            RadarInterpretationResult local =
                await _fallbackLocal.InterpretarAsync(mensaje, cancellationToken);
            local.Motor = $"FALLBACK-LOCAL:{local.Motor}";
            return local;
        }

        throw new InvalidOperationException(
            "RADAR Intelligence central no respondió y no existe fallback local configurado.");
    }
}
