param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)

$pattern = '(?s)static string ConstruirAlerta\(SolicitudInmobiliaria s\)\s*\{.*?\n\}(?=\r?\n\r?\nstatic async Task<\(string\? Autor, string\? Telefono\)> ExtraerRemitente)'
$matches = [regex]::Matches($content, $pattern)

if ($matches.Count -ne 1) {
    throw "Expected exactly one ConstruirAlerta block; found $($matches.Count). No file was changed."
}

$replacement = @'
static string ConstruirAlerta(SolicitudInmobiliaria s)
{
    var sb = new StringBuilder();

    string coincidencia = s.MejorCoincidencia.HasValue
        ? $"{Math.Round(s.MejorCoincidencia.Value, MidpointRounding.AwayFromZero):0}%"
        : "confirmada";

    sb.AppendLine($"\U0001F525 RSMAPS RADAR \u00B7 COINCIDENCIA {coincidencia}");
    sb.AppendLine();

    if (!string.IsNullOrWhiteSpace(s.MatchingResumen))
    {
        sb.AppendLine("\u2705 MATCH RSMAPS");

        var lineasMatching = s.MatchingResumen
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.Equals(x, "COINCIDENCIAS RSMAPS", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Candidatos:", StringComparison.OrdinalIgnoreCase));

        foreach (var linea in lineasMatching)
            sb.AppendLine(linea);

        sb.AppendLine();
    }

    sb.AppendLine("\U0001F50E SOLICITUD DETECTADA");

    if (!string.IsNullOrWhiteSpace(s.Operacion))
        sb.AppendLine($"Operaci\u00F3n: {s.Operacion}");

    if (s.TiposPropiedad.Count > 0)
        sb.AppendLine($"Tipo: {MostrarLista(s.TiposPropiedad)}");

    if (s.SubtiposPropiedad.Count > 0)
        sb.AppendLine($"Subtipo: {MostrarLista(s.SubtiposPropiedad)}");

    if (s.Zonas.Count > 0)
        sb.AppendLine($"Zona: {MostrarLista(s.Zonas)}");

    if (s.PrecioMinimo.HasValue)
        sb.AppendLine($"Precio m\u00EDnimo: {MostrarDinero(s.PrecioMinimo)}");

    if (s.PrecioMaximo.HasValue)
        sb.AppendLine($"Precio m\u00E1ximo: {MostrarDinero(s.PrecioMaximo)}");

    if (s.RecamarasMin.HasValue || s.RecamarasMax.HasValue)
        sb.AppendLine($"Rec\u00E1maras: {MostrarRango(s.RecamarasMin, s.RecamarasMax)}");

    if (s.BanosMin.HasValue || s.BanosMax.HasValue)
        sb.AppendLine($"Ba\u00F1os: {MostrarRango(s.BanosMin, s.BanosMax)}");

    if (s.CocheraMinAutos.HasValue)
        sb.AppendLine($"Cochera m\u00EDnima: {s.CocheraMinAutos}");

    if (s.AceptaMascotas.HasValue)
        sb.AppendLine($"Mascotas: {MostrarBooleano(s.AceptaMascotas)}");

    if (s.ModalidadesPago.Count > 0)
        sb.AppendLine($"Pago/cr\u00E9dito: {MostrarLista(s.ModalidadesPago)}");

    sb.AppendLine();
    sb.AppendLine($"\U0001F4CD Origen: {s.ChatOrigen}");

    if (!string.IsNullOrWhiteSpace(s.Autor))
        sb.AppendLine($"Autor: {s.Autor}");

    if (!string.IsNullOrWhiteSpace(s.Telefono))
        sb.AppendLine($"Tel\u00E9fono: {s.Telefono}");

    sb.AppendLine();
    sb.AppendLine("\U0001F4AC MENSAJE ORIGINAL");
    sb.AppendLine(s.MensajeOriginal.Trim());

    return sb.ToString().Trim();
}
'@

$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$replacement = [regex]::Replace($replacement.Trim(), "\r?\n", $newLine)

$m = $matches[0]
$updated = $content.Substring(0, $m.Index) + $replacement + $content.Substring($m.Index + $m.Length)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $updated, $utf8NoBom)

Write-Host "Updated: $ProgramPath"
Write-Host "Next: build and inspect git diff before running RADAR."
