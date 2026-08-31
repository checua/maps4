namespace maps4.Models;

public sealed class RadarPendingDeliveryItem
{
    public long IdRadarMessageDelivery { get; init; }
    public string ChatOrigen { get; init; } = "";
    public string MessageId { get; init; } = "";
    public int SolicitudIndice { get; init; }
    public string ClaveEntrega { get; init; } = "";
    public int? IdInmueble { get; init; }
    public int? Puntuacion { get; init; }
    public string Estado { get; init; } = "";
    public int IntentosEntrega { get; init; }
    public string? PayloadAlerta { get; init; }
}