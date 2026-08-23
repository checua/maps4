namespace RSMaps.Radar.Listener.Config;

public static class AlertSettings
{
    // Chat de control donde Radar envía las solicitudes detectadas.
    // No debe formar parte de ChatsMonitoreados para evitar bucles.
    public const string ChatDestino = "Propiedades";

    public const int EsperaEnvioMs = 500;
}
