using System.Security.Cryptography;
using System.Text;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarAgentInstanceLock : IDisposable
{
    private readonly FileStream _stream;

    private RadarAgentInstanceLock(FileStream stream)
    {
        _stream = stream;
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

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSMaps",
            "RadarAgent",
            "locks");

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{hash}.lock");

        try
        {
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            stream.SetLength(0);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine($"PID={Environment.ProcessId}");
            writer.WriteLine($"STARTED_UTC={DateTime.UtcNow:O}");
            writer.Flush();
            stream.Position = 0;

            return new RadarAgentInstanceLock(stream);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "Ya hay una instancia de este RADAR Agent en ejecución. " +
                "Cierra la ventana anterior antes de iniciar otra.",
                ex);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
