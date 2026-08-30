namespace maps4.Models;

public sealed class RadarDeliveryPrepareRequest
{
    public string ChatOrigen { get; set; } = "";
    public string MessageId { get; set; } = "";
    public int SolicitudIndice { get; set; }
    public string ClaveEntrega { get; set; } = "";
    public int? IdInmueble { get; set; }
    public int? Puntuacion { get; set; }
    public string? PayloadAlerta { get; set; }
}

public sealed class RadarDeliveryPrepareResult
{
    public long IdRadarMessageDelivery { get; init; }
    public string Estado { get; init; } = "";
    public bool YaEnviado { get; init; }
    public int IntentosEntrega { get; init; }
}

public sealed class RadarDeliveryCompleteRequest
{
    public long IdRadarMessageDelivery { get; set; }
    public bool Enviada { get; set; }
    public string? Error { get; set; }
}

public sealed class RadarDeliveryCompleteResult
{
    public long IdRadarMessageDelivery { get; init; }
    public string Estado { get; init; } = "";
    public bool YaEnviado { get; init; }
    public int IntentosEntrega { get; init; }
}
