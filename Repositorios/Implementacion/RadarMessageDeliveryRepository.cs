using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarMessageDeliveryRepository : IRadarMessageDeliveryRepository
{
    private readonly string _cadenaSQL;

    public RadarMessageDeliveryRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<RadarDeliveryPrepareResult> PrepararAsync(
        Guid idAgent,
        RadarDeliveryPrepareRequest request,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            throw new ArgumentException("Se requiere un IdAgent válido.", nameof(idAgent));
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ChatOrigen))
            throw new ArgumentException("Se requiere ChatOrigen.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.MessageId))
            throw new ArgumentException("Se requiere MessageId.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ClaveEntrega))
            throw new ArgumentException("Se requiere ClaveEntrega.", nameof(request));
        if (request.SolicitudIndice < 0)
            throw new ArgumentOutOfRangeException(nameof(request.SolicitudIndice));
        if (request.Puntuacion.HasValue && (request.Puntuacion.Value < 0 || request.Puntuacion.Value > 100))
            throw new ArgumentOutOfRangeException(nameof(request.Puntuacion));

        string chatOrigen = Recortar(request.ChatOrigen, 300);
        string messageId = Recortar(request.MessageId, 200);
        string claveEntrega = Recortar(request.ClaveEntrega, 300);

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        const string sqlProcessing = @"
SELECT TOP (1)
    IdRadarMessageProcessing,
    Estado
FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
WHERE IdAgent = @idAgent
  AND ChatOrigen = @chatOrigen
  AND MessageId = @messageId;";

        long idProcessing;
        string estadoProcessing;

        await using (SqlCommand cmd = new(sqlProcessing, conexion, tx))
        {
            cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;
            cmd.Parameters.Add("@chatOrigen", SqlDbType.NVarChar, 300).Value = chatOrigen;
            cmd.Parameters.Add("@messageId", SqlDbType.NVarChar, 200).Value = messageId;

            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await dr.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("No existe procesamiento central persistido para esta demanda RADAR.");
            }

            idProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]);
            estadoProcessing = dr["Estado"].ToString() ?? string.Empty;
        }

        if (!string.Equals(estadoProcessing, "COMPLETADO", StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("La entrega no puede prepararse hasta que Intelligence y Matching estén COMPLETADOS.");
        }

        const string sqlDelivery = @"
SELECT TOP (1)
    IdRadarMessageDelivery,
    Estado,
    IntentosEntrega
FROM dbo.RSMAPS_RadarMessageDelivery WITH (UPDLOCK, HOLDLOCK)
WHERE IdRadarMessageProcessing = @idProcessing
  AND ClaveEntrega = @claveEntrega;";

        long? idDelivery = null;
        string estadoDelivery = string.Empty;
        int intentosEntrega = 0;

        await using (SqlCommand cmd = new(sqlDelivery, conexion, tx))
        {
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idProcessing;
            cmd.Parameters.Add("@claveEntrega", SqlDbType.NVarChar, 300).Value = claveEntrega;

            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await dr.ReadAsync(cancellationToken))
            {
                idDelivery = Convert.ToInt64(dr["IdRadarMessageDelivery"]);
                estadoDelivery = dr["Estado"].ToString() ?? string.Empty;
                intentosEntrega = Convert.ToInt32(dr["IntentosEntrega"]);
            }
        }

        if (idDelivery.HasValue && string.Equals(estadoDelivery, "ENVIADO", StringComparison.OrdinalIgnoreCase))
        {
            await InsertarEventoAsync(
                conexion,
                tx,
                idProcessing,
                "ENTREGA_REUTILIZADA",
                "COMPLETADO",
                "COMPLETADO",
                $"Entrega {idDelivery.Value} ya confirmada; se evita un envío duplicado.",
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new RadarDeliveryPrepareResult
            {
                IdRadarMessageDelivery = idDelivery.Value,
                Estado = "ENVIADO",
                YaEnviado = true,
                IntentosEntrega = intentosEntrega
            };
        }

        if (idDelivery.HasValue)
        {
            const string sqlRetry = @"
UPDATE dbo.RSMAPS_RadarMessageDelivery
SET Estado = N'PENDIENTE',
    IntentosEntrega = IntentosEntrega + 1,
    UltimoIntentoUtc = SYSUTCDATETIME(),
    UltimoError = NULL,
    ActualizadoUtc = SYSUTCDATETIME()
WHERE IdRadarMessageDelivery = @idDelivery;";

            await using (SqlCommand cmd = new(sqlRetry, conexion, tx))
            {
                cmd.Parameters.Add("@idDelivery", SqlDbType.BigInt).Value = idDelivery.Value;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertarEventoAsync(
                conexion,
                tx,
                idProcessing,
                "ENTREGA_RECLAMADA",
                "COMPLETADO",
                "COMPLETADO",
                $"Entrega {idDelivery.Value} preparada para reintento #{intentosEntrega + 1}.",
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new RadarDeliveryPrepareResult
            {
                IdRadarMessageDelivery = idDelivery.Value,
                Estado = "PENDIENTE",
                YaEnviado = false,
                IntentosEntrega = intentosEntrega + 1
            };
        }

        const string sqlInsert = @"
INSERT dbo.RSMAPS_RadarMessageDelivery
(
    IdRadarMessageProcessing,
    SolicitudIndice,
    ClaveEntrega,
    IdInmueble,
    Puntuacion,
    Estado,
    PayloadAlerta,
    IntentosEntrega,
    UltimoIntentoUtc,
    CreadoUtc,
    ActualizadoUtc
)
OUTPUT INSERTED.IdRadarMessageDelivery
VALUES
(
    @idProcessing,
    @solicitudIndice,
    @claveEntrega,
    @idInmueble,
    @puntuacion,
    N'PENDIENTE',
    @payloadAlerta,
    1,
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
);";

        long nuevoIdDelivery;
        await using (SqlCommand cmd = new(sqlInsert, conexion, tx))
        {
            cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idProcessing;
            cmd.Parameters.Add("@solicitudIndice", SqlDbType.Int).Value = request.SolicitudIndice;
            cmd.Parameters.Add("@claveEntrega", SqlDbType.NVarChar, 300).Value = claveEntrega;
            cmd.Parameters.Add("@idInmueble", SqlDbType.Int).Value = (object?)request.IdInmueble ?? DBNull.Value;
            cmd.Parameters.Add("@puntuacion", SqlDbType.Int).Value = (object?)request.Puntuacion ?? DBNull.Value;
            cmd.Parameters.Add("@payloadAlerta", SqlDbType.NVarChar, -1).Value = (object?)request.PayloadAlerta ?? DBNull.Value;

            object? scalar = await cmd.ExecuteScalarAsync(cancellationToken);
            nuevoIdDelivery = Convert.ToInt64(scalar);
        }

        await InsertarEventoAsync(
            conexion,
            tx,
            idProcessing,
            "ENTREGA_PREPARADA",
            "COMPLETADO",
            "COMPLETADO",
            $"Entrega {nuevoIdDelivery} registrada para solicitud #{request.SolicitudIndice}.",
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return new RadarDeliveryPrepareResult
        {
            IdRadarMessageDelivery = nuevoIdDelivery,
            Estado = "PENDIENTE",
            YaEnviado = false,
            IntentosEntrega = 1
        };
    }

    public async Task<RadarDeliveryCompleteResult> CompletarAsync(
        Guid idAgent,
        RadarDeliveryCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            throw new ArgumentException("Se requiere un IdAgent válido.", nameof(idAgent));
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (request.IdRadarMessageDelivery <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.IdRadarMessageDelivery));

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlTransaction tx = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        const string sqlSelect = @"
SELECT TOP (1)
    d.IdRadarMessageDelivery,
    d.IdRadarMessageProcessing,
    d.Estado,
    d.IntentosEntrega
FROM dbo.RSMAPS_RadarMessageDelivery d WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.RSMAPS_RadarMessageProcessing p
    ON p.IdRadarMessageProcessing = d.IdRadarMessageProcessing
WHERE d.IdRadarMessageDelivery = @idDelivery
  AND p.IdAgent = @idAgent;";

        long idProcessing;
        string estado;
        int intentos;

        await using (SqlCommand cmd = new(sqlSelect, conexion, tx))
        {
            cmd.Parameters.Add("@idDelivery", SqlDbType.BigInt).Value = request.IdRadarMessageDelivery;
            cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

            await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await dr.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("La entrega RADAR no existe o no pertenece al Agent autenticado.");
            }

            idProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]);
            estado = dr["Estado"].ToString() ?? string.Empty;
            intentos = Convert.ToInt32(dr["IntentosEntrega"]);
        }

        if (string.Equals(estado, "ENVIADO", StringComparison.OrdinalIgnoreCase))
        {
            await tx.CommitAsync(cancellationToken);
            return new RadarDeliveryCompleteResult
            {
                IdRadarMessageDelivery = request.IdRadarMessageDelivery,
                Estado = "ENVIADO",
                YaEnviado = true,
                IntentosEntrega = intentos
            };
        }

        string? error = string.IsNullOrWhiteSpace(request.Error)
            ? null
            : Recortar(request.Error, 2000);

        if (request.Enviada)
        {
            const string sqlSuccess = @"
UPDATE dbo.RSMAPS_RadarMessageDelivery
SET Estado = N'ENVIADO',
    EnviadoUtc = COALESCE(EnviadoUtc, SYSUTCDATETIME()),
    UltimoError = NULL,
    ActualizadoUtc = SYSUTCDATETIME()
WHERE IdRadarMessageDelivery = @idDelivery;";

            await using (SqlCommand cmd = new(sqlSuccess, conexion, tx))
            {
                cmd.Parameters.Add("@idDelivery", SqlDbType.BigInt).Value = request.IdRadarMessageDelivery;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertarEventoAsync(
                conexion,
                tx,
                idProcessing,
                "ENTREGA_ENVIADA",
                "COMPLETADO",
                "COMPLETADO",
                $"Entrega {request.IdRadarMessageDelivery} confirmada por el Agent.",
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new RadarDeliveryCompleteResult
            {
                IdRadarMessageDelivery = request.IdRadarMessageDelivery,
                Estado = "ENVIADO",
                YaEnviado = true,
                IntentosEntrega = intentos
            };
        }

        const string sqlFailure = @"
UPDATE dbo.RSMAPS_RadarMessageDelivery
SET Estado = N'FALLIDO_REINTENTABLE',
    UltimoError = @error,
    ActualizadoUtc = SYSUTCDATETIME()
WHERE IdRadarMessageDelivery = @idDelivery;";

        await using (SqlCommand cmd = new(sqlFailure, conexion, tx))
        {
            cmd.Parameters.Add("@error", SqlDbType.NVarChar, 2000).Value = (object?)error ?? "Fallo de entrega sin detalle.";
            cmd.Parameters.Add("@idDelivery", SqlDbType.BigInt).Value = request.IdRadarMessageDelivery;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertarEventoAsync(
            conexion,
            tx,
            idProcessing,
            "ENTREGA_FALLIDA",
            "COMPLETADO",
            "COMPLETADO",
            error ?? "Fallo de entrega sin detalle.",
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return new RadarDeliveryCompleteResult
        {
            IdRadarMessageDelivery = request.IdRadarMessageDelivery,
            Estado = "FALLIDO_REINTENTABLE",
            YaEnviado = false,
            IntentosEntrega = intentos
        };
    }

    private static async Task InsertarEventoAsync(
        SqlConnection conexion,
        SqlTransaction tx,
        long idProcessing,
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
        cmd.Parameters.Add("@idProcessing", SqlDbType.BigInt).Value = idProcessing;
        cmd.Parameters.Add("@tipoEvento", SqlDbType.NVarChar, 60).Value = tipoEvento;
        cmd.Parameters.Add("@estadoAnterior", SqlDbType.NVarChar, 40).Value = (object?)estadoAnterior ?? DBNull.Value;
        cmd.Parameters.Add("@estadoNuevo", SqlDbType.NVarChar, 40).Value = (object?)estadoNuevo ?? DBNull.Value;
        cmd.Parameters.Add("@detalle", SqlDbType.NVarChar, 2000).Value = (object?)detalle ?? DBNull.Value;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Recortar(string valor, int maxLength)
    {
        string limpio = valor.Trim();
        return limpio.Length <= maxLength ? limpio : limpio[..maxLength];
    }
}
