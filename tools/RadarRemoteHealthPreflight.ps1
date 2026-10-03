<#
.SYNOPSIS
 RADAR_REMOTE_HEALTH_01-F: audited, READ-ONLY local preflight.
.DESCRIPTION
 Reads an installed Listener and its EXISTING rollback folder, hashes top-level
 deployable binaries and verifies that a Web rollback zip exists. It NEVER
 copies/deletes files, starts/stops tasks, accesses SQL, or prints secrets.
 A PASS is only a LOCAL inventory gate, not authorization to deploy.
.EXAMPLE
 pwsh -NoProfile -File ./tools/RadarRemoteHealthPreflight.ps1 -ListenerDirectory '<actual-installed-app>' -ListenerRollbackDirectory '<existing-backup>' -WebRollbackPackage '<existing-web-rollback.zip>'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ListenerDirectory,
    [Parameter(Mandatory = $true)][string]$ListenerRollbackDirectory,
    [Parameter(Mandatory = $true)][string]$WebRollbackPackage,
    [string]$TaskName = 'RSMaps RADAR Produccion'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$issues = [System.Collections.Generic.List[string]]::new()
$verifiedFiles = 0
$missingBinaries = 0
$mismatchedBinaries = 0
$taskFound = $false
$taskDisabled = $false
$profilePresent = $false
$webPackagePresent = $false
$webRollbackSha256 = $null

function Get-ExistingDirectory([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        $script:issues.Add("MISSING_$label")
        return $null
    }
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $path).Path).TrimEnd([char[]]@('\','/'))
}

try {
    $installed = Get-ExistingDirectory $ListenerDirectory 'INSTALLED_LISTENER'
    $rollback = Get-ExistingDirectory $ListenerRollbackDirectory 'LISTENER_ROLLBACK'

    if ($null -ne $installed -and $null -ne $rollback) {
        $separator = [System.IO.Path]::DirectorySeparatorChar
        if ([string]::Equals($installed, $rollback, [System.StringComparison]::OrdinalIgnoreCase) -or
            $rollback.StartsWith($installed + $separator, [System.StringComparison]::OrdinalIgnoreCase) -or
            $installed.StartsWith($rollback + $separator, [System.StringComparison]::OrdinalIgnoreCase)) {
            $issues.Add('LISTENER_BACKUP_MUST_BE_SEPARATE')
        }

        $profilePresent = Test-Path -LiteralPath (Join-Path $installed 'WhatsAppProfile') -PathType Container
        if (-not $profilePresent) {
            $issues.Add('WHATSAPP_PROFILE_NOT_DETECTED_CHECK_PATH')
        }

        # Only inspect published executable binaries; NEVER enumerate or hash profile,
        # DPAPI credential files, config, logs, or user data.
        $publishedBinaries = @(Get-ChildItem -LiteralPath $installed -File |
            Where-Object { $_.Name -match '(?i)(\.dll|\.exe|\.deps\.json|\.runtimeconfig\.json)$' })

        if ($publishedBinaries.Count -eq 0) {
            $issues.Add('INSTALLED_EXECUTABLES_NOT_FOUND')
        }

        foreach ($file in $publishedBinaries) {
            $backedUp = Join-Path $rollback $file.Name
            if (-not (Test-Path -LiteralPath $backedUp -PathType Leaf)) {
                $missingBinaries++
                continue
            }
            $sourceSha = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $backupSha = (Get-FileHash -LiteralPath $backedUp -Algorithm SHA256).Hash
            if (-not [string]::Equals($sourceSha, $backupSha, [System.StringComparison]::OrdinalIgnoreCase)) {
                $mismatchedBinaries++
            }
            else {
                $verifiedFiles++
            }
        }

        if ($missingBinaries -gt 0) { $issues.Add('BINARY_FILES_MISSING_FROM_ROLLBACK') }
        if ($mismatchedBinaries -gt 0) { $issues.Add('BINARY_HASH_MISMATCH') }
    }

    if ((Test-Path -LiteralPath $WebRollbackPackage -PathType Leaf) -and
        ($WebRollbackPackage -match '(?i)\.(zip|nupkg)$')) {
        $web = Get-Item -LiteralPath $WebRollbackPackage
        if ($web.Length -gt 0) {
            $webPackagePresent = $true
            $webRollbackSha256 = (Get-FileHash -LiteralPath $web.FullName -Algorithm SHA256).Hash
        }
    }
    if (-not $webPackagePresent) {
        $issues.Add('WEB_ROLLBACK_PACKAGE_NOT_PRESENT')
    }

    $taskCmd = Get-Command -Name Get-ScheduledTask -ErrorAction SilentlyContinue
    if ($null -eq $taskCmd) {
        $issues.Add('SCHEDULED_TASK_API_UNAVAILABLE')
    }
    else {
        $tasks = @(Get-ScheduledTask -ErrorAction Stop |
            Where-Object { $_.TaskName -eq $TaskName })
        $taskFound = ($tasks.Count -eq 1)
        if (-not $taskFound) { $issues.Add('PRODUCTION_TASK_NOT_UNIQUE_OR_NOT_FOUND') }
        else {
            $taskDisabled = ([string]$tasks[0].State -eq 'Disabled')
            if ($taskDisabled) { $issues.Add('PRODUCTION_TASK_DISABLED') }
        }
    }
}
catch {
    # Error messages can contain sensitive local paths: log only a generic code.
    $issues.Add('UNEXPECTED_LOCAL_AUDIT_EXCEPTION')
}

$localOk = $issues.Count -eq 0
$report = [ordered]@{
    PromptId = 'RADAR_REMOTE_HEALTH_01-F'
    ReadOnly = $true
    TaskPresent = $taskFound
    TaskDisabled = $taskDisabled
    WhatsAppProfileDetected = $profilePresent
    InstalledBinaryHashesMatching = $verifiedFiles
    MissingBinaryBackups = $missingBinaries
    MismatchedBinaryHashes = $mismatchedBinaries
    WebRollbackPackageDetected = $webPackagePresent
    WebRollbackPackageSha256 = $webRollbackSha256
    LocalPreflightOk = $localOk
    ProductionSqlVerified = $false
    WebRestoreTested = $false
    ProductionChangeAuthorized = $false
    Issues = @($issues.ToArray())
}

$report | ConvertTo-Json -Depth 5
Write-Output $(if ($localOk) { 'LOCAL_PREFLIGHT_OK' } else { 'LOCAL_PREFLIGHT_BLOCKED' })
if (-not $localOk) { exit 2 }
