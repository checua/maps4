using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

namespace maps4.Services;

public interface IRadarCentralIntelligenceService
{
    bool Configurada { get; }

    Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default);
}

public sealed class RadarCentralIntelligenceService : IRadarCentralIntelligenceService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly Lazy<IRadarInterpreter> _interpreter;

    public RadarCentralIntelligenceService(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
        _interpreter = new Lazy<IRadarInterpreter>(CrearInterpreter);
    }

    public bool Configurada => !string.IsNullOrWhiteSpace(ObtenerApiKey());

    public Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        if (!Configurada)
        {
            throw new InvalidOperationException(
                "RADAR Intelligence central no tiene OPENAI_API_KEY configurada en RSMaps.");
        }

        return _interpreter.Value.InterpretarAsync(mensaje, cancellationToken);
    }

    private IRadarInterpreter CrearInterpreter()
    {
        string apiKey = ObtenerApiKey()
            ?? throw new InvalidOperationException(
                "RADAR Intelligence central requiere OPENAI_API_KEY en el servidor.");

        string model = ObtenerValor("RADAR_OPENAI_MODEL") ?? "gpt-5.6-luna";
        string fallbackModel = ObtenerValor("RADAR_OPENAI_FALLBACK_MODEL") ?? "gpt-5.6-terra";

        IRadarKnowledgeProvider? knowledge = CrearKnowledgeProvider();

        IRadarInterpreter primary = new OpenAiRadarInterpreter(apiKey, model);
        if (knowledge is not null)
            primary = new RadarKnowledgeAwareInterpreter(primary, knowledge);

        IRadarInterpreter? fallback = string.Equals(
            fallbackModel,
            "none",
            StringComparison.OrdinalIgnoreCase)
            ? null
            : new OpenAiRadarInterpreter(apiKey, fallbackModel);

        if (fallback is not null && knowledge is not null)
            fallback = new RadarKnowledgeAwareInterpreter(fallback, knowledge);

        return new RadarIntelligenceInterpreter(primary, fallback);
    }

    private IRadarKnowledgeProvider? CrearKnowledgeProvider()
    {
        string? configured = ObtenerValor("RADAR_KNOWLEDGE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return new JsonRadarKnowledgeProvider(configured);

        string[] candidates =
        [
            Path.Combine(
                _environment.ContentRootPath,
                "RSMaps.Radar.Lab",
                "Knowledge",
                "radar-knowledge.json"),
            Path.Combine(
                AppContext.BaseDirectory,
                "RadarKnowledge",
                "radar-knowledge.json")
        ];

        string? path = candidates.FirstOrDefault(File.Exists);
        return path is null ? null : new JsonRadarKnowledgeProvider(path);
    }

    private string? ObtenerApiKey() => ObtenerValor("OPENAI_API_KEY");

    private string? ObtenerValor(string key)
    {
        string? value = _configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
