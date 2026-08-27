using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarKnowledgeAwareInterpreter : IRadarInterpreter
{
    private readonly IRadarInterpreter _inner;
    private readonly IRadarKnowledgeProvider _knowledgeProvider;

    public RadarKnowledgeAwareInterpreter(
        IRadarInterpreter inner,
        IRadarKnowledgeProvider knowledgeProvider)
    {
        _inner = inner;
        _knowledgeProvider = knowledgeProvider;
    }

    public async Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var contexto = await _knowledgeProvider.ConstruirContextoAsync(mensaje, cancellationToken);
        if (string.IsNullOrWhiteSpace(contexto))
            return await _inner.InterpretarAsync(mensaje, cancellationToken);

        var mensajeConContexto = new RadarMessage
        {
            MessageId = mensaje.MessageId,
            ChatOrigen = mensaje.ChatOrigen,
            Autor = mensaje.Autor,
            Telefono = mensaje.Telefono,
            DetectadoEn = mensaje.DetectadoEn,
            TextoOriginal = $"""
MENSAJE DE WHATSAPP A INTERPRETAR:
<<<
{mensaje.TextoOriginal}
>>>

{contexto}

IMPORTANTE: interpreta únicamente la demanda contenida entre <<< >>>. El conocimiento RADAR es contexto de dominio aprobado y NO forma parte del mensaje del usuario.
"""
        };

        var resultado = await _inner.InterpretarAsync(mensajeConContexto, cancellationToken);

        foreach (var solicitud in resultado.Solicitudes)
        {
            solicitud.MessageId = mensaje.MessageId;
            solicitud.ChatOrigen = mensaje.ChatOrigen;
            solicitud.Autor = mensaje.Autor;
            solicitud.Telefono = mensaje.Telefono;
            solicitud.DetectadoEn = mensaje.DetectadoEn;
            solicitud.MensajeOriginal = mensaje.TextoOriginal;
        }

        resultado.Observaciones = string.IsNullOrWhiteSpace(resultado.Observaciones)
            ? "RADAR Knowledge aplicado."
            : $"{resultado.Observaciones} | RADAR Knowledge aplicado.";

        return resultado;
    }
}
