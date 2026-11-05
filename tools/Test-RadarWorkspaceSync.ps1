$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ("radar-sync-test-" + [guid]::NewGuid())
$remote = Join-Path $root 'remote.git'
$repo = Join-Path $root 'office'
$homeRepo = Join-Path $root 'home'
$script = Join-Path $PSScriptRoot 'Sync-RadarWorkspace.ps1'
New-Item -ItemType Directory -Path $root | Out-Null
function Invoke-FixtureGit {
    & git @args | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git operation failed' }
}
function Check([string]$name, [string]$action, [bool]$success) {
    $output = & pwsh -NoProfile -File $script -Repository $repo -Action $action 2>&1
    if (($LASTEXITCODE -eq 0) -ne $success) { throw "Failed test $name" }
    Write-Output "PASS $name"
}
Invoke-FixtureGit init --bare $remote
Invoke-FixtureGit init -b diagnostic/test $repo
Invoke-FixtureGit -C $repo config user.name 'Local fixture'
Invoke-FixtureGit -C $repo config user.email 'fixture@example.invalid'
Set-Content (Join-Path $repo 'sample.txt') 'base'
Invoke-FixtureGit -C $repo add sample.txt
Invoke-FixtureGit -C $repo commit -m 'fixture base'
Invoke-FixtureGit -C $repo remote add origin $remote
Check 'Unpublished branch blocked' Start $false
Invoke-FixtureGit -C $repo push -u origin diagnostic/test
Check 'Synchronized branch accepted' Start $true
Set-Content (Join-Path $repo 'sample.txt') 'local modification'
$before = (Get-FileHash (Join-Path $repo 'sample.txt')).Hash
Check 'Modified file blocked' Start $false
if ((Get-FileHash (Join-Path $repo 'sample.txt')).Hash -ne $before) { throw 'Local edit overwritten' }
Set-Content (Join-Path $repo 'new.txt') 'new content'
Check 'Untracked file blocked' Prepare $false
Invoke-FixtureGit -C $repo add .
Invoke-FixtureGit -C $repo commit -m 'fixture office'
Check 'Unpublished commit blocked' Start $false
Invoke-FixtureGit clone -b diagnostic/test $remote $homeRepo
Invoke-FixtureGit -C $homeRepo config user.name 'Local fixture'
Invoke-FixtureGit -C $homeRepo config user.email 'fixture@example.invalid'
Set-Content (Join-Path $homeRepo 'home.txt') 'home content'
Invoke-FixtureGit -C $homeRepo add .
Invoke-FixtureGit -C $homeRepo commit -m 'fixture home'
Invoke-FixtureGit -C $homeRepo push
Check 'Divergence blocked' Start $false
$status = & pwsh -NoProfile -File $script -Repository $repo -Action Status
if (($status -join ' ') -notmatch 'Ahead\s*:\s*1' -or ($status -join ' ') -notmatch 'Behind\s*:\s*1') { throw 'Divergence counts incorrect' }
Write-Output 'PASS divergence counts 1/1'
Set-Content (Join-Path $repo 'fixture-secret.txt') ('Pass' + 'word=' + 'SYNTHETIC_TEST_VALUE')
Check 'Synthetic secret detected' Secrets $false
Invoke-FixtureGit -C $repo add .
Invoke-FixtureGit -C $repo commit -m 'fixture synthetic secret'
Set-Content (Join-Path $repo 'fixture-secret.txt') 'redacted'
Invoke-FixtureGit -C $repo add .
Invoke-FixtureGit -C $repo commit -m 'fixture remove synthetic secret'
Check 'Secret in unpublished history detected' Secrets $false
Invoke-FixtureGit -C $repo remote set-url origin (Join-Path $root 'missing.git')
Check 'Fetch failure blocks start' Start $false
Write-Output "Fixtures preserved at $root"
