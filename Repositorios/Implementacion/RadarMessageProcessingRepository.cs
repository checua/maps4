using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarMessageProcessingRepository : IRadarMessageProcessingRepository
{
    private readonly string _cadenaSQL;
    private readonly int _maxIntentos;

    public RadarMessageProcessingRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        string? configured = configuration["Radar:Processing:MaxAttempts"];
        _maxIntentos = configured is null ? 5 : int.Parse(configured, System.Globalization.CultureInfo.InvariantCulture);
        if (_maxIntentos is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Radar:Processing:MaxAttempts debe estar entre 1 y 100.");
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
    MaxIntentosProcesamiento,
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
        int maxIntentos = _maxIntentos;
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
                maxIntentos = Convert.ToInt32(dr["MaxIntentosProcesamiento"]);
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
    MaxIntentosProcesamiento,
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
    @maxIntentos,
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
                cmd.Parameters.Add("@maxIntentos", SqlDbType.Int).Value = _maxIntentos;

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

        // COMPLETADO never becomes a new claim, even if its cached JSON is damaged.
        if (string.Equals(estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase))
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
        if (string.Equals(estado, "FALLIDO_PERMANENTE", StringComparison.OrdinalIgnoreCase))
        {
            await tx.CommitAsync(cancellationToken);
            return new RadarMessageProcessingClaimResult
            {
                Status = RadarMessageProcessingClaimStatus.PermanentFailure,
                IdRadarMessageProcessing = idProcessing.Value,
                IntentosProcesamiento = intentos
            };
        }
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

        // Respect an in-flight last attempt. Only an expired owner can be superseded.
        if (intentos >= maxIntentos)
        {
            await FinalizarFalloAsync(conexion, tx, idProcessing.Value, estado,
                "Presupuesto de intentos centrales agotado.", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new RadarMessageProcessingClaimResult
            {
                Status = RadarMessageProcessingClaimStatus.PermanentFailure,
                IdRadarMessageProcessing = idProcessing.Value,
                IntentosProcesamiento = intentos
            };
        }

        if (estado is not ("DETECTADO" or "FALLIDO_REINTENTABLE" or "PROCESANDO"))
            throw new InvalidOperationException("Estado central desconocido; no se permite reclamarlo.");

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
WHERE IdRadarMessageProcessing = @idProcessing
  AND Estado IN (N'DETECTADO', N'FALLIDO_REINTENTABLE', N'PROCESANDO')
  AND IntentosProcesamiento < MaxIntentosProcesamiento
  AND (LeaseHastaUtc IS NULL OR LeaseHastaUtc <= SYSUTCDATETIME());";

        await using (SqlCommand cmd = new(sqlReclamar, conexion, tx))
        {
            cmd.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
            cmd.Parameters.Add("@leaseSeconds", SqlDbType.Int).Value = leaseSeconds;
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idProcessing.Value;
            if (await cmd.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("El claim central perdió su condición transaccional.");
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
    TerminadoUtc = NULL,
    DownstreamAckUtc = NULL,
    DisposicionTerminal = NULL,
    ReintentarDespuesUtc = NULL,
    UltimoError = NULL,
    ActualizadoUtc = SYSUTCDATETIME(),
    LeaseToken = NULL,
    LeaseHastaUtc = NULL
WHERE IdRadarMessageProcessing = @idProcessing
  AND Estado = N'PROCESANDO'
  AND LeaseToken = @leaseToken
  AND LeaseHastaUtc > SYSUTCDATETIME();";

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

    public async Task<bool> MarcarTerminadoAsync(
        Guid idAgent,
        string chatOrigen,
        string messageId,
        string disposicionTerminal,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty ||
            string.IsNullOrWhiteSpace(chatOrigen) ||
            string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(disposicionTerminal))
        {
            return false;
        }

        string chat = chatOrigen.Trim();
        string id = messageId.Trim();
        string disposicion = disposicionTerminal.Trim();
        if (chat.Length > 300 || id.Length > 200 || disposicion.Length > 60)
            return false;

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sqlUpdate = @"
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET DownstreamAckUtc = SYSUTCDATETIME(),
    DisposicionTerminal = @disposicion,
    TerminadoUtc = SYSUTCDATETIME(),
    ActualizadoUtc = SYSUTCDATETIME()
WHERE IdAgent = @idAgent
  AND ChatOrigen = @chatOrigen
  AND MessageId = @messageId
  AND Estado = N'COMPLETADO'
  AND ResultadoCentralJson IS NOT NULL
  AND DownstreamAckUtc IS NULL;";

        int afectados;
        long? idProcessing = null;

        const string sqlFind = @"
SELECT TOP (1)
    IdRadarMessageProcessing,
    DownstreamAckUtc,
    ResultadoCentralJson
FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
WHERE IdAgent = @idAgent
  AND ChatOrigen = @chatOrigen
  AND MessageId = @messageId
  AND Estado = N'COMPLETADO'
  AND ResultadoCentralJson IS NOT NULL;";

        bool yaTerminado = false;
        string? cacheJson = null;
        await using (SqlCommand find = new(sqlFind, conexion, tx))
        {
            find.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            find.Parameters.Add("@chatOrigen", SqlDbType.NVarChar, 300).Value = chat;
            find.Parameters.Add("@messageId", SqlDbType.NVarChar, 200).Value = id;

            await using SqlDataReader dr = await find.ExecuteReaderAsync(cancellationToken);
            if (await dr.ReadAsync(cancellationToken))
            {
                idProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]);
                yaTerminado = dr["DownstreamAckUtc"] != DBNull.Value;
                cacheJson = dr["ResultadoCentralJson"].ToString();
            }
        }

        if (!idProcessing.HasValue || !RadarCentralResultReader.TryRead(cacheJson, out _, id, chat))
        {
            await tx.CommitAsync(cancellationToken);
            return false;
        }

        if (yaTerminado)
        {
            await tx.CommitAsync(cancellationToken);
            return true;
        }

        await using (SqlCommand update = new(sqlUpdate, conexion, tx))
        {
            update.Parameters.Add("@disposicion", SqlDbType.NVarChar, 60).Value = disposicion;
            update.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            update.Parameters.Add("@chatOrigen", SqlDbType.NVarChar, 300).Value = chat;
            update.Parameters.Add("@messageId", SqlDbType.NVarChar, 200).Value = id;
            afectados = await update.ExecuteNonQueryAsync(cancellationToken);
        }

        if (afectados != 1)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        await InsertarEventoAsync(
            conexion,
            tx,
            idProcessing.Value,
            "WORKFLOW_TERMINADO",
            "COMPLETADO",
            "COMPLETADO",
            "ACK terminal durable: " + disposicion,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return true;
    }
    public async Task<RadarMessageProcessingFailureStatus> RegistrarFalloAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string error,
        TimeSpan retryDelay,
        bool permanente = false,
        CancellationToken cancellationToken = default)
    {
        if (idRadarMessageProcessing <= 0 || leaseToken == Guid.Empty)
            return RadarMessageProcessingFailureStatus.LeaseLost;

        int retrySeconds = (int)Math.Clamp(Math.Ceiling(retryDelay.TotalSeconds), 1d, 3600d);
        string detalle = string.IsNullOrWhiteSpace(error)
            ? "Fallo central sin detalle."
            : error.Trim()[..Math.Min(error.Trim().Length, 2000)];

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sqlOwner = @"
SELECT IntentosProcesamiento, MaxIntentosProcesamiento
FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
WHERE IdRadarMessageProcessing = @idProcessing
  AND Estado = N'PROCESANDO' AND LeaseToken = @leaseToken
  AND LeaseHastaUtc > SYSUTCDATETIME();";
        int intentos;
        int maxIntentos;
        await using (SqlCommand owner = new(sqlOwner, conexion, tx))
        {
            owner.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idRadarMessageProcessing;
            owner.Parameters.Add("@leaseToken", SqlDbType.UniqueIdentifier).Value = leaseToken;
            await using SqlDataReader reader = await owner.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return RadarMessageProcessingFailureStatus.LeaseLost;
            intentos = reader.GetInt32(0);
            maxIntentos = reader.GetInt32(1);
        }
        if (permanente || intentos >= maxIntentos)
        {
            await FinalizarFalloAsync(conexion, tx, idRadarMessageProcessing, "PROCESANDO", detalle, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return RadarMessageProcessingFailureStatus.PermanentFailure;
        }

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
        return afectados == 1 ? RadarMessageProcessingFailureStatus.Retryable : RadarMessageProcessingFailureStatus.LeaseLost;
    }

    private static async Task FinalizarFalloAsync(
        SqlConnection conexion, SqlTransaction tx, long id, string estadoAnterior,
        string error, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE dbo.RSMAPS_RadarMessageProcessing
SET Estado = N'FALLIDO_PERMANENTE', UltimoError = @error,
    TerminadoUtc = SYSUTCDATETIME(), ActualizadoUtc = SYSUTCDATETIME(),
    ReintentarDespuesUtc = NULL, LeaseToken = NULL, LeaseHastaUtc = NULL
WHERE IdRadarMessageProcessing = @id AND Estado IN (N'DETECTADO', N'PROCESANDO', N'FALLIDO_REINTENTABLE');";
        await using SqlCommand command = new(sql, conexion, tx);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@error", SqlDbType.NVarChar, 2000).Value = error;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("No se pudo terminalizar el procesamiento.");
        await InsertarEventoAsync(conexion, tx, id, "PROCESAMIENTO_FALLIDO_PERMANENTE",
            estadoAnterior, "FALLIDO_PERMANENTE", error, cancellationToken);
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
