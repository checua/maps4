using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public class RuleBasedRadarInterpreter : IRadarInterpreter
{
    private readonly IRadarInterpreter? _overrideInterpreter;

    public RuleBasedRadarInterpreter()
    {
        // Puente temporal para la integración E2E del Agent.
        // Sin RADAR_LIVE_AI=1 conserva exactamente el comportamiento histórico.
        if (EsVariableActiva("RADAR_LIVE_AI"))
            _overrideInterpreter = CrearRadarIntelligence();
    }

    public Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        if (_overrideInterpreter is not null)
            return _overrideInterpreter.InterpretarAsync(mensaje, cancellationToken);

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

    private static IRadarInterpreter CrearRadarIntelligence()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "RADAR_LIVE_AI está activo pero OPENAI_API_KEY no está definida en esta sesión.");
        }

        var primaryModel = Environment.GetEnvironmentVariable("RADAR_OPENAI_MODEL")
            ?? "gpt-5.6-luna";
        var fallbackModel = Environment.GetEnvironmentVariable("RADAR_OPENAI_FALLBACK_MODEL")
            ?? "gpt-5.6-terra";

        IRadarKnowledgeProvider? knowledgeProvider = null;
        var knowledgePath = Environment.GetEnvironmentVariable("RADAR_KNOWLEDGE_PATH");

        if (string.IsNullOrWhiteSpace(knowledgePath))
        {
            knowledgePath = Path.Combine(
                Environment.CurrentDirectory,
                "RSMaps.Radar.Lab",
                "Knowledge",
                "radar-knowledge.json");
        }

        if (File.Exists(knowledgePath))
            knowledgeProvider = new JsonRadarKnowledgeProvider(knowledgePath);

        IRadarInterpreter primary = new OpenAiRadarInterpreter(apiKey, primaryModel);
        if (knowledgeProvider is not null)
            primary = new RadarKnowledgeAwareInterpreter(primary, knowledgeProvider);

        IRadarInterpreter? fallback = string.Equals(
            fallbackModel,
            "none",
            StringComparison.OrdinalIgnoreCase)
            ? null
            : new OpenAiRadarInterpreter(apiKey, fallbackModel);

        if (fallback is not null && knowledgeProvider is not null)
            fallback = new RadarKnowledgeAwareInterpreter(fallback, knowledgeProvider);

        return new RadarIntelligenceInterpreter(primary, fallback);
    }

    private static bool EsVariableActiva(string nombre)
    {
        var value = Environment.GetEnvironmentVariable(nombre);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
