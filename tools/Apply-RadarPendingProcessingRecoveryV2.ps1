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

$expectedDirty = @(
    'Program.cs',
    'RSMaps.Radar.Listener/Program.cs',
    'Controllers/RadarProcessingRecoveryController.cs',
    'Models/RadarPendingProcessingModels.cs',
    'RSMaps.Radar.Listener/Services/RadarProcessingRecoveryClient.cs',
    'Repositorios/Contrato/IRadarPendingProcessingRepository.cs',
    'Repositorios/Implementacion/RadarPendingProcessingRepository.cs'
)

$statusLines = @(& git -C $RepoRoot status --porcelain)
foreach ($line in $statusLines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $path = $line.Substring(3).Trim().Replace('\\','/')
    if ($expectedDirty -notcontains $path) {
        throw "Unexpected local change found: $path. Refusing to reset unrelated work."
    }
}

# Restore only the two tracked files modified by the failed first patch.
& git -C $RepoRoot checkout -- 'Program.cs' 'RSMaps.Radar.Listener/Program.cs'
if ($LASTEXITCODE -ne 0) {
    throw 'Could not restore tracked RADAR files to HEAD.'
}

$modelsPath = Join-Path $RepoRoot 'Models\RadarPendingProcessingModels.cs'
$interfacePath = Join-Path $RepoRoot 'Repositorios\Contrato\IRadarPendingProcessingRepository.cs'
$repositoryPath = Join-Path $RepoRoot 'Repositorios\Implementacion\RadarPendingProcessingRepository.cs'
$controllerPath = Join-Path $RepoRoot 'Controllers\RadarProcessingRecoveryController.cs'
$clientPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Services\RadarProcessingRecoveryClient.cs'
$webProgramPath = Join-Path $RepoRoot 'Program.cs'
$listenerProgramPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'

foreach ($path in @($modelsPath, $interfacePath, $repositoryPath, $controllerPath, $clientPath)) {
    Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
}

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
'@

$interface = @'
using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarPendingProcessingRepository
{
    Task<IReadOnlyList<RadarPendingProcessingItem>> ListarAsync(
        Guid idAgent,
        int max,
        CancellationToken cancellationToken = default);
}
'@

$repository = @'
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

    public RadarProcessingRecoveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarPendingProcessingRepository pendingRepository)
    {
        _pairingRepository = pairingRepository;
        _pendingRepository = pendingRepository;
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

$client = @'
using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingProcessingClientItem
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

public sealed class RadarPendingProcessingClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingProcessingClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = string.Empty;
}

public static class RadarProcessingRecoveryClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingProcessingClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        if (!RadarCentralIntelligenceClient.Habilitada)
        {
            return new RadarPendingProcessingClientResult
            {
                Ok = true,
                Detalle = "Central Intelligence is disabled."
            };
        }

        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingProcessingClientResult
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
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/processing/agent/pending?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingProcessingClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(body, 220)}"
                };
            }

            List<RadarPendingProcessingClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingProcessingClientItem>>(
                    cancellationToken: cancellationToken);

            return new RadarPendingProcessingClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingProcessingClientResult
            {
                Ok = false,
                Detalle = "Pending durable processing query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingProcessingClientResult
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

Write-Utf8NoBom -Path $modelsPath -Content $models
Write-Utf8NoBom -Path $interfacePath -Content $interface
Write-Utf8NoBom -Path $repositoryPath -Content $repository
Write-Utf8NoBom -Path $controllerPath -Content $controller
Write-Utf8NoBom -Path $clientPath -Content $client

$webProgram = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $webProgramPath))
$diAnchor = 'builder.Services.AddScoped<IRadarPendingDeliveryRepository, RadarPendingDeliveryRepository>();'
$diReplacement = $diAnchor + [Environment]::NewLine + 'builder.Services.AddScoped<IRadarPendingProcessingRepository, RadarPendingProcessingRepository>();'
$webProgram = Replace-Once -Text $webProgram -Old $diAnchor -New $diReplacement -Description 'web DI registration'
Write-Utf8NoBom -Path $webProgramPath -Content $webProgram

$listenerProgram = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $listenerProgramPath))

$deliveryRecoveryAnchor = @'
        await RecuperarEntregasDurablesPendientesAsync(
            page,
            idsConocidosPorChat,
            entregasPendientesAlArranque,
            capturarPendientesAlArranque);
'@

$processingRecoveryCall = @'
        await RecuperarProcesamientosDurablesPendientesAsync(
            page,
            idsConocidosPorChat,
            interpreter,
            enviosConfirmadosPorSolicitud,
            entregasLabFalladasUnaVez);

'@

$listenerProgram = Replace-Once -Text $listenerProgram -Old $deliveryRecoveryAnchor -New ($processingRecoveryCall + $deliveryRecoveryAnchor) -Description 'processing recovery call'

$foreachStartMarker = '            foreach (var messageId in resultado.DemandasInterpretadas)'
$foreachTailMarker = @'
                else
                {
                    Console.WriteLine($"  [PENDING] {messageId}: not acknowledged; retry remains enabled.");
                }
            }
'@

$foreachStart = $listenerProgram.IndexOf($foreachStartMarker, [System.StringComparison]::Ordinal)
if ($foreachStart -lt 0) {
    throw 'Could not locate ordinary downstream foreach start.'
}

$foreachTailStart = $listenerProgram.IndexOf($foreachTailMarker, $foreachStart, [System.StringComparison]::Ordinal)
if ($foreachTailStart -lt 0) {
    throw 'Could not locate ordinary downstream foreach tail.'
}

$foreachEnd = $foreachTailStart + $foreachTailMarker.Length
$oldDownstreamBlock = $listenerProgram.Substring($foreachStart, $foreachEnd - $foreachStart)

$sharedCall = @'
            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                resultado.DemandasInterpretadas,
                resultado.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
'@

$listenerProgram = $listenerProgram.Substring(0, $foreachStart) + $sharedCall + $listenerProgram.Substring($foreachEnd)

$sharedFunctionPrefix = @'
static async Task ProcesarDemandasInterpretadasAsync(
    IPage page,
    HashSet<string> idsConocidos,
    IReadOnlyCollection<string> demandasInterpretadas,
    IReadOnlyList<SolicitudInmobiliaria> solicitudes,
    HashSet<string> enviosConfirmadosPorSolicitud,
    HashSet<string> entregasLabFalladasUnaVez)
{
'@

$sharedFunctionSuffix = @'
}

static async Task RecuperarProcesamientosDurablesPendientesAsync(
    IPage page,
    Dictionary<string, HashSet<string>> idsConocidosPorChat,
    IRadarInterpreter interpreter,
    HashSet<string> enviosConfirmadosPorSolicitud,
    HashSet<string> entregasLabFalladasUnaVez)
{
    if (!RadarCentralIntelligenceClient.Habilitada)
        return;

    RadarPendingProcessingClientResult consulta = await RadarProcessingRecoveryClient.ListarPendientesAsync();
    if (!consulta.Ok)
    {
        Console.WriteLine($"  [PROCESSING RECOVERY] Could not query durable pending processing: {consulta.Detalle}");
        return;
    }

    if (consulta.Items.Count == 0)
        return;

    Console.WriteLine(
        $"  [PROCESSING RECOVERY] {consulta.Items.Count} durable message(s) require central processing recovery.");

    foreach (RadarPendingProcessingClientItem pendiente in consulta.Items)
    {
        try
        {
            Console.WriteLine(
                $"  [PROCESSING RECOVERY] Message {pendiente.MessageId} from {pendiente.ChatOrigen} resumes after attempt {pendiente.IntentosProcesamiento}.");

            var radarMessage = new RadarMessage
            {
                MessageId = pendiente.MessageId,
                ChatOrigen = pendiente.ChatOrigen,
                Autor = pendiente.Autor,
                Telefono = pendiente.Telefono,
                TextoOriginal = pendiente.MensajeOriginal,
                DetectadoEn = pendiente.DetectadoUtc.Kind == DateTimeKind.Utc
                    ? pendiente.DetectadoUtc.ToLocalTime()
                    : pendiente.DetectadoUtc
            };

            RadarInterpretationResult interpretacion = await interpreter.InterpretarAsync(radarMessage);

            Console.WriteLine(
                $"  [PROCESSING RECOVERY] {pendiente.MessageId}: central processing recovered with {interpretacion.Solicitudes.Count} request(s). Motor: {interpretacion.Motor}.");

            if (!idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsConocidos))
            {
                idsConocidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                idsConocidosPorChat[pendiente.ChatOrigen] = idsConocidos;
            }

            await ProcesarDemandasInterpretadasAsync(
                page,
                idsConocidos,
                new[] { pendiente.MessageId },
                interpretacion.Solicitudes,
                enviosConfirmadosPorSolicitud,
                entregasLabFalladasUnaVez);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"  [PROCESSING RECOVERY PENDING] {pendiente.MessageId}: {ex.Message}");
        }
    }
}

'@

$sharedDownstream = $oldDownstreamBlock.Replace('resultado.DemandasInterpretadas', 'demandasInterpretadas').Replace('resultado.Solicitudes', 'solicitudes')
$sharedFunction = $sharedFunctionPrefix + $sharedDownstream + [Environment]::NewLine + $sharedFunctionSuffix

$deliveryFunctionMarker = 'static async Task RecuperarEntregasDurablesPendientesAsync('
$deliveryFunctionIndex = $listenerProgram.IndexOf($deliveryFunctionMarker, [System.StringComparison]::Ordinal)
if ($deliveryFunctionIndex -lt 0) {
    throw 'Could not locate durable delivery recovery function.'
}

$listenerProgram = $listenerProgram.Substring(0, $deliveryFunctionIndex) + $sharedFunction + $listenerProgram.Substring($deliveryFunctionIndex)
Write-Utf8NoBom -Path $listenerProgramPath -Content $listenerProgram

Write-Host 'RADAR pending processing recovery V2 applied.'
Write-Host 'The failed V1 local patch was reset before applying V2.'
Write-Host 'Next: build both projects before starting A or B.'
