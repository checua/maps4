namespace RSMaps.Radar.Listener.Config;

public static class AlertSettings
{
    // En RADAR_SAFE_LAB nunca apuntamos al chat real de alertas.
    // El Listener intentará resolver un destino deliberadamente inexistente,
    // por lo que no podrá enviar ningún mensaje de WhatsApp durante la prueba.
    public static string ChatDestino => RadarSettings.ModoSeguroLab
        ? "__RADAR_SAFE_LAB_NO_SEND__"
        : "Propiedades";

    public const int EsperaEnvioMs = 500;
}
