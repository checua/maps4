using RSMaps.Radar.Listener.Config;
using System.Security.Cryptography;
using System.Text;

namespace RSMaps.Radar.Listener.Services;

public static class RadarAgentCredentialStore
{
    public static string ObtenerRuta(RadarAgentConfig? config = null)
    {
        string? configurada = Environment.GetEnvironmentVariable("RADAR_AGENT_CREDENTIAL_PATH")?.Trim();
        if (!string.IsNullOrWhiteSpace(configurada))
            return configurada;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string nombre = NormalizarNombreArchivo(config?.Usuario);
        return Path.Combine(localAppData, "RSMaps", "RadarAgent", $"{nombre}.credential");
    }

    public static bool TryLeerToken(RadarAgentConfig? config, out string token, out string detalle)
    {
        token = string.Empty;
        string path = ObtenerRuta(config);

        if (!OperatingSystem.IsWindows())
        {
            detalle = "La credencial protegida del Agent requiere Windows.";
            return false;
        }

        if (!File.Exists(path))
        {
            detalle = $"No existe la credencial protegida: {path}";
            return false;
        }

        try
        {
            string base64 = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(base64))
            {
                detalle = "El archivo de credencial está vacío.";
                return false;
            }

            byte[] protegidos = Convert.FromBase64String(base64);
            byte[] claros = ProtectedData.Unprotect(
                protegidos,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            token = Encoding.UTF8.GetString(claros).Trim();
            CryptographicOperations.ZeroMemory(claros);

            if (string.IsNullOrWhiteSpace(token))
            {
                detalle = "La credencial protegida no contiene un token válido.";
                return false;
            }

            detalle = path;
            return true;
        }
        catch (Exception ex) when (
            ex is CryptographicException or FormatException or IOException or UnauthorizedAccessException)
        {
            detalle = $"No fue posible leer la credencial protegida: {ex.Message}";
            token = string.Empty;
            return false;
        }
    }

    private static string NormalizarNombreArchivo(string? usuario)
    {
        string valor = string.IsNullOrWhiteSpace(usuario) ? "radar-agent" : usuario.Trim().ToLowerInvariant();
        var sb = new StringBuilder();
        bool guion = false;

        foreach (char c in valor)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                guion = false;
            }
            else if (!guion && sb.Length > 0)
            {
                sb.Append('-');
                guion = true;
            }
        }

        return sb.ToString().Trim('-') is { Length: > 0 } nombre
            ? nombre
            : "radar-agent";
    }
}
