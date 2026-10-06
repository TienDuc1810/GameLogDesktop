param([Parameter(Mandatory=$true)][string]$PublishFolder,[Parameter(Mandatory=$true)][string]$Version,[Parameter(Mandatory=$true)][string]$ZipPath,[string]$PackageUrl='')
$ErrorActionPreference='Stop'
$releaseRoot=[IO.Path]::GetFullPath($PublishFolder).TrimEnd('\')
$files=@(Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | Where-Object {$_.Name -notin @('update-manifest.json','update-feed.json','desktop-error.log','update-result.json')} | ForEach-Object {
 $relative=$_.FullName.Substring($releaseRoot.Length+1).Replace('\','/')
 if($relative -match '^(desktop-data|data|backup|work)/'){throw 'Publish folder contains private user data'}
 @{Path=$relative;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
@{Product='GameLogDesktop';Version=$Version;Files=$files} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $releaseRoot 'update-manifest.json') -Encoding UTF8
Compress-Archive -LiteralPath $releaseRoot -DestinationPath $ZipPath -Force
@{Product='GameLogDesktop';Version=$Version;PackageUrl=$PackageUrl;PackageSha256=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content -LiteralPath ([IO.Path]::ChangeExtension($ZipPath,'.feed.json')) -Encoding UTF8
