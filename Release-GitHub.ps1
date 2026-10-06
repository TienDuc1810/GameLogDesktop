param([Parameter(Mandatory=$true)][string]$Repository,[Parameter(Mandatory=$true)][string]$Tag,[string]$DotNet='dotnet',[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
if($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$'){throw 'Repository must be owner/repo'}
$projectPath=Join-Path $PSScriptRoot 'GameLogDesktop.csproj'
$version=([xml](Get-Content -LiteralPath $projectPath -Raw)).Project.PropertyGroup.Version
if($Tag -ne "v$version"){throw 'Release tag must match project version'}
$buildRoot=Join-Path $PSScriptRoot ('release-output/'+[guid]::NewGuid().ToString('N'))
$appFolder=Join-Path $buildRoot 'GameLogDesktop'
& $DotNet publish $projectPath -c Release -r win-x64 --self-contained true -o $appFolder --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed'}
@{Product='GameLogDesktop';FeedUrl="https://github.com/$Repository/releases/latest/download/update-feed.json"} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $appFolder 'update-default.json') -Encoding utf8
$qa=Join-Path $buildRoot 'qa'
$env:GAMELOG_PRICE_PROBE='0'
$env:GAMELOG_METRO_PROBE=''
$process=Start-Process -FilePath (Join-Path $appFolder 'GameLogDesktop.exe') -ArgumentList @('--self-test',"`"$qa`"") -PassThru -WindowStyle Hidden
if(-not $process.WaitForExit(180000)){$process.Kill();throw 'Self-tests timed out'}
if($process.ExitCode -ne 0){throw ('Self-tests failed: '+(Get-Content -LiteralPath (Join-Path $qa 'failure.txt') -Raw))}
$zipName="GameLogDesktop-Windows-x64-v$version.zip"
$zipPath=Join-Path $buildRoot $zipName
& (Join-Path $PSScriptRoot 'Generate-Release.ps1') -PublishFolder $appFolder -Version $version -ZipPath $zipPath -PackageUrl "https://github.com/$Repository/releases/download/$Tag/$zipName"
$feedPath=Join-Path $buildRoot 'update-feed.json'
Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($zipPath,'.feed.json')) -Destination $feedPath
if($PrepareOnly){Write-Output "Prepared release: $buildRoot";exit}
& gh release view $Tag --repo $Repository *> $null
if($LASTEXITCODE -eq 0){
 & gh release upload $Tag $zipPath $feedPath --repo $Repository --clobber
 if($LASTEXITCODE -ne 0){throw 'Upload failed'}
 & gh release edit $Tag --repo $Repository --latest
}else{
 & gh release create $Tag $zipPath $feedPath --repo $Repository --verify-tag --title "GameLog Desktop $version" --generate-notes --latest
}
if($LASTEXITCODE -ne 0){throw 'GitHub publication failed; build files remain for inspection'}
Write-Output "Published $Repository $Tag"
