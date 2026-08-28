namespace RSMaps.Radar.Listener.Config;

public static class AlertSettings
{
    // SAFE LAB jamás usa un destino real. Fuera de ese modo, cada Agent puede
    // definir su destino sin recompilar; si no existe configuración conserva
    // el comportamiento histórico de enviar a "Propiedades".
    public static string ChatDestino
    {
        get
        {
            if (RadarSettings.ModoSeguroLab)
                return "__RADAR_SAFE_LAB_NO_SEND__";

            var configurado = RadarSettings.ConfiguracionAgente?.DestinoAlertas;
            return string.IsNullOrWhiteSpace(configurado)
                ? "Propiedades"
                : configurado.Trim();
        }
    }

    public const int EsperaEnvioMs = 500;
}
