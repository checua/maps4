param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Insert-Before-Once {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Anchor,
        [Parameter(Mandatory = $true)][string]$Insertion,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $first = $Text.IndexOf($Anchor, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Could not locate patch anchor: $Description"
    }

    $second = $Text.IndexOf($Anchor, $first + $Anchor.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Patch anchor is not unique: $Description"
    }

    return $Text.Substring(0, $first) + $Insertion + $Text.Substring($first)
}

$openAiPath = Join-Path $RepoRoot 'RSMaps.Radar.Intelligence\Services\OpenAiRadarInterpreter.cs'
$normalizerPath = Join-Path $RepoRoot 'RSMaps.Radar.Intelligence\Services\RadarInterpretationNormalizer.cs'

$targets = @(
    'RSMaps.Radar.Intelligence/Services/OpenAiRadarInterpreter.cs',
    'RSMaps.Radar.Intelligence/Services/RadarInterpretationNormalizer.cs'
)

$status = & git -C $RepoRoot status --porcelain -- @targets
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect git status.'
}
if ($status) {
    throw "Target RADAR files already have local changes. Commit or revert them before applying this helper.`n$status"
}

if (-not (Test-Path -LiteralPath $openAiPath)) {
    throw "File not found: $openAiPath"
}
if (-not (Test-Path -LiteralPath $normalizerPath)) {
    throw "File not found: $normalizerPath"
}

$openAi = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $openAiPath))
$normalizer = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $normalizerPath))

if ($openAi.Contains('con cochera') -and $normalizer.Contains('CocheraMinAutos = 1;')) {
    Write-Host 'RADAR explicit cochera requirement fix is already present.'
    exit 0
}

$newLineOpenAi = if ($openAi.Contains("`r`n")) { "`r`n" } else { "`n" }
$newLineNormalizer = if ($normalizer.Contains("`r`n")) { "`r`n" } else { "`n" }

$instructionAnchor = '- La confianza debe estar entre 0 y 1.'
$instruction = '- "con cochera", "con garage" o "con garaje" sin cantidad significa cocheraMinAutos=1; si indica "cochera/garage/garaje para N autos", usa N. No tomes cochera de una respuesta/oferta citada como requisito del solicitante.' + $newLineOpenAi
$openAi = Insert-Before-Once -Text $openAi -Anchor $instructionAnchor -Insertion $instruction -Description 'OpenAI cochera instruction'

$normalizerAnchor = '        if (string.IsNullOrWhiteSpace(solicitud.Operacion))'
$cocheraBlockLines = @(
    '        if (!solicitud.CocheraMinAutos.HasValue)',
    '        {',
    '            var textoSolicitud = texto;',
    '            var inicioRespuesta = BuscarInicioRespuestaOferta(textoSolicitud);',
    '            if (inicioRespuesta >= 0)',
    '                textoSolicitud = textoSolicitud[..inicioRespuesta].Trim();',
    '',
    '            Match cantidadCochera = Regex.Match(',
    '                textoSolicitud,',
    '                @"\b(?:cochera|garage|garaje)\s+(?:para\s+)?(?<autos>\d+)\s*(?:autos?|carros?|vehiculos?)\b",',
    '                RegexOptions.IgnoreCase);',
    '',
    '            if (cantidadCochera.Success &&',
    '                int.TryParse(cantidadCochera.Groups["autos"].Value, out int autos) &&',
    '                autos > 0)',
    '            {',
    '                solicitud.CocheraMinAutos = autos;',
    '            }',
    '            else if (Regex.IsMatch(',
    '                textoSolicitud,',
    '                @"\b(?:cochera|garage|garaje)\s+doble\b",',
    '                RegexOptions.IgnoreCase))',
    '            {',
    '                solicitud.CocheraMinAutos = 2;',
    '            }',
    '            else',
    '            {',
    '                bool cocheraNegada = Regex.IsMatch(',
    '                    textoSolicitud,',
    '                    @"\b(?:sin\s+(?:cochera|garage|garaje)|no\s+(?:requiere|necesita|ocupa)\s+(?:cochera|garage|garaje))\b",',
    '                    RegexOptions.IgnoreCase);',
    '                bool conCochera = Regex.IsMatch(',
    '                    textoSolicitud,',
    '                    @"\bcon\s+(?:cochera|garage|garaje)\b",',
    '                    RegexOptions.IgnoreCase);',
    '',
    '                if (conCochera && !cocheraNegada)',
    '                    solicitud.CocheraMinAutos = 1;',
    '            }',
    '        }',
    ''
)
$cocheraBlock = [string]::Join($newLineNormalizer, $cocheraBlockLines) + $newLineNormalizer
$normalizer = Insert-Before-Once -Text $normalizer -Anchor $normalizerAnchor -Insertion $cocheraBlock -Description 'deterministic explicit cochera recovery'

Write-Utf8NoBom -Path $openAiPath -Content $openAi
Write-Utf8NoBom -Path $normalizerPath -Content $normalizer

Write-Host 'RADAR explicit cochera requirement fix applied.'
Write-Host 'Changed:'
Write-Host '  RSMaps.Radar.Intelligence/Services/OpenAiRadarInterpreter.cs'
Write-Host '  RSMaps.Radar.Intelligence/Services/RadarInterpretationNormalizer.cs'
Write-Host 'Behavior:'
Write-Host '  "con cochera" / "con garage" / "con garaje" => CocheraMinAutos = 1 when the model omitted it.'
Write-Host '  Explicit numeric capacity and "cochera doble" are preserved as stronger minimums.'
Write-Host '  Offer/response text after known response markers is excluded from deterministic recovery.'
