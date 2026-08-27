using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public class RuleBasedRadarInterpreter : IRadarInterpreter
{
    public Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var solicitud = ExtractorInmobiliario.Extraer(
            mensaje.TextoOriginal,
            mensaje.ChatOrigen,
            mensaje.MessageId);

        solicitud.Autor = mensaje.Autor;
        solicitud.Telefono = mensaje.Telefono;
        solicitud.DetectadoEn = mensaje.DetectadoEn;

        var resultado = new RadarInterpretationResult
        {
            Motor = "RULE_BASED",
            Solicitudes = [solicitud]
        };

        return Task.FromResult(resultado);
    }
}
