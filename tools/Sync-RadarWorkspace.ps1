[CmdletBinding()]
param(
    [ValidateSet('Status', 'Fetch', 'Start', 'Prepare', 'Secrets')]
    [string]$Action = 'Status',
    [string]$Repository = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
function Invoke-Git {
    param([string[]]$Arguments)
    $result = & git --no-optional-locks -C $Repository @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Git failed. No working files were changed by this script.' }
    return $result
}
try {
    $Repository = (Resolve-Path -LiteralPath $Repository).Path
    $null = Invoke-Git @('rev-parse', '--show-toplevel')
    if ($Action -in @('Fetch', 'Start', 'Prepare')) {
        $null = Invoke-Git @('fetch', 'origin')
    }
    $branch = Invoke-Git @('branch', '--show-current')
    $head = Invoke-Git @('rev-parse', 'HEAD')
    $dirty = @(Invoke-Git @('status', '--porcelain=v1', '--untracked-files=all'))
    $untracked = @(Invoke-Git @('ls-files', '--others', '--exclude-standard'))
    $ignored = @(Invoke-Git @('ls-files', '--others', '--ignored', '--exclude-standard'))
    $remoteRef = "refs/remotes/origin/$branch"
    & git -C $Repository show-ref --verify --quiet $remoteRef
    $remoteExists = $LASTEXITCODE -eq 0
    $ahead = $null
    $behind = $null
    if ($remoteExists) {
        $counts = (Invoke-Git @('rev-list', '--left-right', '--count', "HEAD...$remoteRef")) -split '\s+'
        $ahead = [int]$counts[0]
        $behind = [int]$counts[1]
    }
    $unpublished = @(Invoke-Git @('rev-list', 'HEAD', '--not', '--remotes=origin'))
    [pscustomobject]@{
        Branch = $branch; Head = $head; ChangedEntries = $dirty.Count
        UntrackedFiles = $untracked.Count; IgnoredFiles = $ignored.Count
        SameNameRemoteExists = $remoteExists; Ahead = $ahead; Behind = $behind
        CommitsAbsentFromOrigin = $unpublished.Count
    } | Format-List
    $findings = 0
    if ($Action -in @('Secrets', 'Prepare')) {
        # Heuristic only. Never print matching content or credential values.
        $pattern = '(?i)(password|pwd|accountkey|clientsecret|api[_-]?key)\s*["'']?\s*[:=]\s*["'']?[^\s"'';]{4,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}'
        $files = @(Invoke-Git @('-c', 'core.quotePath=false', 'ls-files', '--cached', '--others', '--exclude-standard')) | Sort-Object -Unique
        if ($Action -eq 'Prepare') {
            $files = @(@(Invoke-Git @('-c', 'core.quotePath=false', 'diff', '--name-only', 'HEAD')) + $untracked) | Sort-Object -Unique
        }
        foreach ($file in $files) {
            $path = Join-Path $Repository $file
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
            if ([IO.File]::ReadAllText($path) -match $pattern) {
                Write-Output "REVIEW working file: $file"
                $findings++
            }
        }
        foreach ($commit in $unpublished) {
            # Inspect every changed file at every unpublished commit, including removed secrets.
            $historicalFiles = @(Invoke-Git @('-c', 'core.quotePath=false', 'diff-tree', '--root', '-m', '--no-commit-id', '--name-only', '-r', '--diff-filter=ACMRT', $commit)) | Sort-Object -Unique
            foreach ($file in $historicalFiles) {
                $content = (Invoke-Git @('show', "${commit}:$file")) -join "`n"
                if ($content -match $pattern) {
                    Write-Output "REVIEW historical file: ${commit}:$file"
                    $findings++
                }
            }
        }
        Write-Output "Heuristic findings: $findings. Manual review and a dedicated secret scanner remain required before publication."
    }
    if ($Action -in @('Start', 'Prepare')) {
        if (!$branch -or !$remoteExists -or $dirty.Count -gt 0 -or $ahead -ne 0 -or $behind -ne 0) {
            throw 'Handoff blocked. Require a named branch, clean working tree, and HEAD equal to the same branch on origin.'
        }
    }
    if ($findings -gt 0) { throw 'Potential secrets require local review. Values were suppressed.' }
    if ($Action -eq 'Prepare') { Write-Output 'Git handoff checks passed. Review ignored files separately and record HEAD in DEV_HANDOFF.' }
    exit 0
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}
