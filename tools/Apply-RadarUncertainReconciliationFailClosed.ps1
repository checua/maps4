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

# The previous reconciliation helper returned false both when the marker was absent
# and when WhatsApp could not be inspected. That is unsafe because an inspection
# failure could be treated as permission to resend. Make the result tri-state via
# a tuple: Consultada=false means UNKNOWN and must remain pending.
$oldCall = @'
        if (await EntregaYaVisibleEnDestinoAsync(page, pendiente.ClaveEntrega))
        {
            RadarDeliveryCompleteClientResult reconciliada = await RadarDeliveryClient.CompletarAsync(
'@

$newCall = @'
        var verificacionEntrega = await VerificarEntregaEnDestinoAsync(page, pendiente.ClaveEntrega);
        if (!verificacionEntrega.Consultada)
        {
            Console.WriteLine(
                $"  [RECOVERY UNCERTAIN] Delivery #{pendiente.IdRadarMessageDelivery} could not be verified in WhatsApp; it remains pending and WILL NOT be resent.");
            continue;
        }

        if (verificacionEntrega.Encontrada)
        {
            RadarDeliveryCompleteClientResult reconciliada = await RadarDeliveryClient.CompletarAsync(
'@

if ($content.Contains($oldCall)) {
    $content = $content.Replace($oldCall, $newCall)
}
elseif (-not $content.Contains('var verificacionEntrega = await VerificarEntregaEnDestinoAsync(')) {
    throw 'Could not locate uncertain-send reconciliation call.'
}

$startMarker = 'static async Task<bool> EntregaYaVisibleEnDestinoAsync('
$endMarker = 'static Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta('
$start = $content.IndexOf($startMarker, [StringComparison]::Ordinal)
$end = $content.IndexOf($endMarker, [StringComparison]::Ordinal)

if ($start -ge 0) {
    if ($end -lt 0 -or $end -le $start) {
        throw 'Could not locate reconciliation helper boundaries.'
    }

    $replacement = @'
static async Task<(bool Consultada, bool Encontrada)> VerificarEntregaEnDestinoAsync(
    IPage page,
    string claveEntrega)
{
    try
    {
        if (!await AbrirChat(page, AlertSettings.ChatDestino))
            return (false, false);

        await Task.Delay(500);
        string marca = MarcaEntrega(claveEntrega);
        var mensajes = page.Locator("[data-testid^='conv-msg-'][data-id]");
        int total = await mensajes.CountAsync();
        if (total <= 0)
            return (false, false);

        int inicio = Math.Max(0, total - 60);
        for (int i = total - 1; i >= inicio; i--)
        {
            string texto;
            try
            {
                texto = await mensajes.Nth(i).InnerTextAsync();
            }
            catch
            {
                return (false, false);
            }

            if (texto.Contains(marca, StringComparison.Ordinal))
                return (true, true);
        }

        return (true, false);
    }
    catch
    {
        return (false, false);
    }
}

'@

    $content = $content.Substring(0, $start) + $replacement + $content.Substring($end)
}
elseif (-not $content.Contains('static async Task<(bool Consultada, bool Encontrada)> VerificarEntregaEnDestinoAsync(')) {
    throw 'Could not locate existing reconciliation helper.'
}

# Remove obsolete wrapper that is no longer called after payload-based sending was introduced.
$obsolete = @'
static Task<(bool Enviada, bool MarcadoNoLeido, string Detalle)> EnviarAlerta(
    IPage page,
    SolicitudInmobiliaria s) =>
    EnviarAlertaPayload(page, ConstruirAlerta(s), s.ChatOrigen);

'@
if ($content.Contains($obsolete)) {
    $content = $content.Replace($obsolete, '')
}

[System.IO.File]::WriteAllText($resolved, $content, $utf8NoBom)

Write-Host 'RADAR uncertain-send reconciliation now fails closed.'
Write-Host 'If WhatsApp cannot be inspected, the delivery remains pending and is not resent.'
Write-Host 'A confirmed marker reconciles SQL to ENVIADO without duplicate delivery.'
Write-Host 'The obsolete EnviarAlerta wrapper was removed to clear CS8321.'
