param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)

$oldLine = '        var text = (await message.InnerTextAsync()).Trim();'
$matches = ([regex]::Matches($content, [regex]::Escape($oldLine))).Count
if ($matches -ne 1) {
    throw "Expected exactly one message InnerText assignment; found $matches. No file was changed."
}

$newLine = '        var text = LimpiarTextoMensajeWhatsApp((await message.InnerTextAsync()).Trim());'
$content = $content.Replace($oldLine, $newLine)

$anchor = 'static async Task<(int Revisados, List<SolicitudInmobiliaria> Solicitudes, HashSet<string> DemandasInterpretadas)> ProcesarMensajesNuevos('
$anchorCount = ([regex]::Matches($content, [regex]::Escape($anchor))).Count
if ($anchorCount -ne 1) {
    throw "Expected exactly one ProcesarMensajesNuevos anchor; found $anchorCount. No file was changed."
}

$helper = @'
static string LimpiarTextoMensajeWhatsApp(string texto)
{
    if (string.IsNullOrWhiteSpace(texto))
        return texto;

    // WhatsApp Web suele incluir la hora visible como una última línea independiente
    // dentro del InnerText del mensaje. Se elimina solo si toda la última línea es una hora.
    var lineas = texto
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n');

    if (lineas.Length <= 1)
        return texto.Trim();

    var ultima = lineas[^1].Trim();
    if (!Regex.IsMatch(
            ultima,
            @"^\d{1,2}:\d{2}\s*(?:a\.?\s*m\.?|p\.?\s*m\.?)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    {
        return texto.Trim();
    }

    return string.Join("\n", lineas[..^1]).Trim();
}

'@

$newLineStyle = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$helper = [regex]::Replace($helper, "\r?\n", $newLineStyle)
$content = $content.Replace($anchor, $helper + $anchor)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $content, $utf8NoBom)

Write-Host "Updated: $ProgramPath"
Write-Host "Next: build and inspect git diff before running RADAR."
