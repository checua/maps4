namespace maps4.Models;

public sealed class RadarPendingProcessingItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public int IntentosProcesamiento { get; init; }
    public DateTime DetectadoUtc { get; init; }
    public DateTime? ReintentarDespuesUtc { get; init; }
    public DateTime? LeaseHastaUtc { get; init; }
}

public sealed class RadarPendingWorkflowItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string? MotorInteligencia { get; init; }
    public string ResultadoCentralJson { get; init; } = string.Empty;
    public DateTime DetectadoUtc { get; init; }
    public DateTime? MatchingCompletadoUtc { get; init; }
}

public sealed class RadarTerminalAckRequest
{
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string DisposicionTerminal { get; init; } = string.Empty;
}