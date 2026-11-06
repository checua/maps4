namespace RSMaps.Radar.Listener.Services;

public sealed class RadarCentralPermanentFailureException : Exception
{
    public RadarCentralPermanentFailureException() : base("El backend confirmó un fallo permanente; no se crea Delivery ni ACK de éxito.") { }
}
