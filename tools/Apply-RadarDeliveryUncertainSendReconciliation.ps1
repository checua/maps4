param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$programPath = Join-Path $RepoRoot 'RSMaps.Radar.Listener\Program.cs'
if (-not (Test-Path -LiteralPath $programPath)) {
    throw "Listener Program.cs not found: $programPath"
}

$resolved = Resolve-Path -LiteralPath $programPath
$content = [System.IO.File]::ReadAllText($resolved)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# 1. Build one deterministic payload per delivery and persist/send that exact text.
$claveAnchor = '                    var claveEntrega = ClaveEntrega(messageId, indice, solicitud);'
$payloadLine = '                    var payloadEntrega = ConstruirAlerta(solicitud) + Environment.NewLine + Environment.NewLine + MarcaEntrega(claveEntrega);'
if (-not $content.Contains($payloadLine)) {
    if (-not $content.Contains($claveAnchor)) {
        throw 'Could not locate claveEntrega anchor.'
    }
    $content = $content.Replace(
        $claveAnchor,
        $claveAnchor + [Environment]::NewLine + $payloadLine)
}

$oldPreparePayload = '                            ConstruirAlerta(solicitud));'
$newPreparePayload = '                            payloadEntrega);'
if ($content.Contains($oldPreparePayload)) {
    $content = $content.Replace($oldPreparePayload, $newPreparePayload)
}
elseif (-not $content.Contains($newPreparePayload)) {
    throw 'Could not locate durable prepare payload argument.'
}

$oldLiveSend = '                        envio = await EnviarAlerta(page, solicitud);'
$newLiveSend = '                        envio = await EnviarAlertaPayload(page, payloadEntrega, solicitud.ChatOrigen);'
if ($content.Contains($oldLiveSend)) {
    $content = $content.Replace($oldLiveSend, $newLiveSend)
}
elseif (-not $content.Contains($newLiveSend)) {
    throw 'Could not locate live alert send call.'
}

# 2. Add the deterministic visible marker used to reconcile an uncertain external send.
$claveFunction = @'
static string ClaveEntrega(string messageId, int indice, SolicitudInmobiliaria solicitud) =>
    $"{messageId}:{indice}:{solicitud.IdInmuebleCoincidente?.ToString() ?? "-"}";
'@
$claveWithMarker = @'
static string ClaveEntrega(string messageId, int indice, SolicitudInmobiliaria solicitud) =>
    $"{messageId}:{indice}:{solicitud.IdInmuebleCoincidente?.ToString() ?? "-"}";

static string MarcaEntrega(string claveEntrega) =>
    $"RADAR-DELIVERY:{claveEntrega}";
'@
if (-not $content.Contains('static string MarcaEntrega(string claveEntrega)')) {
    if (-not $content.Contains($claveFunction)) {
        throw 'Could not locate ClaveEntrega function.'
    }
    $content = $content.Replace($claveFunction, $claveWithMarker)
}

# 3. Before resending recovered work, inspect the destination chat for the marker.
$recoveryAnchor = @'
        RadarDeliveryPrepareClientResult preparada = await RadarDeliveryClient.PrepararAsync(
            pendiente.ChatOrigen,
            pendiente.MessageId,
            pendiente.SolicitudIndice,
            pendiente.ClaveEntrega,
            pendiente.IdInmueble,
            pendiente.Puntuacion.HasValue ? (double?)pendiente.Puntuacion.Value : null,
            pendiente.PayloadAlerta);
'@
$recoveryBlock = @'
        // The previous Agent process may have pressed Enter successfully and crashed
        // before RSMaps received the ENVIADO confirmation. Reconcile WhatsApp first.
        if (await EntregaYaVisibleEnDestinoAsync(page, pendiente.ClaveEntrega))
        {
            RadarDeliveryCompleteClientResult reconciliada = await RadarDeliveryClient.CompletarAsync(
                pendiente.IdRadarMessageDelivery,
                true,
                null);

            if (!reconciliada.Ok)
            {
                Console.WriteLine(
                    $"  [RECOVERY UNCERTAIN] Delivery #{pendiente.IdRadarMessageDelivery} is visible in WhatsApp but RSMaps confirmation failed: {reconciliada.Detalle}");
                continue;
            }

            entregasPendientesAlArranque.Remove(pendiente.IdRadarMessageDelivery);
            if (idsConocidosPorChat.TryGetValue(pendiente.ChatOrigen, out HashSet<string>? idsReconciliados))
                idsReconciliados.Add(pendiente.MessageId);

            Console.WriteLine(
                $"  [RECOVERY RECONCILED] Delivery #{pendiente.IdRadarMessageDelivery} already exists in WhatsApp; duplicate send skipped and RSMaps marked ENVIADO.");
            Console.WriteLine(
                $"  [RECOVERY ACK] {pendiente.MessageId}: uncertain external send reconciled.");
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
'@
if (-not $content.Contains('[RECOVERY RECONCILED]')) {
    if (-not $content.Contains($recoveryAnchor)) {
        throw 'Could not locate restart recovery prepare block.'
    }
    $content = $content.Replace($recoveryAnchor, $recoveryBlock)
}

# 4. Add the WhatsApp reconciliation helper before EnviarAlerta.
$sendMarker = 'static Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta('
if (-not $content.Contains('static async Task<bool> EntregaYaVisibleEnDestinoAsync(')) {
    $index = $content.IndexOf($sendMarker, [StringComparison]::Ordinal)
    if ($index -lt 0) {
        throw 'Could not locate EnviarAlerta insertion point.'
    }

$reconcileFunction = @'
static async Task<bool> EntregaYaVisibleEnDestinoAsync(
    IPage page,
    string claveEntrega)
{
    try
    {
        if (!await AbrirChat(page, AlertSettings.ChatDestino))
            return false;

        await Task.Delay(500);
        string marca = MarcaEntrega(claveEntrega);
        var mensajes = page.Locator("[data-testid^='conv-msg-'][data-id]");
        int total = await mensajes.CountAsync();
        int inicio = Math.Max(0, total - 60);

        for (int i = total - 1; i >= inicio; i--)
        {
            try
            {
                string texto = await mensajes.Nth(i).InnerTextAsync();
                if (texto.Contains(marca, StringComparison.Ordinal))
                    return true;
            }
            catch { }
        }

        return false;
    }
    catch
    {
        // A reconciliation failure is not treated as proof of absence.
        // The normal recovery path remains responsible for the pending delivery.
        return false;
    }
}

'@
    $content = $content.Insert($index, $reconcileFunction)
}

[System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)

Write-Host 'RADAR uncertain-send reconciliation applied successfully.'
Write-Host 'Each real alert now carries a deterministic RADAR-DELIVERY marker.'
Write-Host 'Restart recovery checks WhatsApp for that marker before resending pending work.'
Write-Host 'If the prior send is already visible, RSMaps is reconciled to ENVIADO without a duplicate send.'
Write-Host 'No SQL migration is required.'
