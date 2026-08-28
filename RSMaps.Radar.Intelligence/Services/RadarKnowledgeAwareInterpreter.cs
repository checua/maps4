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
        var terminos = await _knowledgeProvider.ObtenerTerminosRelevantesAsync(
            mensaje,
            cancellationToken);
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

        // La aplicación determinística del término se limita a una sola solicitud.
        // En mensajes con varias demandas, aplicar un término globalmente podría
        // contaminar solicitudes que no lo contienen.
        if (resultado.Solicitudes.Count == 1)
            AplicarTerminosAprobados(resultado.Solicitudes[0], terminos);

        resultado.Observaciones = string.IsNullOrWhiteSpace(resultado.Observaciones)
            ? "RADAR Knowledge aplicado."
            : $"{resultado.Observaciones} | RADAR Knowledge aplicado.";

        return resultado;
    }

    private static void AplicarTerminosAprobados(
        SolicitudInmobiliaria solicitud,
        IReadOnlyList<RadarKnowledgeTerm> terminos)
    {
        foreach (var termino in terminos.Where(x =>
                     string.Equals(x.Categoria, "SubtipoPropiedad", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(termino.ValorCanonico))
                continue;

            var subtipo = termino.ValorCanonico.Trim();

            if (!solicitud.SubtiposPropiedad.Contains(subtipo, StringComparer.OrdinalIgnoreCase))
                solicitud.SubtiposPropiedad.Add(subtipo);

            solicitud.TiposPropiedad = solicitud.TiposPropiedad
                .Where(x => !CoincideConTermino(x, termino))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            solicitud.Zonas = solicitud.Zonas
                .Where(x => !CoincideConTermino(x, termino))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(termino.TipoBaseCanonico) &&
                !solicitud.TiposPropiedad.Contains(
                    termino.TipoBaseCanonico,
                    StringComparer.OrdinalIgnoreCase))
            {
                solicitud.TiposPropiedad.Add(termino.TipoBaseCanonico.Trim());
            }
        }
    }

    private static bool CoincideConTermino(string valor, RadarKnowledgeTerm termino)
    {
        var n = RadarInterpretationNormalizer.NormalizarTexto(valor);
        if (string.IsNullOrWhiteSpace(n))
            return false;

        var candidatos = new List<string>
        {
            termino.Termino,
            termino.ValorCanonico ?? string.Empty
        };
        candidatos.AddRange(termino.Alias);

        return candidatos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(RadarInterpretationNormalizer.NormalizarTexto)
            .Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
    }
}
