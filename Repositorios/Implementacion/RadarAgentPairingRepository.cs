using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarAgentPairingRepository : IRadarAgentPairingRepository
{
    private readonly string _cadenaSQL;

    public RadarAgentPairingRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<RadarAgentPairingCreateResult> CrearCodigoAsync(
        string correo,
        string nombreAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw new ArgumentException("Se requiere el correo autenticado.", nameof(correo));

        string nombre = NormalizarNombreAgent(nombreAgent);
        string codigo = CrearCodigoHumano();
        string codigoHash = Hash(codigo);
        DateTime expiraUtc = DateTime.UtcNow.AddMinutes(10);

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sqlContexto = @"
SELECT TOP (1)
    u.idAsesor,
    cu.IdCuenta,
    c.Nombre AS CuentaNombre
FROM dbo.RSMAPS_Usuario u
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = u.idAsesor
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = cu.IdCuenta
   AND c.Activo = 1
WHERE u.correo = @correo
ORDER BY cu.EsPredeterminada DESC, cu.IdCuenta;";

        int idAsesor;
        int idCuenta;
        string cuentaNombre;

        await using (SqlCommand cmd = new(sqlContexto, conexion, tx))
        {
            cmd.Parameters.Add("@correo", SqlDbType.VarChar, 200).Value = correo.Trim();
            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await dr.ReadAsync(cancellationToken))
                throw new InvalidOperationException("El usuario no tiene una cuenta activa para vincular RADAR Agent.");

            idAsesor = Convert.ToInt32(dr["idAsesor"]);
            idCuenta = Convert.ToInt32(dr["IdCuenta"]);
            cuentaNombre = dr["CuentaNombre"].ToString() ?? string.Empty;
        }

        const string sqlInvalidar = @"
UPDATE dbo.RSMAPS_RadarAgentPairing
SET ConsumidoUtc = SYSUTCDATETIME()
WHERE IdAsesor = @idAsesor
  AND IdCuenta = @idCuenta
  AND ConsumidoUtc IS NULL;";

        await using (SqlCommand cmd = new(sqlInvalidar, conexion, tx))
        {
            cmd.Parameters.Add("@idAsesor", SqlDbType.Int).Value = idAsesor;
            cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sqlInsert = @"
INSERT dbo.RSMAPS_RadarAgentPairing
(
    CodigoHash,
    IdAsesor,
    IdCuenta,
    NombreAgent,
    CreadoUtc,
    ExpiraUtc
)
VALUES
(
    @codigoHash,
    @idAsesor,
    @idCuenta,
    @nombreAgent,
    SYSUTCDATETIME(),
    @expiraUtc
);";

        await using (SqlCommand cmd = new(sqlInsert, conexion, tx))
        {
            cmd.Parameters.Add("@codigoHash", SqlDbType.Char, 64).Value = codigoHash;
            cmd.Parameters.Add("@idAsesor", SqlDbType.Int).Value = idAsesor;
            cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
            cmd.Parameters.Add("@nombreAgent", SqlDbType.NVarChar, 120).Value = nombre;
            cmd.Parameters.Add("@expiraUtc", SqlDbType.DateTime2).Value = expiraUtc;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new RadarAgentPairingCreateResult
        {
            Codigo = codigo,
            ExpiraUtc = expiraUtc,
            IdAsesor = idAsesor,
            IdCuenta = idCuenta,
            CuentaNombre = cuentaNombre
        };
    }

    public async Task<RadarAgentPairingExchangeResult?> ConsumirCodigoAsync(
        string codigo,
        string nombreAgent,
        string? equipoNombre,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;

        string codigoHash = Hash(NormalizarCodigo(codigo));
        string nombre = NormalizarNombreAgent(nombreAgent);
        string? equipo = string.IsNullOrWhiteSpace(equipoNombre)
            ? null
            : equipoNombre.Trim()[..Math.Min(equipoNombre.Trim().Length, 200)];

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        const string sqlPairing = @"
SELECT TOP (1)
    p.IdPairing,
    p.IdAsesor,
    p.IdCuenta,
    c.Nombre AS CuentaNombre,
    cu.RolCodigo
FROM dbo.RSMAPS_RadarAgentPairing p WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.RSMAPS_CuentaUsuario cu
    ON cu.IdAsesor = p.IdAsesor
   AND cu.IdCuenta = p.IdCuenta
   AND cu.Activo = 1
INNER JOIN dbo.RSMAPS_Cuenta c
    ON c.IdCuenta = p.IdCuenta
   AND c.Activo = 1
WHERE p.CodigoHash = @codigoHash
  AND p.ConsumidoUtc IS NULL
  AND p.ExpiraUtc > SYSUTCDATETIME();";

        long idPairing;
        int idAsesor;
        int idCuenta;
        string cuentaNombre;
        string rolCodigo;

        await using (SqlCommand cmd = new(sqlPairing, conexion, tx))
        {
            cmd.Parameters.Add("@codigoHash", SqlDbType.Char, 64).Value = codigoHash;
            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await dr.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }

            idPairing = Convert.ToInt64(dr["IdPairing"]);
            idAsesor = Convert.ToInt32(dr["IdAsesor"]);
            idCuenta = Convert.ToInt32(dr["IdCuenta"]);
            cuentaNombre = dr["CuentaNombre"].ToString() ?? string.Empty;
            rolCodigo = dr["RolCodigo"].ToString() ?? string.Empty;
        }

        Guid idAgent = Guid.NewGuid();
        string token = CrearToken();
        string tokenHash = Hash(token);

        const string sqlInsertAgent = @"
INSERT dbo.RSMAPS_RadarAgentDevice
(
    IdAgent,
    IdAsesor,
    IdCuenta,
    NombreAgent,
    EquipoNombre,
    TokenHash,
    Activo,
    FechaAltaUtc
)
VALUES
(
    @idAgent,
    @idAsesor,
    @idCuenta,
    @nombreAgent,
    @equipoNombre,
    @tokenHash,
    1,
    SYSUTCDATETIME()
);";

        await using (SqlCommand cmd = new(sqlInsertAgent, conexion, tx))
        {
            cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            cmd.Parameters.Add("@idAsesor", SqlDbType.Int).Value = idAsesor;
            cmd.Parameters.Add("@idCuenta", SqlDbType.Int).Value = idCuenta;
            cmd.Parameters.Add("@nombreAgent", SqlDbType.NVarChar, 120).Value = nombre;
            cmd.Parameters.Add("@equipoNombre", SqlDbType.NVarChar, 200).Value = (object?)equipo ?? DBNull.Value;
            cmd.Parameters.Add("@tokenHash", SqlDbType.Char, 64).Value = tokenHash;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sqlConsumir = @"
UPDATE dbo.RSMAPS_RadarAgentPairing
SET ConsumidoUtc = SYSUTCDATETIME(),
    IdAgentCreado = @idAgent
WHERE IdPairing = @idPairing
  AND ConsumidoUtc IS NULL;";

        await using (SqlCommand cmd = new(sqlConsumir, conexion, tx))
        {
            cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            cmd.Parameters.Add("@idPairing", SqlDbType.BigInt).Value = idPairing;
            int afectados = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (afectados != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }
        }

        await tx.CommitAsync(cancellationToken);

        return new RadarAgentPairingExchangeResult
        {
            IdAgent = idAgent,
            Token = token,
            IdAsesor = idAsesor,
            IdCuenta = idCuenta,
            CuentaNombre = cuentaNombre,
            RolCodigo = rolCodigo
        };
    }

    private static string NormalizarNombreAgent(string nombreAgent)
    {
        string nombre = string.IsNullOrWhiteSpace(nombreAgent) ? "RADAR Agent" : nombreAgent.Trim();
        return nombre[..Math.Min(nombre.Length, 120)];
    }

    private static string CrearCodigoHumano()
    {
        string hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        return $"RDR-{hex[..5]}-{hex[5..10]}-{hex[10..15]}-{hex[15..20]}";
    }

    private static string CrearToken()
    {
        string base64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant();

    private static string Hash(string valor)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(valor));
        return Convert.ToHexString(bytes);
    }
}
