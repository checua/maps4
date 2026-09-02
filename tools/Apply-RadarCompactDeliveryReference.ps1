param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)

$pattern = '(?s)static string MarcaEntrega\(string claveEntrega\)\s*=>\s*\$"RADAR-DELIVERY:\{claveEntrega\}";'
$matches = [regex]::Matches($content, $pattern)

if ($matches.Count -ne 1) {
    throw "Expected exactly one MarcaEntrega expression; found $($matches.Count). No file was changed."
}

$replacement = @'
static string MarcaEntrega(string claveEntrega)
{
    // Mantener una referencia determinística dentro del mensaje permite reconciliar
    // un envío incierto después de una caída, sin exponer la clave durable completa.
    var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(claveEntrega));
    var referencia = Convert.ToHexString(hash.AsSpan(0, 8));
    return $"Ref. RADAR: {referencia}";
}
'@

$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$replacement = [regex]::Replace($replacement.Trim(), "\r?\n", $newLine)

$updated = [regex]::Replace($content, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement }, 1)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $updated, $utf8NoBom)

Write-Host "Updated: $ProgramPath"
Write-Host "Delivery marker is now a compact deterministic 64-bit reference."
Write-Host "Next: build and inspect git diff before running RADAR."
