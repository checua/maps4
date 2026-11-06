$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=Join-Path $repo ('RSMaps.Radar.Lab\bin\rollback-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$web=Join-Path $root 'web'
$restored=Join-Path $root 'restored'
$lab=Join-Path $root 'lab'
$targets=Join-Path $root 'SafePublish.targets'
@'
<Project>
<Target Name="ExcludePrivateConfiguration" AfterTargets="ComputeFilesToPublish">
<ItemGroup>
<ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" Condition="$([System.String]::Copy('%(ResolvedFileToPublish.RelativePath)').StartsWith('appsettings'))" />
</ItemGroup>
</Target>
</Project>
'@ | Set-Content $targets
Push-Location $repo
try {
    # Record exact local inputs because the candidate has not been committed yet.
    $sourceFiles = rg --files -g '*.cs' -g '*.csproj' -g '*.sql' -g '*.cshtml' -g '*.js' -g '*.css' -g 'global.json' -g 'NuGet.Config'
    $sourceFiles | ForEach-Object {
        [pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_).Hash}
    } | Export-Csv (Join-Path $root 'source-manifest.csv') -NoTypeInformation
    dotnet publish maps4.csproj -c Release --no-restore -o $web --nologo "/p:CustomAfterMicrosoftCommonTargets=$targets" > (Join-Path $root 'publish.log')
    if($LASTEXITCODE){throw 'Publish failed'}
    if(Get-ChildItem $web -Filter 'appsettings*.json'){throw 'Original settings reached publish'}
    @{
        Logging=@{LogLevel=@{Default='Information';'Microsoft.AspNetCore'='Warning'}}
        AllowedHosts='localhost;127.0.0.1'
        RSMaps=@{ImageStorageProvider='Local';PublicBaseUrl='http://127.0.0.1';PublicAssetBaseUrl='http://127.0.0.1'}
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $web 'appsettings.json')
    foreach($name in @('maps4.dll','maps4.deps.json','maps4.runtimeconfig.json','RSMaps.Radar.Intelligence.dll','web.config','RadarKnowledge/radar-knowledge.json','wwwroot/js/index.js')) {
        if(!(Test-Path (Join-Path $web $name))){throw "Missing $name"}
    }
    # Password variables read from forms are runtime input, not embedded literals.
    $pattern='(?i)(AccountKey\s*=\s*[^;\s"'']+|;\s*(?:Password|Pwd)\s*=\s*[^;\s"'']+|(?:Password|Pwd)\s*=\s*["''][^"'']+["'']|sk-(?:proj-)?[A-Za-z0-9_-]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|Bearer\s+[A-Za-z0-9_-]{20,})'
    foreach($file in Get-ChildItem $web -Recurse -File) {
        if($file.Extension -match '^\.(dpapi|credential|credentials|pem|key|pfx|p12|publishsettings)$' -or $file.Name -like '.env*'){throw "Private file: $($file.Name)"}
        if($file.Extension -match '^\.(json|config|js|xml|txt|ps1|yaml|yml|html|cshtml)$' -and [IO.File]::ReadAllText($file.FullName) -match $pattern){throw "Sensitive pattern: $($file.Name)"}
    }
    $manifest=@(Get-ChildItem $web -Recurse -File | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($web.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash $_.FullName).Hash;Source='5299f15 plus uncommitted worktree';Utc=[DateTime]::UtcNow.ToString('o')}
    })
    $manifest | Export-Csv (Join-Path $root 'manifest.csv') -NoTypeInformation
    $zip=Join-Path $root 'web-compatible-rollback.zip'
    Compress-Archive -Path (Join-Path $web '*') -DestinationPath $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $restored
    if(@(Get-ChildItem $restored -Recurse -File).Count -ne $manifest.Count){throw 'File count mismatch'}
    foreach($entry in $manifest){if((Get-FileHash (Join-Path $restored $entry.Path)).Hash -ne $entry.SHA256){throw 'Restore hash mismatch'}}
    dotnet build RSMaps.Radar.Lab/RSMaps.Radar.Lab.csproj -c Release --no-restore -o $lab --nologo > (Join-Path $root 'lab-build.log')
    if($LASTEXITCODE){throw 'Lab build failed'}
    # Load the exact restored backend assemblies into the isolated test harness.
    foreach($name in @('maps4.dll','RSMaps.Radar.Intelligence.dll')){Copy-Item (Join-Path $restored $name) (Join-Path $lab $name) -Force}
    foreach($mode in @('poison-message-regression','poison-message-sql-regression','delivery-flow-regression')) {
        $env:RADAR_LAB_MODE=$mode
        dotnet (Join-Path $lab 'RSMaps.Radar.Lab.dll') > (Join-Path $root ($mode+'.log'))
        if($LASTEXITCODE){throw "Regression failed: $mode"}
        Write-Output "$mode=PASS"
    }
    Remove-Item Env:RADAR_LAB_MODE
    $socket=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $socket.Start(); $port=$socket.LocalEndpoint.Port; $socket.Stop()
    $psi=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $psi.Arguments='maps4.dll'
    $psi.WorkingDirectory=$restored
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    $psi.EnvironmentVariables.Clear()
    foreach($name in @('SystemRoot','PATH','TEMP','TMP','USERPROFILE','LOCALAPPDATA','ProgramFiles')){$psi.EnvironmentVariables[$name]=[Environment]::GetEnvironmentVariable($name)}
    $psi.EnvironmentVariables['ASPNETCORE_ENVIRONMENT']='RollbackValidation'
    $psi.EnvironmentVariables['ASPNETCORE_URLS']="http://127.0.0.1:$port"
    $psi.EnvironmentVariables['ConnectionStrings__cadenaSQL']='Server=(localdb)\MSSQLLocalDB;Database=RadarPoisonRegression_Unused;Integrated Security=true;Encrypt=false;Connect Timeout=2'
    $psi.EnvironmentVariables['OPENAI_API_KEY']=''
    $process=[Diagnostics.Process]::Start($psi)
    $stdout=$process.StandardOutput.ReadToEndAsync()
    $stderr=$process.StandardError.ReadToEndAsync()
    try {
        $ready=$false
        for($i=0;$i -lt 30;$i++) {
            if($process.HasExited){throw 'Web exited before readiness'}
            try{$r=Invoke-WebRequest "http://127.0.0.1:$port/Inicio/IniciarSesion" -UseBasicParsing -TimeoutSec 2; if($r.StatusCode -eq 200){$ready=$true;break}}catch{Start-Sleep -Milliseconds 500}
        }
        if(!$ready){throw 'Login smoke failed'}
        $asset=Invoke-WebRequest "http://127.0.0.1:$port/js/index.js" -UseBasicParsing -TimeoutSec 5
        if($asset.StatusCode -ne 200){throw 'Static asset smoke failed'}
        Write-Output 'RESTORED_WEB_SMOKE=PASS'
    }
    finally {
        # Terminate exclusively the isolated process created above.
        if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
        $stdout.Result | Set-Content (Join-Path $root 'local-web.log')
        $stderr.Result | Set-Content (Join-Path $root 'local-web-error.log')
        $process.Dispose()
    }
    [pscustomobject]@{Root=$root;Zip=$zip;SHA256=(Get-FileHash $zip).Hash;Files=$manifest.Count;Restore='PASS';Boot='PASS'} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $root 'result.json')
}
finally {Remove-Item Env:RADAR_LAB_MODE -ErrorAction SilentlyContinue; Pop-Location}
