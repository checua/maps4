param(
    [string]$ProgramPath = ".\RSMaps.Radar.Listener\Program.cs"
)

$ErrorActionPreference = "Stop"

$resolvedProgramPath = Resolve-Path -LiteralPath $ProgramPath
$content = [System.IO.File]::ReadAllText($resolvedProgramPath)
$nl = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

$alreadyPatched =
    $content.Contains("if (string.IsNullOrWhiteSpace(id) || knownIds.Contains(id))") -and
    $content.Contains("var interpretacion = await interpreter.InterpretarAsync(radarMessage);${nl}        knownIds.Add(id);${nl}        revisados++;")

if ($alreadyPatched)
{
    Write-Host "RADAR retry patch is already applied."
    exit 0
}

$oldIdCheck = "if (string.IsNullOrWhiteSpace(id) || !knownIds.Add(id))"
$newIdCheck = "if (string.IsNullOrWhiteSpace(id) || knownIds.Contains(id))"

if (!$content.Contains($oldIdCheck))
{
    throw "Expected knownIds.Add guard was not found. Program.cs may have changed."
}

$content = $content.Replace($oldIdCheck, $newIdCheck)

$oldEarlyReview = "${nl}        revisados++;${nl}${nl}        var text = (await message.InnerTextAsync()).Trim();"
$newEarlyReview = "${nl}${nl}        var text = (await message.InnerTextAsync()).Trim();"

if (!$content.Contains($oldEarlyReview))
{
    throw "Expected early revisados increment was not found."
}

$content = $content.Replace($oldEarlyReview, $newEarlyReview)

$oldEmptyText = "        if (string.IsNullOrWhiteSpace(text))${nl}            continue;"
$newEmptyText = @(
    "        if (string.IsNullOrWhiteSpace(text))",
    "        {",
    "            knownIds.Add(id);",
    "            revisados++;",
    "            continue;",
    "        }"
) -join $nl

if (!$content.Contains($oldEmptyText))
{
    throw "Expected empty-text terminal branch was not found."
}

$content = $content.Replace($oldEmptyText, $newEmptyText)

$oldNonDemand = "        if (classification != TipoMensaje.Demanda)${nl}        {${nl}            Console.WriteLine"
$newNonDemand = "        if (classification != TipoMensaje.Demanda)${nl}        {${nl}            knownIds.Add(id);${nl}            revisados++;${nl}            Console.WriteLine"

if (!$content.Contains($oldNonDemand))
{
    throw "Expected non-demand terminal branch was not found."
}

$content = $content.Replace($oldNonDemand, $newNonDemand)

$oldInterpretation = "        var interpretacion = await interpreter.InterpretarAsync(radarMessage);${nl}        Console.WriteLine("
$newInterpretation = @(
    "        // Demand messages are acknowledged only after Intelligence returns successfully.",
    "        var interpretacion = await interpreter.InterpretarAsync(radarMessage);",
    "        knownIds.Add(id);",
    "        revisados++;",
    "        Console.WriteLine("
) -join $nl

if (!$content.Contains($oldInterpretation))
{
    throw "Expected Intelligence call was not found."
}

$content = $content.Replace($oldInterpretation, $newInterpretation)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedProgramPath, $content, $utf8NoBom)

Write-Host "RADAR retry patch applied successfully."
Write-Host "Demand message IDs are now acknowledged only after successful Intelligence processing."
Write-Host "Empty and non-demand messages remain terminal and will not be retried."
