param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProgramPath)) {
    throw "Program.cs not found: $ProgramPath"
}

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)

$oldBlock = @'
static string MostrarRango(int? min, int? max)
{
    if (!min.HasValue && !max.HasValue)
        return "-";

    if (min == max)
        return min?.ToString() ?? "-";

    return $"{min?.ToString() ?? "-"} a {max?.ToString() ?? "-"}";
}
'@

$newBlock = @'
static string MostrarRango(int? min, int? max)
{
    if (!min.HasValue && !max.HasValue)
        return "-";

    if (min.HasValue && max.HasValue)
    {
        if (min.Value == max.Value)
            return min.Value.ToString();

        return $"{min.Value} a {max.Value}";
    }

    if (min.HasValue)
        return $"m\u00EDnimo {min.Value}";

    return $"m\u00E1ximo {max!.Value}";
}
'@

$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$oldBlock = [regex]::Replace($oldBlock.Trim(), "\r?\n", $newLine)
$newBlock = [regex]::Replace($newBlock.Trim(), "\r?\n", $newLine)

$matches = ([regex]::Matches($content, [regex]::Escape($oldBlock))).Count
if ($matches -ne 1) {
    throw "Expected exactly one MostrarRango block; found $matches. No file was changed."
}

$updated = $content.Replace($oldBlock, $newBlock)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $updated, $utf8NoBom)

Write-Host "Updated: $ProgramPath"
Write-Host "Range formatting now renders min-only and max-only values explicitly."
Write-Host "Next: build and inspect git diff before running RADAR."
