namespace RSMaps.Radar.Listener.Services;

public static class RadarInterpreterFactory
{
    public static IRadarInterpreter Create()
    {
        if (RadarCentralIntelligenceClient.Habilitada)
            return CreateCentral();

        string engine = (Environment.GetEnvironmentVariable("RADAR_INTERPRETER") ?? "radar")
            .Trim()
            .ToLowerInvariant();

        return engine switch
        {
            "radar" or "openai" => CreateRadarIntelligence(),
            "openai_raw" => CreateOpenAiRaw(),
            "rules" or "rule_based" => new RuleBasedRadarInterpreter(),
            _ => throw new InvalidOperationException(
                $"RADAR_INTERPRETER='{engine}' no es válido. Usa 'radar', 'openai', 'openai_raw' o 'rules'.")
        };
    }

    private static IRadarInterpreter CreateCentral()
    {
        IRadarInterpreter? fallbackLocal = null;

        if (RadarCentralIntelligenceClient.FallbackLocalHabilitado)
        {
            try
            {
                fallbackLocal = CreateRadarIntelligence();
                Console.WriteLine("RADAR Intelligence central: fallback local temporal disponible.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"⚠ RADAR Intelligence central: no pude preparar fallback local ({ex.Message}).");
            }
        }

        Console.WriteLine(
            $"RADAR Intelligence: CENTRAL vía RSMaps ({RadarAgentBackendClient.BaseUrl}).");

        return new CentralRadarInterpreter(fallbackLocal);
    }

    private static IRadarInterpreter CreateRadarIntelligence()
    {
        string apiKey = GetApiKey();
        string model = Environment.GetEnvironmentVariable("RADAR_OPENAI_MODEL") ?? "gpt-5.6-luna";
        string fallbackModel = Environment.GetEnvironmentVariable("RADAR_OPENAI_FALLBACK_MODEL") ?? "gpt-5.6-terra";
        IRadarKnowledgeProvider? knowledgeProvider = CreateKnowledgeProvider();

        IRadarInterpreter primary = new OpenAiRadarInterpreter(apiKey, model);
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

        Console.WriteLine($"RADAR Intelligence: OpenAI {model}" +
            (fallback is null ? " (sin fallback)." : $" → fallback {fallbackModel}."));

        return new RadarIntelligenceInterpreter(primary, fallback);
    }

    private static IRadarInterpreter CreateOpenAiRaw()
    {
        string apiKey = GetApiKey();
        string model = Environment.GetEnvironmentVariable("RADAR_OPENAI_MODEL") ?? "gpt-5.6-luna";
        Console.WriteLine($"RADAR Intelligence RAW: OpenAI {model}.");
        return new OpenAiRadarInterpreter(apiKey, model);
    }

    private static IRadarKnowledgeProvider? CreateKnowledgeProvider()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("RADAR_KNOWLEDGE_PATH");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath))
            {
                Console.WriteLine($"RADAR Knowledge: {configuredPath}");
                return new JsonRadarKnowledgeProvider(configuredPath);
            }

            Console.WriteLine($"⚠ RADAR Knowledge: RADAR_KNOWLEDGE_PATH no existe: '{configuredPath}'.");
            return null;
        }

        string[] candidates =
        [
            Path.Combine(
                Environment.CurrentDirectory,
                "RSMaps.Radar.Lab",
                "Knowledge",
                "radar-knowledge.json"),
            Path.Combine(
                AppContext.BaseDirectory,
                "Knowledge",
                "radar-knowledge.json")
        ];

        string? path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            Console.WriteLine("⚠ RADAR Knowledge: no se encontró radar-knowledge.json; el pipeline continúa sin conocimiento adicional.");
            return null;
        }

        Console.WriteLine($"RADAR Knowledge: {path}");
        return new JsonRadarKnowledgeProvider(path);
    }

    private static string GetApiKey()
    {
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "RADAR Intelligence requiere OPENAI_API_KEY. La clave debe configurarse como variable de entorno y nunca guardarse en Git. " +
                "Para una ejecución explícita sin OpenAI usa RADAR_INTERPRETER=rules.");
        }

        return apiKey.Trim();
    }
}
