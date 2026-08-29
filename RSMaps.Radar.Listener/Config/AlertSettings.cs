namespace RSMaps.Radar.Listener.Config;

public static class AlertSettings
{
    // SAFE LAB jamás usa un destino real. Fuera de ese modo, la configuración
    // central de RSMaps tiene prioridad; el JSON local queda como fallback.
    public static string ChatDestino
    {
        get
        {
            if (RadarSettings.ModoSeguroLab)
                return "__RADAR_SAFE_LAB_NO_SEND__";

            var remota = RadarSettings.ConfiguracionRemota;
            if (remota?.Configurada == true && !string.IsNullOrWhiteSpace(remota.DestinoAlertas))
                return remota.DestinoAlertas.Trim();

            var configurado = RadarSettings.ConfiguracionAgente?.DestinoAlertas;
            return string.IsNullOrWhiteSpace(configurado)
                ? "Propiedades"
                : configurado.Trim();
        }
    }

    public const int EsperaEnvioMs = 500;
}
