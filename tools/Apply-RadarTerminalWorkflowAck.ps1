param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Replace-Once {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Old,
        [Parameter(Mandatory = $true)][string]$New,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $first = $Text.IndexOf($Old, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Could not locate patch anchor: $Description"
    }

    $second = $Text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Patch anchor is not unique: $Description"
    }

    return $Text.Substring(0, $first) + $New + $Text.Substring($first + $Old.Length)
}

$targets = @(
    'Repositorios/Contrato/IRadarMessageProcessingRepository.cs',
    'Repositorios/Implementacion/RadarMessageProcessingRepository.cs',
    'Models/RadarPendingProcessingModels.cs',
    'Repositorios/Contrato/IRadarPendingProcessingRepository.cs',
    'Repositorios/Implementacion/RadarPendingProcessingRepository.cs',
    'Controllers/RadarProcessingRecoveryController.cs',
    'RSMaps.Radar.Listener/Services/RadarWorkflowClient.cs',
    'RSMaps.Radar.Listener/Program.cs',
    'sql/RSMaps2/45_radar_terminal_workflow_ack.sql'
)

$status = & git -C $RepoRoot status --porcelain -- @targets
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect git status.'
}
if ($status) {
    throw "Target RADAR files already have local changes. Commit or revert them before applying this helper.`n$status"
}

$interfaceProcessingPath = Join-Path $RepoRoot 'Repositorios\Contrato\IRadarMessageProcessingRepository.cs'
$repositoryProcessingPath = Join-Path $RepoRoot 'Repositorios\Implementacion\RadarMessageProcessingRepository.cs'
$modelsPath = Join-Path $RepoRoot 'Models\RadarPendingProcessingModels.cs'
$interfacePendingPath = Join-Path $RepoRoot 'Repositorios\Contrato\IRadarPendingProcessingRepository.cs'
$repositoryPendingPath = Join-Path $RepoRoot 'Repositorios\Implementacion\RadarPendingProcessingRepository.cs'
$controllerPath = Join-Path $RepoRoot 'Controllers\RadarProcessingRecoveryController.cs'
$workflowClientPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Services\RadarWorkflowClient.cs'
$listenerProgramPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
$migrationPath = Join-Path $RepoRoot 'sql\RSMaps2\45_radar_terminal_workflow_ack.sql'

if (Test-Path -LiteralPath $workflowClientPath) {
    throw "Target file already exists: $workflowClientPath"
}
if (Test-Path -LiteralPath $migrationPath) {
    throw "Target migration already exists: $migrationPath"
}

$interfaceProcessing = @'
using maps4.Models;
using RSMaps.Radar.Listener.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarMessageProcessingRepository
{
    Task<RadarMessageProcessingClaimResult> ReclamarAsync(
        Guid idAgent,
        RadarMessage mensaje,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task CompletarAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string? motorInteligencia,
        string resultadoCentralJson,
        CancellationToken cancellationToken = default);

    Task<bool> MarcarTerminadoAsync(
        Guid idAgent,
        string chatOrigen,
        string messageId,
        string disposicionTerminal,
        CancellationToken cancellationToken = default);

    Task<bool> MarcarFalloReintentableAsync(
        long idRadarMessageProcessing,
        Guid leaseToken,
        string error,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}
'@

$models = @'
namespace maps4.Models;

public sealed class RadarPendingProcessingItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public int IntentosProcesamiento { get; init; }
    public DateTime DetectadoUtc { get; init; }
    public DateTime? ReintentarDespuesUtc { get; init; }
    public DateTime? LeaseHastaUtc { get; init; }
}

public sealed class RadarPendingWorkflowItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string? MotorInteligencia { get; init; }
    public string ResultadoCentralJson { get; init; } = string.Empty;
    public DateTime DetectadoUtc { get; init; }
    public DateTime? MatchingCompletadoUtc { get; init; }
}

public sealed class RadarTerminalAckRequest
{
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string DisposicionTerminal { get; init; } = string.Empty;
}
'@

$interfacePending = @'
using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarPendingProcessingRepository
{
    Task<IReadOnlyList<RadarPendingProcessingItem>> ListarAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadarPendingWorkflowItem>> ListarDownstreamPendienteAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default);
}
'@

$repositoryPending = @'
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarPendingProcessingRepository : IRadarPendingProcessingRepository
{
    private readonly string _cadenaSQL;

    public RadarPendingProcessingRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<IReadOnlyList<RadarPendingProcessingItem>> ListarAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return [];

        int limite = Math.Clamp(max, 1, 100);
        var items = new List<RadarPendingProcessingItem>();

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (@max)
    IdRadarMessageProcessing,
    ChatOrigen,
    MessageId,
    Autor,
    Telefono,
    MensajeOriginal,
    Estado,
    IntentosProcesamiento,
    DetectadoUtc,
    ReintentarDespuesUtc,
    LeaseHastaUtc
FROM dbo.RSMAPS_RadarMessageProcessing WITH (READPAST)
WHERE IdAgent = @idAgent
  AND ResultadoCentralJson IS NULL
  AND
  (
      (
          Estado = N'FALLIDO_REINTENTABLE'
          AND (ReintentarDespuesUtc IS NULL OR ReintentarDespuesUtc <= SYSUTCDATETIME())
      )
      OR
      (
          Estado = N'PROCESANDO'
          AND (LeaseHastaUtc IS NULL OR LeaseHastaUtc <= SYSUTCDATETIME())
      )
  )
ORDER BY
    COALESCE(ReintentarDespuesUtc, LeaseHastaUtc, DetectadoUtc),
    IdRadarMessageProcessing;";

        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@max", SqlDbType.Int).Value = limite;
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            items.Add(new RadarPendingProcessingItem
            {
                IdRadarMessageProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]),
                ChatOrigen = dr["ChatOrigen"].ToString() ?? string.Empty,
                MessageId = dr["MessageId"].ToString() ?? string.Empty,
                Autor = dr["Autor"] == DBNull.Value ? null : dr["Autor"].ToString(),
                Telefono = dr["Telefono"] == DBNull.Value ? null : dr["Telefono"].ToString(),
                MensajeOriginal = dr["MensajeOriginal"] == DBNull.Value
                    ? string.Empty
                    : dr["MensajeOriginal"].ToString() ?? string.Empty,
                Estado = dr["Estado"].ToString() ?? string.Empty,
                IntentosProcesamiento = Convert.ToInt32(dr["IntentosProcesamiento"]),
                DetectadoUtc = Convert.ToDateTime(dr["DetectadoUtc"]),
                ReintentarDespuesUtc = dr["ReintentarDespuesUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["ReintentarDespuesUtc"]),
                LeaseHastaUtc = dr["LeaseHastaUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["LeaseHastaUtc"])
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<RadarPendingWorkflowItem>> ListarDownstreamPendienteAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            return [];

        int limite = Math.Clamp(max, 1, 100);
        var items = new List<RadarPendingWorkflowItem>();

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (@max)
    IdRadarMessageProcessing,
    ChatOrigen,
    MessageId,
    Autor,
    Telefono,
    MensajeOriginal,
    MotorInteligencia,
    ResultadoCentralJson,
    DetectadoUtc,
    MatchingCompletadoUtc
FROM dbo.RSMAPS_RadarMessageProcessing WITH (READPAST)
WHERE IdAgent = @idAgent
  AND Estado = N'COMPLETADO'
  AND ResultadoCentralJson IS NOT NULL
  AND DownstreamAckUtc IS NULL
ORDER BY MatchingCompletadoUtc, IdRadarMessageProcessing;";

        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@max", SqlDbType.Int).Value = limite;
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            items.Add(new RadarPendingWorkflowItem
            {
                IdRadarMessageProcessing = Convert.ToInt64(dr["IdRadarMessageProcessing"]),
                ChatOrigen = dr["ChatOrigen"].ToString() ?? string.Empty,
                MessageId = dr["MessageId"].ToString() ?? string.Empty,
                Autor = dr["Autor"] == DBNull.Value ? null : dr["Autor"].ToString(),
                Telefono = dr["Telefono"] == DBNull.Value ? null : dr["Telefono"].ToString(),
                MensajeOriginal = dr["MensajeOriginal"] == DBNull.Value
                    ? string.Empty
                    : dr["MensajeOriginal"].ToString() ?? string.Empty,
                MotorInteligencia = dr["MotorInteligencia"] == DBNull.Value
                    ? null
                    : dr["MotorInteligencia"].ToString(),
                ResultadoCentralJson = dr["ResultadoCentralJson"].ToString() ?? string.Empty,
                DetectadoUtc = Convert.ToDateTime(dr["DetectadoUtc"]),
                MatchingCompletadoUtc = dr["MatchingCompletadoUtc"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(dr["MatchingCompletadoUtc"])
            });
        }

        return items;
    }
}
'@

$controller = @'
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/processing")]
public sealed class RadarProcessingRecoveryController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarPendingProcessingRepository _pendingRepository;
    private readonly IRadarMessageProcessingRepository _processingRepository;

    public RadarProcessingRecoveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarPendingProcessingRepository pendingRepository,
        IRadarMessageProcessingRepository processingRepository)
    {
        _pairingRepository = pairingRepository;
        _pendingRepository = pendingRepository;
        _processingRepository = processingRepository;
    }

    [AllowAnonymous]
    [HttpGet("agent/pending")]
    public async Task<IActionResult> Pendientes(
        [FromQuery] int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        IReadOnlyList<RadarPendingProcessingItem> items = await _pendingRepository.ListarAsync(
            agent.IdAgent,
            max,
            cancellationToken);

        return Ok(items);
    }

    [AllowAnonymous]
    [HttpGet("agent/pending-downstream")]
    public async Task<IActionResult> DownstreamPendiente(
        [FromQuery] int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        IReadOnlyList<RadarPendingWorkflowItem> items = await _pendingRepository.ListarDownstreamPendienteAsync(
            agent.IdAgent,
            max,
            cancellationToken);

        return Ok(items);
    }

    [AllowAnonymous]
    [HttpPost("agent/terminal-ack")]
    public async Task<IActionResult> TerminalAck(
        [FromBody] RadarTerminalAckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.ChatOrigen) ||
            string.IsNullOrWhiteSpace(request.MessageId) ||
            string.IsNullOrWhiteSpace(request.DisposicionTerminal))
        {
            return BadRequest(new { mensaje = "ChatOrigen, MessageId and DisposicionTerminal are required." });
        }

        RadarAgentAuthenticationResult? agent = await AutenticarAgentAsync(cancellationToken);
        if (agent is null)
            return Unauthorized(new { mensaje = "RADAR Agent invalid, revoked or without active account." });

        bool ok = await _processingRepository.MarcarTerminadoAsync(
            agent.IdAgent,
            request.ChatOrigen,
            request.MessageId,
            request.DisposicionTerminal,
            cancellationToken);

        if (!ok)
        {
            return Conflict(new
            {
                mensaje = "RADAR could not persist the terminal workflow ACK for this message."
            });
        }

        return Ok(new { ok = true });
    }

    private async Task<RadarAgentAuthenticationResult?> AutenticarAgentAsync(
        CancellationToken cancellationToken)
    {
        string authorization = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string credencial = authorization[bearer.Length..].Trim();
        if (string.IsNullOrWhiteSpace(credencial))
            return null;

        return await _pairingRepository.ValidarCredencialAsync(
            credencial,
            cancellationToken);
    }
}
'@

$workflowClient = @'
using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingWorkflowClientItem
{
    public long IdRadarMessageProcessing { get; init; }
    public string ChatOrigen { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string? Autor { get; init; }
    public string? Telefono { get; init; }
    public string MensajeOriginal { get; init; } = string.Empty;
    public string? MotorInteligencia { get; init; }
    public string ResultadoCentralJson { get; init; } = string.Empty;
    public DateTime DetectadoUtc { get; init; }
    public DateTime? MatchingCompletadoUtc { get; init; }
}

public sealed class RadarPendingWorkflowClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingWorkflowClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = string.Empty;
}

public sealed class RadarTerminalAckClientResult
{
    public bool Ok { get; init; }
    public string Detalle { get; init; } = string.Empty;
}

public static class RadarWorkflowClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingWorkflowClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        if (!RadarCentralIntelligenceClient.Habilitada)
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = true,
                Detalle = "Central Intelligence is disabled."
            };
        }

        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            int limite = Math.Clamp(max, 1, 100);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/pending-downstream?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingWorkflowClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            List<RadarPendingWorkflowClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingWorkflowClientItem>>(
                    cancellationToken: cancellationToken);

            return new RadarPendingWorkflowClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = false,
                Detalle = "Pending terminal workflow query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingWorkflowClientResult
            {
                Ok = false,
                Detalle = "RSMaps unavailable: " + Recortar(ex.Message, 180)
            };
        }
        finally
        {
            token = string.Empty;
        }
    }

    public static async Task<RadarTerminalAckClientResult> ConfirmarTerminalAsync(
        string chatOrigen,
        string messageId,
        string disposicionTerminal,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarTerminalAckClientResult
            {
                Ok = false,
                Detalle = "Agent credential unavailable: " + Recortar(detalle, 160)
            };
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/terminal-ack");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                chatOrigen,
                messageId,
                disposicionTerminal
            });

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarTerminalAckClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            return new RadarTerminalAckClientResult
            {
                Ok = true,
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarTerminalAckClientResult
            {
                Ok = false,
                Detalle = "Terminal workflow ACK timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarTerminalAckClientResult
            {
                Ok = false,
                Detalle = "RSMaps unavailable: " + Recortar(ex.Message, 180)
            };
        }
        finally
        {
            token = string.Empty;
        }
    }

    private static string Recortar(string texto, int max)
    {
        texto = (texto ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "...";
    }
}
'@

$migration = @'
/* ============================================================
   RSMaps 2.0 - Paso 45
   RADAR: ACK TERMINAL DURABLE DEL WORKFLOW

   Objetivo:
   - Separar "Intelligence + matching completados" de "workflow terminado".
   - Permitir recuperar mensajes cuyo resultado central ya fue persistido,
     pero cuyo downstream no alcanzo a terminar antes de reiniciar el Agent.
   - Evitar inferir el estado terminal por la existencia o ausencia de Delivery.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54500, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

IF OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing', N'U') IS NULL
BEGIN
    THROW 54501, 'Primero debe ejecutarse 44_radar_message_processing_persistence.sql.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DownstreamAckUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarMessageProcessing
            ADD DownstreamAckUtc datetime2(3) NULL;
    END;

    IF COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DisposicionTerminal') IS NULL
    BEGIN
        ALTER TABLE dbo.RSMAPS_RadarMessageProcessing
            ADD DisposicionTerminal nvarchar(60) NULL;
    END;

    /*
       Los registros previos a este paso ya usaban TerminadoUtc como si fuera
       finalizacion end-to-end. Se marcan como legado para no reactivar pruebas
       historicas al habilitar la nueva recuperacion downstream.
    */
    UPDATE dbo.RSMAPS_RadarMessageProcessing
    SET DownstreamAckUtc = COALESCE(DownstreamAckUtc, TerminadoUtc, ActualizadoUtc, SYSUTCDATETIME()),
        DisposicionTerminal = COALESCE(DisposicionTerminal, N'LEGACY_PRE_45')
    WHERE Estado = N'COMPLETADO'
      AND ResultadoCentralJson IS NOT NULL
      AND DownstreamAckUtc IS NULL
      AND TerminadoUtc IS NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing')
          AND name = N'IX_RSMAPS_RadarMessageProcessing_DownstreamPendiente'
    )
    BEGIN
        CREATE INDEX IX_RSMAPS_RadarMessageProcessing_DownstreamPendiente
            ON dbo.RSMAPS_RadarMessageProcessing(IdAgent, Estado, DownstreamAckUtc, ActualizadoUtc)
            INCLUDE (ChatOrigen, MessageId, MatchingCompletadoUtc);
    END;

    COMMIT TRANSACTION;

    SELECT
        COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DownstreamAckUtc') AS DownstreamAckUtcBytes,
        COL_LENGTH(N'dbo.RSMAPS_RadarMessageProcessing', N'DisposicionTerminal') AS DisposicionTerminalBytes;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
'@

Write-Utf8NoBom -Path $interfaceProcessingPath -Content $interfaceProcessing
Write-Utf8NoBom -Path $modelsPath -Content $models
Write-Utf8NoBom -Path $interfacePendingPath -Content $interfacePending
Write-Utf8NoBom -Path $repositoryPendingPath -Content $repositoryPending
Write-Utf8NoBom -Path $controllerPath -Content $controller
Write-Utf8NoBom -Path $workflowClientPath -Content $workflowClient
Write-Utf8NoBom -Path $migrationPath -Content $migration

$processingRepo = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $repositoryProcessingPath))

$completeOld = @'
    MatchingCompletadoUtc = SYSUTCDATETIME(),
    TerminadoUtc = SYSUTCDATETIME(),
    ReintentarDespuesUtc = NULL,
'@
$completeNew = @'
    MatchingCompletadoUtc = SYSUTCDATETIME(),
    TerminadoUtc = NULL,
    DownstreamAckUtc = NULL,
    DisposicionTerminal = NULL,
    ReintentarDespuesUtc = NULL,
'@
$processingRepo = Replace-Once -Text $processingRepo -Old $completeOld -New $completeNew -Description 'central completion no longer marks end-to-end termination'

$failureMarker = '    public async Task<bool> MarcarFalloReintentableAsync('
$terminalMethod = @'
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
    DownstreamAckUtc
FROM dbo.RSMAPS_RadarMessageProcessing WITH (UPDLOCK, HOLDLOCK)
WHERE IdAgent = @idAgent
  AND ChatOrigen = @chatOrigen
  AND MessageId = @messageId
  AND Estado = N'COMPLETADO'
  AND ResultadoCentralJson IS NOT NULL;";

        bool yaTerminado = false;
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
            }
        }

        if (!idProcessing.HasValue)
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

'@
$processingRepo = Replace-Once -Text $processingRepo -Old $failureMarker -New ($terminalMethod + $failureMarker) -Description 'terminal workflow repository method'
Write-Utf8NoBom -Path $repositoryProcessingPath -Content $processingRepo

$listener = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $listenerProgramPath))

$usingAnchor = "using System.Text.RegularExpressions;`r`n"
if (-not $listener.Contains($usingAnchor)) {
    $usingAnchor = "using System.Text.RegularExpressions;`n"
}
$listener = Replace-Once -Text $listener -Old $usingAnchor -New ($usingAnchor + 'using System.Text.Json;' + [Environment]::NewLine) -Description 'System.Text.Json using'

$recoveryAnchor = @'
        await RecuperarEntregasDurablesPendientesAsync(
            page,
            idsConocidosPorChat,
            entregasPendientesAlArranque,
            capturarPendientesAlArranque);
        capturarPendientesAlArranque = false;
'@
$recoveryNew = $recoveryAnchor + @'

        await RecuperarWorkflowsDurablesPendientesAsync(
            page,
            idsConocidosPorChat,
            enviosConfirmadosPorSolicitud,
            entregasLabFalladasUnaVez);
'@
$listener = Replace-Once -Text $listener -Old $recoveryAnchor -New $recoveryNew -Description 'workflow recovery call after delivery recovery'

$ordinaryCall = @'
            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                resultado.DemandasInterpretadas,
                resultado.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
'@
$ordinaryCallNew = @'
            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                chat,
                resultado.DemandasInterpretadas,
                resultado.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
'@
$listener = Replace-Once -Text $listener -Old $ordinaryCall -New $ordinaryCallNew -Description 'ordinary downstream chat origin argument'

$signatureOld = @'
static async Task ProcesarDemandasInterpretadasAsync(
    IPage page,
    HashSet<string> idsConocidos,
    IReadOnlyCollection<string> demandasInterpretadas,
'@
$signatureNew = @'
static async Task ProcesarDemandasInterpretadasAsync(
    IPage page,
    HashSet<string> idsConocidos,
    string chatOrigen,
    IReadOnlyCollection<string> demandasInterpretadas,
'@
$listener = Replace-Once -Text $listener -Old $signatureOld -New $signatureNew -Description 'shared downstream chat origin parameter'

$zeroOld = @'
                if (solicitudesMensaje.Count == 0)
                {
                    idsConocidos.Add(messageId);
                    Console.WriteLine($"  [ACK] {messageId}: Intelligence finished with no actionable requests.");
                    continue;
                }

                var mensajeCompletado = true;
'@
$zeroNew = @'
                if (solicitudesMensaje.Count == 0)
                {
                    if (!await ConfirmarAckTerminalAsync(chatOrigen, messageId, "SIN_SOLICITUD_ACCIONABLE"))
                    {
                        Console.WriteLine($"  [PENDING] {messageId}: durable terminal ACK is still pending.");
                        continue;
                    }

                    idsConocidos.Add(messageId);
                    Console.WriteLine($"  [ACK DURABLE] {messageId}: Intelligence finished with no actionable requests.");
                    continue;
                }

                var mensajeCompletado = true;
                var tuvoCoincidenciaUtil = false;
                var bloqueoSafeLab = false;
'@
$listener = Replace-Once -Text $listener -Old $zeroOld -New $zeroNew -Description 'zero request durable terminal ACK'

$usefulOld = @'
                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);
'@
$usefulNew = @'
                    tuvoCoincidenciaUtil = true;
                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);
'@
$listener = Replace-Once -Text $listener -Old $usefulOld -New $usefulNew -Description 'track useful match'

$safeLabOld = @'
                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)
                    {
                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");
                        continue;
                    }
'@
$safeLabNew = @'
                    if (RadarSettings.ModoSeguroLab && !simularFailOnce)
                    {
                        bloqueoSafeLab = true;
                        Console.WriteLine("  [SAFE LAB] Useful match confirmed; WhatsApp delivery blocked by safe mode.");
                        continue;
                    }
'@
$listener = Replace-Once -Text $listener -Old $safeLabOld -New $safeLabNew -Description 'track safe lab terminal disposition'

$terminalOld = @'
                if (mensajeCompletado)
                {
                    idsConocidos.Add(messageId);
                    var prefijo = messageId + ":";
                    enviosConfirmadosPorSolicitud.RemoveWhere(
                        x => x.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"  [ACK] {messageId}: terminal processing completed.");
                }
                else
                {
                    Console.WriteLine($"  [PENDING] {messageId}: not acknowledged; retry remains enabled.");
                }
'@
$terminalNew = @'
                if (mensajeCompletado)
                {
                    string disposicion = bloqueoSafeLab
                        ? "SAFE_LAB_BLOQUEADO"
                        : tuvoCoincidenciaUtil
                            ? "ALERTA_ENTREGADA"
                            : "SIN_COINCIDENCIA_UTIL";

                    if (!await ConfirmarAckTerminalAsync(chatOrigen, messageId, disposicion))
                    {
                        Console.WriteLine($"  [PENDING] {messageId}: downstream finished but durable terminal ACK is still pending.");
                        continue;
                    }

                    idsConocidos.Add(messageId);
                    var prefijo = messageId + ":";
                    enviosConfirmadosPorSolicitud.RemoveWhere(
                        x => x.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"  [ACK DURABLE] {messageId}: terminal workflow completed ({disposicion}).");
                }
                else
                {
                    Console.WriteLine($"  [PENDING] {messageId}: not acknowledged; retry remains enabled.");
                }
'@
$listener = Replace-Once -Text $listener -Old $terminalOld -New $terminalNew -Description 'durable terminal ACK before local ACK'

$processingRecoveryCallOld = @'
            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                new[] { pendiente.MessageId },
                interpretacion.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
'@
$processingRecoveryCallNew = @'
            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                pendiente.ChatOrigen,
                new[] { pendiente.MessageId },
                interpretacion.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
'@
$listener = Replace-Once -Text $listener -Old $processingRecoveryCallOld -New $processingRecoveryCallNew -Description 'processing recovery chat origin argument'

$deliveryFunctionMarker = 'static async Task RecuperarEntregasDurablesPendientesAsync('
$workflowFunctions = @'
static async Task<bool> ConfirmarAckTerminalAsync(
    string chatOrigen,
    string messageId,
    string disposicion)
{
    if (!RadarCentralIntelligenceClient.Habilitada)
        return true;

    RadarTerminalAckClientResult ack = await RadarWorkflowClient.ConfirmarTerminalAsync(
        chatOrigen,
        messageId,
        disposicion);

    if (ack.Ok)
        return true;

    Console.WriteLine($"  [ACK DURABLE PENDING] {messageId}: {ack.Detalle}");
    return false;
}

static async Task RecuperarWorkflowsDurablesPendientesAsync(
    IPage page,
    Dictionary<string, HashSet<string>> idsConocidosPorChat,
    HashSet<string> enviosConfirmadosPorSolicitud,
    HashSet<string> entregasLabFalladasUnaVez)
{
    if (!RadarCentralIntelligenceClient.Habilitada)
        return;

    RadarPendingWorkflowClientResult consulta = await RadarWorkflowClient.ListarPendientesAsync();
    if (!consulta.Ok)
    {
        Console.WriteLine($"  [WORKFLOW RECOVERY] Could not query completed messages with pending downstream work: {consulta.Detalle}");
        return;
    }

    if (consulta.Items.Count == 0)
        return;

    Console.WriteLine(
        $"  [WORKFLOW RECOVERY] {consulta.Items.Count} completed central message(s) still require terminal downstream ACK.");

    foreach (RadarPendingWorkflowClientItem pendiente in consulta.Items)
    {
        try
        {
            RadarInterpretationResult? interpretacion = JsonSerializer.Deserialize<RadarInterpretationResult>(
                pendiente.ResultadoCentralJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (interpretacion is null)
            {
                Console.WriteLine(
                    $"  [WORKFLOW RECOVERY PENDING] {pendiente.MessageId}: persisted central result could not be reconstructed.");
                continue;
            }

            Console.WriteLine(
                $"  [WORKFLOW RECOVERY] {pendiente.MessageId}: resuming downstream from persisted central result ({interpretacion.Solicitudes.Count} request(s)).");

            if (!idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsConocidos))
            {
                idsConocidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                idsConocidosPorChat[pendiente.ChatOrigen] = idsConocidos;
            }

            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                pendiente.ChatOrigen,
                new[] { pendiente.MessageId },
                interpretacion.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"  [WORKFLOW RECOVERY PENDING] {pendiente.MessageId}: {ex.Message}");
        }
    }
}

'@
$listener = Replace-Once -Text $listener -Old $deliveryFunctionMarker -New ($workflowFunctions + $deliveryFunctionMarker) -Description 'workflow recovery functions'

$uncertainAckOld = @'
            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
            if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsReconciliados))
                idsReconciliados.Add(pendiente.MessageId);

            Console.WriteLine(
                $"  [RECOVERY RECONCILED] Delivery #{pendiente.IdRadarMessageDelivery} already exists in WhatsApp; duplicate send skipped and RSMaps marked ENVIADO.");
            Console.WriteLine(
                $"  [RECOVERY ACK] {pendiente.MessageId}: uncertain external send reconciled.");
            continue;
'@
$uncertainAckNew = @'
            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);

            Console.WriteLine(
                $"  [RECOVERY RECONCILED] Delivery #{pendiente.IdRadarMessageDelivery} already exists in WhatsApp; duplicate send skipped and RSMaps marked ENVIADO.");
            Console.WriteLine(
                $"  [RECOVERY DELIVERY] {pendiente.MessageId}: delivery reconciled; message-level terminal ACK will be evaluated separately.");
            continue;
'@
$listener = Replace-Once -Text $listener -Old $uncertainAckOld -New $uncertainAckNew -Description 'delivery reconciliation no longer ACKs whole message'

$alreadySentOld = @'
        if (preparada.YaEnviado)
        {
            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
            if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsEnviados))
                idsEnviados.Add(pendiente.MessageId);

            Console.WriteLine(
                $"  [RECOVERY DEDUP] Delivery #{preparada.IdRadarMessageDelivery} was already ENVIADO; duplicate skipped.");
            continue;
        }
'@
$alreadySentNew = @'
        if (preparada.YaEnviado)
        {
            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);

            Console.WriteLine(
                $"  [RECOVERY DEDUP] Delivery #{preparada.IdRadarMessageDelivery} was already ENVIADO; duplicate skipped.");
            continue;
        }
'@
$listener = Replace-Once -Text $listener -Old $alreadySentOld -New $alreadySentNew -Description 'already sent delivery does not ACK whole message'

$recoveredSentOld = @'
        entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
        if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsRecuperados))
            idsRecuperados.Add(pendiente.MessageId);

        Console.WriteLine(
            $"  [RECOVERY SENT] Durable delivery #{preparada.IdRadarMessageDelivery} confirmed ENVIADO after restart recovery.");
        Console.WriteLine(
            $"  [RECOVERY ACK] {pendiente.MessageId}: durable pending delivery recovered.");
'@
$recoveredSentNew = @'
        entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);

        Console.WriteLine(
            $"  [RECOVERY SENT] Durable delivery #{preparada.IdRadarMessageDelivery} confirmed ENVIADO after restart recovery.");
        Console.WriteLine(
            $"  [RECOVERY DELIVERY] {pendiente.MessageId}: delivery recovered; message-level terminal ACK will be evaluated separately.");
'@
$listener = Replace-Once -Text $listener -Old $recoveredSentOld -New $recoveredSentNew -Description 'recovered delivery does not ACK whole message'

$testOld = @'
        // Demand messages are only acknowledged after all terminal downstream work finishes.
        var interpretacion = await interpreter.InterpretarAsync(radarMessage);
        demandasInterpretadas.Add(id);
'@
$testNew = @'
        // Demand messages are only acknowledged after all terminal downstream work finishes.
        var interpretacion = await interpreter.InterpretarAsync(radarMessage);

        string? workflowTest = Environment.GetEnvironmentVariable("RADAR_WORKFLOW_TEST")?.Trim();
        if (RadarCentralIntelligenceClient.Habilitada &&
            string.Equals(workflowTest, "crash-after-central", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "  [WORKFLOW TEST] Central processing completed; terminating Agent before downstream work starts.");
            Environment.Exit(87);
        }

        demandasInterpretadas.Add(id);
'@
$listener = Replace-Once -Text $listener -Old $testOld -New $testNew -Description 'CENTRAL 011B crash-after-central test hook'

Write-Utf8NoBom -Path $listenerProgramPath -Content $listener

Write-Host 'RADAR terminal workflow ACK patch applied.'
Write-Host 'Created:'
Write-Host '  sql/RSMaps2/45_radar_terminal_workflow_ack.sql'
Write-Host '  RSMaps.Radar.Listener/Services/RadarWorkflowClient.cs'
Write-Host 'Modified:'
Write-Host '  Repositorios/Contrato/IRadarMessageProcessingRepository.cs'
Write-Host '  Repositorios/Implementacion/RadarMessageProcessingRepository.cs'
Write-Host '  Models/RadarPendingProcessingModels.cs'
Write-Host '  Repositorios/Contrato/IRadarPendingProcessingRepository.cs'
Write-Host '  Repositorios/Implementacion/RadarPendingProcessingRepository.cs'
Write-Host '  Controllers/RadarProcessingRecoveryController.cs'
Write-Host '  RSMaps.Radar.Listener/Program.cs'
Write-Host ''
Write-Host 'Next: build maps4.csproj and RSMaps.Radar.Listener.csproj. Do not run A or B before applying SQL step 45.'
