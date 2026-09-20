namespace RSMaps.Radar.Listener.Models;

public sealed class RadarWhatsAppMessageCapture
{
    public bool Confirmado { get; init; }
    public string? MotivoNoConfirmado { get; init; }
    public string? MessageId { get; init; }
    public string ChatOrigen { get; init; } = "";
    public string? AutorActual { get; init; }
    public string? TelefonoActual { get; init; }
    public string TextoPropio { get; init; } = "";
    public bool TieneCita { get; init; }
    public string? TextoCitado { get; init; }
    public bool EsReenviado { get; init; }
    public DateTime? TimestampMensaje { get; init; }
}
