param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Update-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object[]]$Replacements
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "No existe el archivo esperado: $Path"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $original = $text

    foreach ($replacement in $Replacements) {
        $old = [string]$replacement.Old
        $new = [string]$replacement.New
        $expected = [int]$replacement.Count

        $actual = ([regex]::Matches($text, [regex]::Escape($old))).Count
        if ($actual -ne $expected) {
            throw "Proteccion: '$old' aparece $actual veces en $Path; se esperaban $expected. No se modifica el archivo."
        }

        $text = $text.Replace($old, $new)
    }

    if ($text -eq $original) {
        throw "No hubo cambios en $Path."
    }

    [System.IO.File]::WriteAllText($Path, $text, $utf8NoBom)
    Write-Host "OK  $Path"
}

$controller = Join-Path $RepoRoot 'Controllers\BorradorController.cs'
$view = Join-Path $RepoRoot 'Views\Borrador\Editar.cshtml'
$compat = Join-Path $RepoRoot 'Controllers\ModernImageCompatibilityController.cs'

Update-Utf8File -Path $controller -Replacements @(
    @{ Old = 'if (actuales.Count >= 20)'; New = 'if (actuales.Count >= 40)'; Count = 1 },
    @{ Old = 'return BadRequest(new { success = false, message = "El borrador ya tiene el máximo de 20 fotos." });'; New = 'return BadRequest(new { success = false, message = "La propiedad ya tiene el máximo de 40 fotos." });'; Count = 1 },
    @{ Old = '53230 => "La propiedad ya tiene el m\u00e1ximo de 20 fotos.",'; New = '53230 => "La propiedad ya tiene el m\u00e1ximo de 40 fotos.",'; Count = 1 }
)

Update-Utf8File -Path $view -Replacements @(
    @{ Old = '@Model.Fotos.Count/20'; New = '@Model.Fotos.Count/40'; Count = 1 },
    @{ Old = 'Model.Fotos.Count >= 20'; New = 'Model.Fotos.Count >= 40'; Count = 2 },
    @{ Old = 'const maxPhotos = 20;'; New = 'const maxPhotos = 40;'; Count = 1 },
    @{ Old = "alert('Este borrador ya tiene 20 fotos.');"; New = "alert('Esta propiedad ya tiene 40 fotos.');"; Count = 1 }
)

Update-Utf8File -Path $compat -Replacements @(
    @{ Old = 'orden > 20'; New = 'orden > 40'; Count = 1 }
)

Write-Host ''
Write-Host 'Verificacion de referencias activas al limite de fotos:'
$targets = @($controller, $view, $compat)
foreach ($target in $targets) {
    Select-String -Path $target -Pattern '20 fotos|/20|>= 20|> 20|maxPhotos = 20|40 fotos|/40|>= 40|> 40|maxPhotos = 40' |
        ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }
}

Write-Host ''
Write-Host 'OK - Codigo web preparado para permitir hasta 40 fotos por propiedad.'
Write-Host 'El script no ejecuta SQL, no hace commit y no toca ningun proceso en ejecucion.'
