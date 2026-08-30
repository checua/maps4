param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$rootProgramPath = Join-Path $RepoRoot 'Program.cs'
$listenerProgramPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
$serverModelPath = Join-Path $RepoRoot 'Models\RadarPendingDeliveryModels.cs'
$serverInterfacePath = Join-Path $RepoRoot 'Repositorios\Contrato\IRadarPendingDeliveryRepository.cs'
$serverRepositoryPath = Join-Path $RepoRoot 'Repositorios\Implementacion\RadarPendingDeliveryRepository.cs'
$serverControllerPath = Join-Path $RepoRoot 'Controllers\RadarDeliveryRecoveryController.cs'
$listenerRecoveryClientPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Services\RadarDeliveryRecoveryClient.cs'

foreach ($required in @($rootProgramPath, $listenerProgramPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required file not found: $required"
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

# -----------------------------------------------------------------------------
# 1. Server: model returned to the authenticated Agent for pending deliveries.
# -----------------------------------------------------------------------------
$serverModel = @'
namespace maps4.Models;

public sealed class RadarPendingDeliveryItem
{
    public long IdRadarMessageDelivery { get; init; }
    public string ChatOrigen { get; init; } = "";
    public string MessageId { get; init; } = "";
    public int SolicitudIndice { get; init; }
    public string ClaveEntrega { get; init; } = "";
    public int? IdInmueble { get; init; }
    public int? Puntuacion { get; init; }
    public string Estado { get; init; } = "";
    public int IntentosEntrega { get; init; }
    public string? PayloadAlerta { get; init; }
}
'@
Write-Utf8NoBom $serverModelPath $serverModel

# -----------------------------------------------------------------------------
# 2. Server: repository contract and implementation.
# -----------------------------------------------------------------------------
$serverInterface = @'
using maps4.Models;

namespace maps4.Repositorios.Contrato;

public interface IRadarPendingDeliveryRepository
{
    Task<IReadOnlyList<RadarPendingDeliveryItem>> ListarAsync(
        Guid idAgent,
        int maxResultados,
        CancellationToken cancellationToken = default);
}
'@
Write-Utf8NoBom $serverInterfacePath $serverInterface

$serverRepository = @'
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;
using System.Data;

namespace maps4.Repositorios.Implementacion;

public sealed class RadarPendingDeliveryRepository : IRadarPendingDeliveryRepository
{
    private readonly string _cadenaSQL;

    public RadarPendingDeliveryRepository(IConfiguration configuration)
    {
        _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
    }

    public async Task<IReadOnlyList<RadarPendingDeliveryItem>> ListarAsync(
        Guid idAgent,
        int maxResultados,
        CancellationToken cancellationToken = default)
    {
        if (idAgent == Guid.Empty)
            throw new ArgumentException("Se requiere un IdAgent válido.", nameof(idAgent));

        int max = Math.Clamp(maxResultados, 1, 100);
        var items = new List<RadarPendingDeliveryItem>();

        const string sql = @"
SELECT TOP (@max)
    d.IdRadarMessageDelivery,
    p.ChatOrigen,
    p.MessageId,
    d.SolicitudIndice,
    d.ClaveEntrega,
    d.IdInmueble,
    d.Puntuacion,
    d.Estado,
    d.IntentosEntrega,
    d.PayloadAlerta
FROM dbo.RSMAPS_RadarMessageDelivery d
INNER JOIN dbo.RSMAPS_RadarMessageProcessing p
    ON p.IdRadarMessageProcessing = d.IdRadarMessageProcessing
WHERE p.IdAgent = @idAgent
  AND p.Estado = N'COMPLETADO'
  AND d.Estado IN (N'PENDIENTE', N'FALLIDO_REINTENTABLE')
ORDER BY
    COALESCE(d.UltimoIntentoUtc, d.CreadoUtc),
    d.IdRadarMessageDelivery;";

        await using SqlConnection conexion = new(_cadenaSQL);
        await conexion.OpenAsync(cancellationToken);
        await using SqlCommand cmd = new(sql, conexion);
        cmd.Parameters.Add("@max", SqlDbType.Int).Value = max;
        cmd.Parameters.Add("@idAgent", SqlDbType.UniqueIdentifier).Value = idAgent;

        await using SqlDataReader dr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            items.Add(new RadarPendingDeliveryItem
            {
                IdRadarMessageDelivery = Convert.ToInt64(dr["IdRadarMessageDelivery"]),
                ChatOrigen = dr["ChatOrigen"].ToString() ?? string.Empty,
                MessageId = dr["MessageId"].ToString() ?? string.Empty,
                SolicitudIndice = Convert.ToInt32(dr["SolicitudIndice"]),
                ClaveEntrega = dr["ClaveEntrega"].ToString() ?? string.Empty,
                IdInmueble = dr["IdInmueble"] == DBNull.Value ? null : Convert.ToInt32(dr["IdInmueble"]),
                Puntuacion = dr["Puntuacion"] == DBNull.Value ? null : Convert.ToInt32(dr["Puntuacion"]),
                Estado = dr["Estado"].ToString() ?? string.Empty,
                IntentosEntrega = Convert.ToInt32(dr["IntentosEntrega"]),
                PayloadAlerta = dr["PayloadAlerta"] == DBNull.Value ? null : dr["PayloadAlerta"].ToString()
            });
        }

        return items;
    }
}
'@
Write-Utf8NoBom $serverRepositoryPath $serverRepository

# -----------------------------------------------------------------------------
# 3. Server: authenticated pending-delivery endpoint.
# -----------------------------------------------------------------------------
$serverController = @'
using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[ApiController]
[Route("api/radar/delivery")]
public sealed class RadarDeliveryRecoveryController : ControllerBase
{
    private readonly IRadarAgentPairingRepository _pairingRepository;
    private readonly IRadarPendingDeliveryRepository _pendingRepository;

    public RadarDeliveryRecoveryController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarPendingDeliveryRepository pendingRepository)
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
            return Unauthorized(new { mensaje = "RADAR Agent no válido, revocado o sin cuenta activa." });

        IReadOnlyList<RadarPendingDeliveryItem> items = await _pendingRepository.ListarAsync(
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
Write-Utf8NoBom $serverControllerPath $serverController

# Register the recovery repository in the web application.
$rootProgram = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $rootProgramPath))
$registration = 'builder.Services.AddScoped<IRadarPendingDeliveryRepository, RadarPendingDeliveryRepository>();'
if (-not $rootProgram.Contains($registration)) {
    $anchor = 'builder.Services.AddScoped<IRadarMessageDeliveryRepository, RadarMessageDeliveryRepository>();'
    if (-not $rootProgram.Contains($anchor)) {
        throw 'Could not locate IRadarMessageDeliveryRepository registration in root Program.cs.'
    }

    $rootProgram = $rootProgram.Replace(
        $anchor,
        $anchor + [Environment]::NewLine + $registration)
    [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $rootProgramPath), $rootProgram, $utf8NoBom)
}

# -----------------------------------------------------------------------------
# 4. Agent: client for the authenticated pending-delivery endpoint.
# -----------------------------------------------------------------------------
$listenerRecoveryClient = @'
using RSMaps.Radar.Listener.Config;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarPendingDeliveryClientItem
{
    public long IdRadarMessageDelivery { get; init; }
    public string ChatOrigen { get; init; } = "";
    public string MessageId { get; init; } = "";
    public int SolicitudIndice { get; init; }
    public string ClaveEntrega { get; init; } = "";
    public int? IdInmueble { get; init; }
    public int? Puntuacion { get; init; }
    public string Estado { get; init; } = "";
    public int IntentosEntrega { get; init; }
    public string? PayloadAlerta { get; init; }
}

public sealed class RadarPendingDeliveryClientResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<RadarPendingDeliveryClientItem> Items { get; init; } = [];
    public string Detalle { get; init; } = "";
}

public static class RadarDeliveryRecoveryClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<RadarPendingDeliveryClientResult> ListarPendientesAsync(
        int max = 20,
        CancellationToken cancellationToken = default)
    {
        RadarAgentConfig? config = RadarSettings.ConfiguracionAgente;
        if (!RadarAgentCredentialStore.TryLeerToken(config, out string token, out string detalle))
        {
            return new RadarPendingDeliveryClientResult
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
                $"{RadarAgentBackendClient.BaseUrl}/api/radar/delivery/agent/pending?max={limite}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RadarPendingDeliveryClientResult
                {
                    Ok = false,
                    Detalle = $"HTTP {(int)response.StatusCode}: {Recortar(errorBody, 220)}"
                };
            }

            List<RadarPendingDeliveryClientItem>? items = await response.Content
                .ReadFromJsonAsync<List<RadarPendingDeliveryClientItem>>(cancellationToken: cancellationToken);

            return new RadarPendingDeliveryClientResult
            {
                Ok = true,
                Items = items ?? [],
                Detalle = "OK"
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RadarPendingDeliveryClientResult
            {
                Ok = false,
                Detalle = "Pending durable delivery query timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new RadarPendingDeliveryClientResult
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
        texto = (texto ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "...";
    }
}
'@
Write-Utf8NoBom $listenerRecoveryClientPath $listenerRecoveryClient

# -----------------------------------------------------------------------------
# 5. Agent Program.cs: durable restart recovery and exact payload persistence.
# -----------------------------------------------------------------------------
$listenerProgramResolved = Resolve-Path -LiteralPath $listenerProgramPath
$listenerProgram = [System.IO.File]::ReadAllText($listenerProgramResolved)

# State used to distinguish pending work that already existed when this Agent process started
# from pending work created by the live scanner during the current process.
$stateAnchor = 'var entregasLabFalladasUnaVez = new HashSet<string>(StringComparer.OrdinalIgnoreCase);'
$stateBlock = @'
var entregasLabFalladasUnaVez = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// Durable deliveries found on the first server query belong to a previous Agent process.
// They remain eligible for recovery until they are confirmed ENVIADO.
var entregasPendientesAlArranque = new HashSet<long>();
var capturarPendientesAlArranque = true;
'@
if (-not $listenerProgram.Contains('var entregasPendientesAlArranque = new HashSet<long>();')) {
    if (-not $listenerProgram.Contains($stateAnchor)) {
        throw 'Could not locate LAB delivery state anchor in Listener Program.cs.'
    }
    $listenerProgram = $listenerProgram.Replace($stateAnchor, $stateBlock)
}

# Recover previous-process deliveries before scanning WhatsApp messages in each cycle.
$loopAnchor = @'
        await RadarWhatsAppChatDiscovery.ActualizarSiCorrespondeAsync(
            page,
            RadarSettings.ConfiguracionAgente);

        // Tomamos una fotografía estable de la configuración para este barrido.
'@
$loopReplacement = @'
        await RadarWhatsAppChatDiscovery.ActualizarSiCorrespondeAsync(
            page,
            RadarSettings.ConfiguracionAgente);

        await RecuperarEntregasDurablesPendientesAsync(
            page,
            idsConocidosPorChat,
            entregasPendientesAlArranque,
            capturarPendientesAlArranque);
        capturarPendientesAlArranque = false;

        // Tomamos una fotografía estable de la configuración para este barrido.
'@
if (-not $listenerProgram.Contains('await RecuperarEntregasDurablesPendientesAsync(')) {
    if (-not $listenerProgram.Contains($loopAnchor)) {
        throw 'Could not locate main-loop recovery insertion point in Listener Program.cs.'
    }
    $listenerProgram = $listenerProgram.Replace($loopAnchor, $loopReplacement)
}

# Persist the exact alert text that would be sent, so restart recovery sends the same payload.
$oldPayload = 'solicitud.MensajeOriginal + Environment.NewLine + Environment.NewLine + solicitud.MatchingResumen);'
$newPayload = 'ConstruirAlerta(solicitud));'
if ($listenerProgram.Contains($oldPayload)) {
    $listenerProgram = $listenerProgram.Replace($oldPayload, $newPayload)
}
elseif (-not $listenerProgram.Contains($newPayload)) {
    throw 'Could not locate durable delivery payload expression in Listener Program.cs.'
}

# Remove the mojibake separator observed in Windows console output.
$listenerProgram = $listenerProgram.Replace(' prepared Â· attempt ', ' prepared - attempt ')
$listenerProgram = $listenerProgram.Replace(' prepared · attempt ', ' prepared - attempt ')

# Replace EnviarAlerta with a payload-capable implementation reused by restart recovery.
$sendStartMarker = 'static async Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta('
$sendEndMarker = 'static async Task<ILocator?> ObtenerCajaMensaje(IPage page)'
$sendStart = $listenerProgram.IndexOf($sendStartMarker, [StringComparison]::Ordinal)
$sendEnd = $listenerProgram.IndexOf($sendEndMarker, [StringComparison]::Ordinal)

if ($sendStart -lt 0 -or $sendEnd -lt 0 -or $sendEnd -le $sendStart) {
    throw 'Could not locate EnviarAlerta function boundaries in Listener Program.cs.'
}

if (-not $listenerProgram.Contains('EnviarAlertaPayload(')) {
$sendBlock = @'
static Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta(
    IPage page,
    SolicitudInmobiliaria s) =>
    EnviarAlertaPayload(page, ConstruirAlerta(s), s.ChatOrigen);

static async Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlertaPayload(
    IPage page,
    string alerta,
    string chatOrigen)
{
    try
    {
        if (!await AbrirChat(page, AlertSettings.ChatDestino))
            return (false, false, "abrir chat destino");

        var compose = await ObtenerCajaMensaje(page);
        if (compose is null)
            return (false, false, "encontrar caja de mensaje");

        await compose.ClickAsync();

        try
        {
            await compose.FillAsync(alerta);
        }
        catch
        {
            await page.Keyboard.InsertTextAsync(alerta);
        }

        await Task.Delay(AlertSettings.EsperaEnvioMs);
        await page.Keyboard.PressAsync("Enter");
        await Task.Delay(700);

        var regresoOrigen = await AbrirChat(page, chatOrigen);
        if (!regresoOrigen)
            return (true, false, "enviado; no pude regresar al chat origen");

        await Task.Delay(300);
        var marcado = await MarcarChatEnListaComoNoLeido(page, AlertSettings.ChatDestino);

        return (
            true,
            marcado,
            marcado ? "ok" : "enviado; no se pudo marcar no leído desde la lista");
    }
    catch (Exception ex)
    {
        return (false, false, ex.Message);
    }
}

'@
    $listenerProgram = $listenerProgram.Substring(0, $sendStart) + $sendBlock + $listenerProgram.Substring($sendEnd)
}

# Add the restart-recovery function before the chat-state reconciliation function.
$recoveryFunctionMarker = 'static async Task RecuperarEntregasDurablesPendientesAsync('
if (-not $listenerProgram.Contains($recoveryFunctionMarker)) {
    $insertMarker = 'static async Task<HashSet<string>> ReconciliarEstadoChatsAsync('
    $insertIndex = $listenerProgram.IndexOf($insertMarker, [StringComparison]::Ordinal)
    if ($insertIndex -lt 0) {
        throw 'Could not locate ReconciliarEstadoChatsAsync insertion point in Listener Program.cs.'
    }

$recoveryFunction = @'
static async Task RecuperarEntregasDurablesPendientesAsync(
    IPage page,
    Dictionary<string, HashSet<string>> idsConocidosPorChat,
    HashSet<long> entregasPendientesAlArranque,
    bool capturarPendientesAlArranque)
{
    if (!RadarDeliveryClient.Habilitada)
        return;

    RadarPendingDeliveryClientResult consulta = await RadarDeliveryRecoveryClient.ListarPendientesAsync();
    if (!consulta.Ok)
    {
        Console.WriteLine($"  [RECOVERY] Could not query durable pending deliveries: {consulta.Detalle}");
        return;
    }

    if (capturarPendientesAlArranque)
    {
        foreach (RadarPendingDeliveryClientItem item in consulta.Items)
            entregasPendientesAlArranque.Add(item.IdRadarMessageDelivery);

        if (entregasPendientesAlArranque.Count > 0)
        {
            Console.WriteLine(
                $"  [RECOVERY] {entregasPendientesAlArranque.Count} durable delivery(ies) from a previous Agent process detected.");
        }
    }

    foreach (RadarPendingDeliveryClientItem pendiente in consulta.Items)
    {
        bool absorbidaComoHistorial =
            idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsChat) &&
            idsChat.Contains(pendiente.MessageId);

        bool perteneceAProcesoAnterior = entregasPendientesAlArranque.Contains(
            pendiente.IdRadarMessageDelivery);

        // Live pending work created by this process remains owned by the ordinary WhatsApp scanner.
        if (!perteneceAProcesoAnterior && !absorbidaComoHistorial)
            continue;

        string? pruebaEntregaLab = Environment.GetEnvironmentVariable(
            "RADAR_SAFE_LAB_DELIVERY_TEST")?.Trim();
        bool simularFailOnce = RadarSettings.ModoSeguroLab &&
            string.Equals(pruebaEntregaLab, "fail-once", StringComparison.OrdinalIgnoreCase);

        if (RadarSettings.ModoSeguroLab && !simularFailOnce)
        {
            Console.WriteLine(
                $"  [RECOVERY SAFE LAB] Delivery #{pendiente.IdRadarMessageDelivery} remains pending; real WhatsApp delivery is blocked.");
            continue;
        }

        if (string.IsNullOrWhiteSpace(pendiente.PayloadAlerta))
        {
            Console.WriteLine(
                $"  [RECOVERY] Delivery #{pendiente.IdRadarMessageDelivery} has no persisted alert payload; it remains pending.");
            continue;
        }

        RadarDeliveryPrepareClientResult preparada = await RadarDeliveryClient.PrepararAsync(
            pendiente.ChatOrigen,
            pendiente.MessageId,
            pendiente.SolicitudIndice,
            pendiente.ClaveEntrega,
            pendiente.IdInmueble,
            pendiente.Puntuacion.HasValue ? (double?)pendiente.Puntuacion.Value : null,
            pendiente.PayloadAlerta);

        if (!preparada.Ok)
        {
            Console.WriteLine(
                $"  [RECOVERY] Delivery #{pendiente.IdRadarMessageDelivery} could not be reclaimed: {preparada.Detalle}");
            continue;
        }

        if (preparada.YaEnviado)
        {
            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
            if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsEnviados))
                idsEnviados.Add(pendiente.MessageId);

            Console.WriteLine(
                $"  [RECOVERY DEDUP] Delivery #{preparada.IdRadarMessageDelivery} was already ENVIADO; duplicate skipped.");
            continue;
        }

        Console.WriteLine(
            $"  [RECOVERY] Durable delivery #{preparada.IdRadarMessageDelivery} resumed - attempt {preparada.IntentosEntrega}.");

        (bool Enviada, bool MarcadoNoLeido, string Detalle) envio;
        if (simularFailOnce)
        {
            // A recovered delivery already had a prior durable attempt. Once it is reclaimed,
            // the attempt number is > 1, so the LAB recovery succeeds even after process memory reset.
            if (preparada.IntentosEntrega <= 1)
            {
                envio = (false, false, "SAFE LAB simulated first delivery failure during recovery");
                Console.WriteLine(
                    "  [SAFE LAB RECOVERY] First durable attempt intentionally failed; no WhatsApp message was sent.");
            }
            else
            {
                envio = (true, true, "SAFE LAB simulated delivery recovery after Agent restart");
                Console.WriteLine(
                    "  [SAFE LAB RECOVERY] Durable retry succeeded after Agent restart; no WhatsApp message was sent.");
            }
        }
        else
        {
            envio = await EnviarAlertaPayload(
                page,
                pendiente.PayloadAlerta,
                pendiente.ChatOrigen);
        }

        RadarDeliveryCompleteClientResult confirmacion = await RadarDeliveryClient.CompletarAsync(
            preparada.IdRadarMessageDelivery,
            envio.Enviada,
            envio.Enviada ? null : envio.Detalle);

        if (!confirmacion.Ok)
        {
            Console.WriteLine(
                $"  [RECOVERY] Delivery #{preparada.IdRadarMessageDelivery} could not persist its result: {confirmacion.Detalle}");
            continue;
        }

        if (!envio.Enviada)
        {
            Console.WriteLine(
                $"  [RECOVERY PENDING] Delivery #{preparada.IdRadarMessageDelivery} failed again: {envio.Detalle}");
            continue;
        }

        entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
        if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsRecuperados))
            idsRecuperados.Add(pendiente.MessageId);

        Console.WriteLine(
            $"  [RECOVERY SENT] Durable delivery #{preparada.IdRadarMessageDelivery} confirmed ENVIADO after restart recovery.");
        Console.WriteLine(
            $"  [RECOVERY ACK] {pendiente.MessageId}: durable pending delivery recovered.");
    }
}

'@
    $listenerProgram = $listenerProgram.Insert($insertIndex, $recoveryFunction)
}

[System.IO.File]::WriteAllText($listenerProgramResolved, $listenerProgram, $utf8NoBom)

Write-Host 'RADAR durable restart recovery patch applied successfully.'
Write-Host 'RSMaps now exposes pending durable deliveries for the authenticated Agent.'
Write-Host 'The Agent can recover deliveries that existed before its current process started.'
Write-Host 'SAFE LAB fail-once recovery succeeds based on durable attempt count, not process memory.'
Write-Host 'The exact alert payload is persisted for future recovery.'
Write-Host 'No SQL migration is required for this patch.'
