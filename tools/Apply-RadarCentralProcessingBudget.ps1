param(
    [string]$ControllerPath = ".\Controllers\RadarIntelligenceController.cs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ControllerPath)) {
    throw "RadarIntelligenceController.cs not found: $ControllerPath"
}

$resolvedPath = Resolve-Path -LiteralPath $ControllerPath
$content = [System.IO.File]::ReadAllText($resolvedPath)
$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

$oldConstants = @'
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
'@

$newConstants = @'
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProcessingBudget = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
'@

$oldProcessing = @'
            RadarInterpretationResult resultado = await _processing.ProcesarAsync(
                agent.Correo,
                mensaje,
                cancellationToken);
'@

$newProcessing = @'
            using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            processingCts.CancelAfter(ProcessingBudget);

            RadarInterpretationResult resultado;
            try
            {
                resultado = await _processing.ProcesarAsync(
                    agent.Correo,
                    mensaje,
                    processingCts.Token);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested &&
                processingCts.IsCancellationRequested)
            {
                const string errorTimeout =
                    "RADAR Intelligence central excedio 75 segundos de procesamiento.";

                await _messageProcessingRepository.MarcarFalloReintentableAsync(
                    claim.IdRadarMessageProcessing,
                    claim.LeaseToken.Value,
                    errorTimeout,
                    RetryDelay,
                    CancellationToken.None);

                Response.Headers["Retry-After"] = ((int)RetryDelay.TotalSeconds).ToString();
                Response.Headers["X-Radar-Processing"] = "timeout";

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        mensaje = errorTimeout,
                        reintentarDespuesUtc = DateTime.UtcNow.Add(RetryDelay)
                    });
            }
'@

$oldConstants = [regex]::Replace($oldConstants.TrimEnd(), "\r?\n", $newLine)
$newConstants = [regex]::Replace($newConstants.TrimEnd(), "\r?\n", $newLine)
$oldProcessing = [regex]::Replace($oldProcessing.TrimEnd(), "\r?\n", $newLine)
$newProcessing = [regex]::Replace($newProcessing.TrimEnd(), "\r?\n", $newLine)

$constantsCount = ([regex]::Matches($content, [regex]::Escape($oldConstants))).Count
if ($constantsCount -ne 1) {
    throw "Expected exactly one processing constants block; found $constantsCount. No file was changed."
}

$processingCount = ([regex]::Matches($content, [regex]::Escape($oldProcessing))).Count
if ($processingCount -ne 1) {
    throw "Expected exactly one central processing call block; found $processingCount. No file was changed."
}

$updated = $content.Replace($oldConstants, $newConstants)
$updated = $updated.Replace($oldProcessing, $newProcessing)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedPath, $updated, $utf8NoBom)

Write-Host "Updated: $ControllerPath"
Write-Host "Central processing now has a 75-second server budget inside the 90-second Agent timeout and 120-second lease."
Write-Host "Next: build and inspect git diff before restarting A or B."
