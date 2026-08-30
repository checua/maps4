using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using RSMaps.Radar.Listener.Models;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarMessageProcessingRepository : IRadarMessageProcessingRepository
{
    private readonly string _cadenaSQL;

    public RadarMessageProcessingRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<RadarMessageProcessingClaimResult> ReclamarAsync(
        Guid idAgent,
        RadarMessage mensaje,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            throw new ArgumentException("Se requiere un IdAgent válido.", nameof(idAgent));
        if (mensaje is null)
            throw new ArgumentNullException(nameof(mensaje));
        if (string.IsNullOrWhiteSpace(mensaje.ChatOrigen))
            throw new ArgumentException("Se requiere ChatOrigen para persistencia idempotente.", nameof(mensaje));
        if (string.IsNullOrWhiteSpace(mensaje.MessageId))
            throw new ArgumentException("Se requiere MessageId para persistencia idempotente.", nameof(mensaje));

        string chatOrigen = mensaje.ChatOrigen.Trim();
        string messageId = mensaje.MessageId.Trim();
        if (chatOrigen.Length > 300)
            throw new ArgumentException("ChatOrigen excede 300 caracteres.", nameof(mensaje));
        if (messageId.Length > 200)
            throw new ArgumentException("MessageId excede 200 caracteres.", nameof(mensaje));

        string? autor = NormalizarNullable(mensaje.Autor, 250);
        string? telefono = NormalizarNullable(mensaje.Telefono, 80);
        int leaseSeconds = (int)Math.Clamp(Math.Ceiling(leaseDuration.TotalSeconds), 30d, 600d);
        Guid leaseToken = Guid.NewGuid();

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        const string sqlSelect = @"
SELECT TOP (1)
    IdRadarMessageProcessing,
    Estado,
    IntentosProcesamiento,
    ResultadoCentralJson,
    LeaseHastaUtc,
    ReintentarDespuesUtc,
    SYSUTCDATETIME() AS AhoraUtc
FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
WHERE IdAgent = @idAgent
  AND ChatOrigen = @chatOrigen
  AND MessageId = @messageId;";

        long? idProcessing = null;
        string estado = string.Empty;
        int intentos = 0;
        string? resultadoCentralJson = null;
        DateTime? leaseHastaUtc = null;
        DateTime? reintentarDespuesUtc = null;
        DateTime ahoraUtc = DateTime.UtcNow;

        await using (SqlCommand cmd = new(sqlSelect, conexion, tx))
        {
            cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            cmd.Parameters.Add("@chatOrigen", SqlDbType.NVarChar, 300).Value = chatOrigen;
            cmd.Parameters.Add("@messageId", SqlDbType.NVarChar, 200).Value = messageId;

            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await dr.ReadAsync(cancellationToken))
            {
                idProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]);
                estado = dr["Estado"].ToString() ?? string.Empty;
                intentos = Convert.ToInt32(dr["IntentosProcesamiento"]);
                resultadoCentralJson = dr["ResultadoCentralJson"] == DBNull.Value
                    ? null
                    : dr["ResultadoCentralJson"].ToString();
                leaseHastaUtc = dr["LeaseHastaUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["LeaseHastaUtc"]);
                reintentarDespuesUtc = dr["ReintentarDespuesUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["ReintentarDespuesUtc"]);
                ahoraUtc = Convert.ToDateTime(dr["AhoraUtc"]);
            }
        }

        if (!idProcessing.HasValue)
        {
            const string sqlInsert = @"
INSERT dbo.RSMAPS_RadarMessageProcessing
(
    IdAgent,
    ChatOrigen,
    MessageId,
    Autor,
    Telefono,
    MensajeOriginal,
    Estado,
    IntentosProcesamiento,
    UltimoIntentoUtc,
    DetectadoUtc,
    CreadoUtc,
    ActualizadoUtc,
    LeaseToken,
    LeaseHastaUtc
)
OUTPUT INSERTED.IdRadarMessageProcessing
VALUES
(
    @idAgent,
    @chatOrigen,
    @messageId,
    @autor,
    @telefono,
    @mensajeOriginal,
    N'PROCESANDO',
    1,
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    @leaseToken,
    DATEADD(SECOND, @leaseSeconds, SYSUTCDATETIME())
);";

            long nuevoId;
            await using (SqlCommand cmd = new(sqlInsert, conexion, tx))
            {
                cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
                cmd.Parameters.Add("@chatOrigen", SqlDbType.NVarChar, 300).Value = chatOrigen;
                cmd.Parameters.Add("@messageId", SqlDbType.NVarChar, 200).Value = messageId;
                cmd.Parameters.Add("@autor", SqlDbType.NVarChar, 250).Value = (object?)autor ?? DBNull.Value;
                cmd.Parameters.Add("@telefono", SqlDbType.NVarChar, 80).Value = (object?)telefono ?? DBNull.Value;
                cmd.Parameters.Add("@mensajeOriginal", SqlDbType.NVarChar, -1).Value = mensaje.TextoOriginal ?? string.Empty;
                cmd.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
                cmd.Parameters.Add("@leaseSeconds", SqlDbType.Int).Value = leaseSeconds;

                object? scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                nuevoId = Convert.ToInt64(scalar);
            }

            await InsertarEventoAsync(
                conexion,
                tx,
                nuevoId,
                "MENSAJE_DETECTADO",
                null,
                "PROCESANDO",
                "Mensaje registrado y reclamado para procesamiento central.",
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new RadarMessageProcessingClaimResult
            {
                Status = RadarMessageProcessingClaimStatus.Acquired,
                IdRadarMessageProcessing = nuevoId,
                LeaseToken = leaseToken,
                IntentosProcesamiento = 1
            };
        }

        if (string.Equals(estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(resultadoCentralJson))
        {
            await InsertarEventoAsync(
                conexion,
                tx,
                idProcessing.Value,
                "RESULTADO_REUTILIZADO",
                "COMPLETADO",
                "COMPLETADO",
                "Solicitud duplicada atendida desde el resultado central persistido.",
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new RadarMessageProcessingClaimResult
            {
                Status = RadarMessageProcessingClaimStatus.Completed,
                IdRadarMessageProcessing = idProcessing.Value,
                ResultadoCentralJson = resultadoCentralJson,
                IntentosProcesamiento = intentos
            };
        }

        DateTime? disponibleDespuesUtc = null;
        if (leaseHastaUtc.HasValue && leaseHastaUtc.Value > ahoraUtc)
            disponibleDespuesUtc = leaseHastaUtc;
        if (reintentarDespuesUtc.HasValue &&
            reintentarDespuesUtc.Value > ahoraUtc &&
            (!disponibleDespuesUtc.HasValue || reintentarDespuesUtc.Value > disponibleDespuesUtc.Value))
        {
            disponibleDespuesUtc = reintentarDespuesUtc;
        }

        if (disponibleDespuesUtc.HasValue)
        {
            await tx.CommitAsync(cancellationToken);
            return new RadarMessageProcessingClaimResult
            {
                Status = RadarMessageProcessingClaimStatus.Busy,
                IdRadarMessageProcessing = idProcessing.Value,
                IntentosProcesamiento = intentos,
                DisponibleDespuesUtc = disponibleDespuesUtc
            };
        }

        const string sqlReclamar = @"
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET Estado = N'PROCESANDO',
    IntentosProcesamiento = IntentosProcesamiento + 1,
    UltimoIntentoUtc = SYSUTCDATETIME(),
    ReintentarDespuesUtc = NULL,
    UltimoError = NULL,
    MotorInteligencia = NULL,
    ResultadoCentralJson = NULL,
    InterpretadoUtc = NULL,
    MatchingCompletadoUtc = NULL,
    TerminadoUtc = NULL,
    ActualizadoUtc = SYSUTCDATETIME(),
    LeaseToken = @leaseToken,
    LeaseHastaUtc = DATEADD(SECOND, @leaseSeconds, SYSUTCDATETIME())
WHERE IdRadarMessageProcessing = @idProcessing;";

        await using (SqlCommand cmd = new(sqlReclamar, conexion, tx))
        {
            cmd.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
            cmd.Parameters.Add("@leaseSeconds", SqlDbType.Int).Value = leaseSeconds;
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idProcessing.Value;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertarEventoAsync(
            conexion,
            tx,
            idProcessing.Value,
            "PROCESAMIENTO_RECLAMADO",
            estado,
            "PROCESANDO",
            "Mensaje pendiente reclamado para un nuevo intento central.",
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return new RadarMessageProcessingClaimResult
        {
            Status = RadarMessageProcessingClaimStatus.Acquired,
            IdRadarMessageProcessing = idProcessing.Value,
            LeaseToken = leaseToken,
            IntentosProcesamiento = intentos + 1
        };
    }

    public async Task CompletarAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string? motorInteligencia,
        string resultadoCentralJson,
        CancellationToken cancellationToken = default)
    {
        if (idRadarMessageProcessing <= 0)
            throw new ArgumentOutOfRangeException(nameof(idRadarMessageProcessing));
        if (leaseToken == Guid.Empty)
            throw new ArgumentException("Se requiere el lease activo.", nameof(leaseToken));
        if (string.IsNullOrWhiteSpace(resultadoCentralJson))
            throw new ArgumentException("Se requiere el resultado central serializado.", nameof(resultadoCentralJson));

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sql = @"
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET Estado = N'COMPLETADO',
    MotorInteligencia = @motorInteligencia,
    ResultadoCentralJson = @resultadoCentralJson,
    InterpretadoUtc = COALESCE(InterpretadoUtc, SYSUTCDATETIME()),
    MatchingCompletadoUtc = SYSUTCDATETIME(),
    TerminadoUtc = SYSUTCDATETIME(),
    ReintentarDespuesUtc = NULL,
    UltimoError = NULL,
    ActualizadoUtc = SYSUTCDATETIME(),
    LeaseToken = NULL,
    LeaseHastaUtc = NULL
WHERE IdRadarMessageProcessing = @idProcessing
  AND Estado = N'PROCESANDO'
  AND LeaseToken = @leaseToken;";

        int afectados;
        await using (SqlCommand cmd = new(sql, conexion, tx))
        {
            cmd.Parameters.Add("@motorInteligencia", SqlDbType.NVarChar, 200).Value =
                (object?)NormalizarNullable(motorInteligencia, 200) ?? DBNull.Value;
            cmd.Parameters.Add("@resultadoCentralJson", SqlDbType.NVarChar, -1).Value = resultadoCentralJson;
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idRadarMessageProcessing;
            cmd.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
            afectados = await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (afectados != 1)
        {
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("El lease de procesamiento RADAR ya no es válido; no se sobrescribió el resultado.");
        }

        await InsertarEventoAsync(
            conexion,
            tx,
            idRadarMessageProcessing,
            "PROCESAMIENTO_COMPLETADO",
            "PROCESANDO",
            "COMPLETADO",
            "Interpretación y matching central persistidos correctamente.",
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<bool> MarcarFalloReintentableAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string error,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (idRadarMessageProcessing <= 0 || leaseToken == Guid.Empty)
            return false;

        int retrySeconds = (int)Math.Clamp(Math.Ceiling(retryDelay.TotalSeconds), 1d, 3600d);
        string detalle = string.IsNullOrWhiteSpace(error)
            ? "Fallo central sin detalle."
            : error.Trim()[..Math.Min(error.Trim().Length, 2000)];

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sql = @"
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET Estado = N'FALLIDO_REINTENTABLE',
    ReintentarDespuesUtc = DATEADD(SECOND, @retrySeconds, SYSUTCDATETIME()),
    UltimoError = @error,
    ActualizadoUtc = SYSUTCDATETIME(),
    LeaseToken = NULL,
    LeaseHastaUtc = NULL
WHERE IdRadarMessageProcessing = @idProcessing
  AND Estado = N'PROCESANDO'
  AND LeaseToken = @leaseToken;";

        int afectados;
        await using (SqlCommand cmd = new(sql, conexion, tx))
        {
            cmd.Parameters.Add("@retrySeconds", SqlDbType.Int).Value = retrySeconds;
            cmd.Parameters.Add("@error", SqlDbType.NVarChar, 2000).Value = detalle;
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idRadarMessageProcessing;
            cmd.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
            afectados = await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (afectados == 1)
        {
            await InsertarEventoAsync(
                conexion,
                tx,
                idRadarMessageProcessing,
                "PROCESAMIENTO_FALLIDO",
                "PROCESANDO",
                "FALLIDO_REINTENTABLE",
                detalle,
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return afectados == 1;
    }

    private static async Task InsertarEventoAsync(
        SqlConnection conexion,
        SqlTransaction tx,
        long idRadarMessageProcessing,
        string tipoEvento,
        string? estadoAnterior,
        string? estadoNuevo,
        string? detalle,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT dbo.RSMAPS_RadarMessageEvent
(
    IdRadarMessageProcessing,
    TipoEvento,
    EstadoAnterior,
    EstadoNuevo,
    Detalle,
    CreadoUtc
)
VALUES
(
    @idProcessing,
    @tipoEvento,
    @estadoAnterior,
    @estadoNuevo,
    @detalle,
    SYSUTCDATETIME()
);";

        await using SqlCommand cmd = new(sql, conexion, tx);
        cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idRadarMessageProcessing;
        cmd.Parameters.Add("@tipoEvento", SqlDbType.NVarChar, 60).Value = tipoEvento;
        cmd.Parameters.Add("@estadoAnterior", SqlDbType.NVarChar, 40).Value = (object?)estadoAnterior ?? DBNull.Value;
        cmd.Parameters.Add("@estadoNuevo", SqlDbType.NVarChar, 40).Value = (object?)estadoNuevo ?? DBNull.Value;
        cmd.Parameters.Add("@detalle", SqlDbType.NVarChar, 2000).Value = (object?)detalle ?? DBNull.Value;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? NormalizarNullable(string? valor, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        string limpio = valor.Trim();
        return limpio[..Math.Min(limpio.Length, maxLength)];
    }
}
