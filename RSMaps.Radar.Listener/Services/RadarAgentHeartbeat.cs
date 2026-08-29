using RSMaps.Radar.Listener.Config;

namespace RSMaps.Radar.Listener.Services;

public static class RadarAgentHeartbeat
{
    private static int _iniciado;

    public static void Start(RadarAgentConfig config)
    {
        if (Interlocked.Exchange(ref _iniciado, 1) == 1)
            return;

        _ = Task.Run(() => EjecutarAsync(config));
    }

    private static async Task EjecutarAsync(RadarAgentConfig config)
    {
        bool? ultimoEstado = true;

        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60));

                RadarAgentRemoteContext? remoto = await RadarAgentBackendClient.ValidarAsync(config);
                bool autenticado = remoto is not null;

                if (ultimoEstado != autenticado)
                {
                    Console.WriteLine(autenticado
                        ? "  ♥ RADAR Agent volvió a confirmar conexión con RSMaps."
                        : "  ⚠ RADAR Agent no pudo confirmar su conexión con RSMaps.");
                }

                ultimoEstado = autenticado;
            }
            catch (Exception ex)
            {
                if (ultimoEstado != false)
                    Console.WriteLine($"  ⚠ Heartbeat RADAR no disponible: {ex.Message}");

                ultimoEstado = false;
            }
        }
    }
}
