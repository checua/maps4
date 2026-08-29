using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarAgentChatDiscoveryRepository : IRadarAgentChatDiscoveryRepository
{
    private readonly string _cadenaSQL;

    public RadarAgentChatDiscoveryRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<bool> ReemplazarChatsAsync(
        Guid idAgent,
        IReadOnlyCollection<string> chats,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return false;

        var normalizados = chats
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Where(x => !EsNumeroSinGuardar(x))
            .Select(x => x[..Math.Min(x.Length, 300)])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2_000)
            .ToList();

        if (normalizados.Count == 0)
            return false;

        string chatsJson = JsonSerializer.Serialize(normalizados);

        const string sql = @"
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RSMAPS_RadarAgentDevice
    WHERE IdAgent = @idAgent
      AND Activo = 1
      AND RevocadoUtc IS NULL
)
BEGIN
    SELECT CAST(0 AS bit);
    RETURN;
END;

DECLARE @Chats TABLE (NombreChat nvarchar(300) NOT NULL PRIMARY KEY);

INSERT @Chats (NombreChat)
SELECT DISTINCT LEFT(LTRIM(RTRIM(CONVERT(nvarchar(300), j.[value]))), 300)
FROM OPENJSON(@chatsJson) j
WHERE NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(300), j.[value]))), N'') IS NOT NULL;

MERGE dbo.RSMAPS_RadarAgentChatDiscovery AS destino
USING @Chats AS origen
ON destino.IdAgent = @idAgent
AND destino.NombreChat = origen.NombreChat
WHEN MATCHED THEN
    UPDATE SET UltimoVistoUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (IdAgent, NombreChat, UltimoVistoUtc)
    VALUES (@idAgent, origen.NombreChat, SYSUTCDATETIME());

SELECT CAST(1 AS bit);";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        cmd.Parameters.Add("@chatsJson", SqlDbType.NVarChar, -1).Value = chatsJson;

        object? valor = await cmd.ExecuteScalarAsync(cancellationToken);
        return valor != null && valor != DBNull.Value && Convert.ToBoolean(valor);
    }

    public async Task<List<RadarAgentDiscoveredChatItem>> ListarChatsAsync(
        string correo,
        int idCuenta,
        Guid idAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || idCuenta <= 0 || idAgent == Guid.Empty)
            return [];

        const string sql = @"
SELECT dc.NombreChat, dc.UltimoVistoUtc
FROM dbo.RSMAPS_RadarAgentChatDiscovery dc
INNER JOIN dbo.RSMAPS_RadarAgentDevice d ON d.IdAgent = dc.IdAgent
INNER JOIN dbo.RSMAPS_Usuario u ON u.idAsesor = d.IdAsesor
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = d.IdAsesor
   AND cu.IdCuenta = d.IdCuenta
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = d.IdCuenta
   AND c.Activo = 1
WHERE dc.IdAgent = @idAgent
  AND d.IdCuenta = @idCuenta
  AND u.correo = @correo
ORDER BY dc.NombreChat;";

        var resultado = new List<RadarAgentDiscoveredChatItem>();

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
        cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
        cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo.Trim();

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            string nombre = dr["NombreChat"].ToString() ?? string.Empty;
            if (EsNumeroSinGuardar(nombre))
                continue;

            resultado.Add(new RadarAgentDiscoveredChatItem
            {
                Nombre = nombre,
                UltimoVistoUtc = Convert.ToDateTime(dr["UltimoVistoUtc"])
            });
        }

        return resultado;
    }

    private static bool EsNumeroSinGuardar(string nombre)
    {
        int digitos = 0;

        foreach (char c in nombre)
        {
            if (char.IsDigit(c))
            {
                digitos++;
                continue;
            }

            if (char.IsWhiteSpace(c) || c is '+' or '-' or '(' or ')' or '.' or '\u00A0')
                continue;

            return false;
        }

        return digitos >= 7;
    }
}
