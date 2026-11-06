namespace maps4.Models;

public enum RadarMessageProcessingClaimStatus
{
    Acquired = 0,
    Completed = 1,
    Busy = 2,
    PermanentFailure = 3
}

public sealed class RadarMessageProcessingClaimResult
{
    public RadarMessageProcessingClaimStatus Status { get; init; }
    public long IdRadarMessageProcessing { get; init; }
    public Guid? LeaseToken { get; init; }
    public string? ResultadoCentralJson { get; init; }
    public int IntentosProcesamiento { get; init; }
    public DateTime? DisponibleDespuesUtc { get; init; }
}

public enum RadarMessageProcessingFailureStatus
{
    LeaseLost,
    Retryable,
    PermanentFailure
}
