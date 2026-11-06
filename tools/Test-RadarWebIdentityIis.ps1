param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$iis = Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
$template = Join-Path $env:ProgramFiles 'IIS Express\config\templates\PersonalWebServer\applicationhost.config'
$modulePath = Join-Path $env:ProgramFiles 'IIS Express\Asp.Net Core Module\V2\aspnetcorev2.dll'
foreach ($path in @($iis,$template,$modulePath)) { if (!(Test-Path -LiteralPath $path)) { throw 'Local IIS Express/ANCM unavailable; do not install automatically.' } }
$root = Join-Path $env:TEMP ('radar-iis-' + [guid]::NewGuid().ToString('N'))
$site = Join-Path $root 'site'
New-Item -ItemType Directory -Path $site | Out-Null
$build = Join-Path $repo 'RSMaps.Radar.Lab\bin\Release\net10.0'
if (!(Test-Path (Join-Path $build 'RSMaps.Radar.Lab.dll'))) { throw 'Build Lab Release first.' }
Copy-Item -Path (Join-Path $build '*') -Destination $site -Recurse
New-Item -ItemType Directory -Path (Join-Path $site 'wwwroot') -Force | Out-Null
'local-only' | Set-Content (Join-Path $site 'wwwroot\probe.txt')
$xml = @'
<configuration><system.webServer><handlers><add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" /></handlers><aspNetCore processPath="dotnet" arguments=".\RSMaps.Radar.Lab.dll" hostingModel="inprocess" stdoutLogEnabled="false"><environmentVariables><environmentVariable name="RADAR_LAB_MODE" value="web-sql-identity-iis-harness" /><environmentVariable name="RADAR_SQL_TEST_INSTANCE" value="RADAROfficeTests" /><environmentVariable name="RADAR_IIS_TEST_ROOT" value="ROOT_PLACEHOLDER" /></environmentVariables></aspNetCore></system.webServer></configuration>
'@
$xml.Replace('ROOT_PLACEHOLDER',[Security.SecurityElement]::Escape($root)) | Set-Content (Join-Path $site 'web.config')
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start(); $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port; $listener.Stop()
[xml]$x = Get-Content -LiteralPath $template
$section = $x.CreateElement('section'); $section.SetAttribute('name','aspNetCore'); $section.SetAttribute('overrideModeDefault','Allow')
$group = $x.SelectSingleNode('/configuration/configSections/sectionGroup[@name="system.webServer"]')
if (!$group.SelectSingleNode('section[@name="aspNetCore"]')) { [void]$group.AppendChild($section) }
$global = $x.SelectSingleNode('/configuration/system.webServer/globalModules')
if (!$global.SelectSingleNode('add[@name="AspNetCoreModuleV2"]')) {
    $m=$x.CreateElement('add'); $m.SetAttribute('name','AspNetCoreModuleV2'); $m.SetAttribute('image',$modulePath); [void]$global.AppendChild($m)
}
$modules = $x.SelectSingleNode('//system.webServer/modules')
if (!$modules.SelectSingleNode('add[@name="AspNetCoreModuleV2"]')) {
    $m=$x.CreateElement('add'); $m.SetAttribute('name','AspNetCoreModuleV2'); [void]$modules.AppendChild($m)
}
$s=$x.SelectSingleNode('/configuration/system.applicationHost/sites/site'); $s.SetAttribute('name','RadarLocalIdentity')
$s.SelectSingleNode('application/virtualDirectory').SetAttribute('physicalPath',$site)
$s.SelectSingleNode('bindings/binding').SetAttribute('bindingInformation',(':'+ $port+':localhost'))
foreach($pool in $x.SelectNodes('/configuration/system.applicationHost/applicationPools/add')) { $pool.SetAttribute('managedRuntimeVersion','') }
$config=Join-Path $root 'applicationhost.config'; $x.Save($config)
foreach ($expected in @('OBSERVED','CONSUMED')) {
    $proc=Start-Process $iis -ArgumentList @('/config:"'+$config+'"','/site:RadarLocalIdentity','/systray:false') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root ($expected+'.log')) -RedirectStandardError (Join-Path $root ($expected+'-error.log'))
    try {
        Start-Sleep -Seconds 2
        $response=Invoke-WebRequest ('http://localhost:'+ $port+'/probe.txt') -SkipHttpErrorCheck -TimeoutSec 30
        if ([int]$response.StatusCode -ne 200) { throw ('IIS local HTTP status '+[int]$response.StatusCode) }
        $status=Join-Path $root 'private\harness-status.json'; $result=$null
        for($attempt=0;$attempt -lt 30;$attempt++) {
            if(Test-Path $status) { $candidate=Get-Content -LiteralPath $status -Raw | ConvertFrom-Json; if($candidate.Pid -eq $proc.Id) { $result=$candidate; break } }
            Start-Sleep -Seconds 1
        }
        if(!$result -or $result.Status -ne $expected -or !$result.DatabaseRemoved -or $result.Process -ne 'iisexpress') { throw 'IIS identity/restart test failed' }
        foreach($route in @('/manifest.json','/private/harness-status.json','/data/radar-sql-identity/manifest.json')) {
            $r=Invoke-WebRequest ('http://localhost:'+ $port+$route) -SkipHttpErrorCheck -TimeoutSec 10
            if([int]$r.StatusCode -ne 404) { throw 'Private HTTP isolation failed' }
        }
        Write-Output ('IIS_INPROCESS_'+$expected+'_AND_PRIVATE_HTTP_OK')
    } finally { if(!$proc.HasExited) { Stop-Process -Id $proc.Id } }
}
Write-Output ('Synthetic local artifacts: '+$root)
