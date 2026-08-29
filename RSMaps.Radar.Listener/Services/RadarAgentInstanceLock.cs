using System.Security.Cryptography;
using System.Text;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarAgentInstanceLock : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private RadarAgentInstanceLock(Mutex mutex)
    {
        _mutex = mutex;
        _ownsMutex = true;
    }

    public static RadarAgentInstanceLock Acquire()
    {
        string identity = Environment.GetEnvironmentVariable("RADAR_AGENT_CONFIG_PATH")?.Trim()
            ?? AppContext.BaseDirectory;

        try
        {
            identity = Path.GetFullPath(identity);
        }
        catch
        {
            // La identidad original sigue siendo suficiente para generar un nombre estable.
        }

        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant())))[..24];

        string mutexName = $"Local\\RSMaps.RadarAgent.{hash}";
        var mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            throw new InvalidOperationException(
                "Ya hay una instancia de este RADAR Agent en ejecución. " +
                "Cierra la ventana anterior antes de iniciar otra.");
        }

        return new RadarAgentInstanceLock(mutex);
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Si el runtime ya liberó el mutex, sólo debemos cerrar el handle.
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}
