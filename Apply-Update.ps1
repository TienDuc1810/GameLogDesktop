param([Parameter(Mandatory=$true)][string]$PlanPath,[switch]$SkipRestart)
$ErrorActionPreference='Stop'
$plan=Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$appRoot=[IO.Path]::GetFullPath($plan.Root).TrimEnd('\')
$stageRoot=[IO.Path]::GetFullPath($plan.Stage)
if(-not $stageRoot.StartsWith($appRoot+'\.gamelog-update-',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid update workspace'}
if([IO.Path]::GetFullPath($PlanPath) -ne (Join-Path $stageRoot 'plan.json')){throw 'Invalid plan location'}
function Resolve-Managed([string]$base,[string]$relative) {
 if([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative.Contains('\')){throw 'Invalid managed path'}
 $parts=$relative.Split('/')
 foreach($part in $parts){if(-not $part -or $part.StartsWith('.') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw 'Invalid managed path'}}
 if($parts[0] -in @('desktop-data','backup','work','data')){throw 'Data folder is protected'}
 $resolved=[IO.Path]::GetFullPath((Join-Path $base $relative))
 if(-not $resolved.StartsWith($base.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside target folder'}
 $cursor=$resolved
 while($cursor -and $cursor.Length -ge $base.Length){if(Test-Path -LiteralPath $cursor){if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked files are not managed'}};$cursor=[IO.Path]::GetDirectoryName($cursor)}
 return $resolved
}
function File-Hash([string]$file){$sha=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($file);try{return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}}
$payloadRoot=Join-Path $stageRoot 'payload'
$rollbackRoot=Join-Path $stageRoot 'rollback'
$changes=[Collections.Generic.List[object]]::new()
$success=$false
$canRestart=$true
$rollbackComplete=$true
try {
 if($plan.ProcessId -gt 0){$oldProcess=Get-Process -Id $plan.ProcessId -ErrorAction SilentlyContinue;if($oldProcess -and -not $oldProcess.WaitForExit(120000)){$canRestart=$false;throw 'App did not close; no files were replaced'}}
 $dataFolder=Join-Path $appRoot 'desktop-data'
 if(Test-Path -LiteralPath $dataFolder){
  $backupBase=Join-Path $dataFolder 'backup'
  foreach($folder in @($dataFolder,$backupBase)){if((Test-Path -LiteralPath $folder) -and ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Cannot back up through a linked data folder'}}
  $dataBackup=Join-Path $backupBase ('update-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff'))
  [IO.Directory]::CreateDirectory($dataBackup)|Out-Null
  foreach($file in Get-ChildItem -LiteralPath $dataFolder -Filter '*.json' -File){[IO.File]::Copy($file.FullName,(Join-Path $dataBackup $file.Name),$true)}
 }
 $manifest=Get-Content -LiteralPath (Join-Path $payloadRoot 'update-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Product -ne 'GameLogDesktop'){throw 'Wrong product'}
 foreach($item in $manifest.Files){$source=Resolve-Managed $payloadRoot $item.Path;if((File-Hash $source) -ne $item.Sha256){throw ('Hash mismatch: '+$item.Path)}}
 foreach($relative in @($plan.Files)+@($plan.Obsolete)){
  $destination=Resolve-Managed $appRoot $relative
  $backup=Resolve-Managed $rollbackRoot $relative
  $existed=Test-Path -LiteralPath $destination
  if($existed){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup))|Out-Null;[IO.File]::Copy($destination,$backup,$true)}
  $changes.Add([pscustomobject]@{Destination=$destination;Backup=$backup;Existed=$existed})
  if($relative -in $plan.Files){$source=Resolve-Managed $payloadRoot $relative;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null;[IO.File]::Copy($source,$destination,$true)}
  elseif($existed){Remove-Item -LiteralPath $destination -Force}
 }
 if(-not $SkipRestart){
  $ready=Join-Path $stageRoot 'ready.json';$env:GAMELOG_UPDATE_READY=$ready
  $newProcess=Start-Process -FilePath (Join-Path $appRoot 'GameLogDesktop.exe') -WorkingDirectory $appRoot -WindowStyle Hidden -PassThru
  $env:GAMELOG_UPDATE_READY=$null
  $deadline=[DateTime]::UtcNow.AddSeconds(30)
  while(-not (Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline -and -not $newProcess.HasExited){Start-Sleep -Milliseconds 250;$newProcess.Refresh()}
  if(-not (Test-Path -LiteralPath $ready) -or (Get-Content -LiteralPath $ready -Raw).Trim() -ne $manifest.Version){if(-not $newProcess.HasExited){$newProcess.Kill();$newProcess.WaitForExit()};throw 'New app failed its startup or version check'}
 }
 $success=$true
 @{Success=$true;Message=('Da cap nhat GameLog Desktop '+$manifest.Version)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $appRoot 'update-result.json') -Encoding UTF8
}catch {
 $reason=$_.Exception.Message
 for($i=$changes.Count-1;$i -ge 0;$i--){$change=$changes[$i];try{if($change.Existed){[IO.File]::Copy($change.Backup,$change.Destination,$true)}elseif(Test-Path -LiteralPath $change.Destination){Remove-Item -LiteralPath $change.Destination -Force}}catch{$rollbackComplete=$false}}
 $message=if($rollbackComplete){'Cap nhat that bai, da khoi phuc ban cu: '+$reason}else{'Can khoi phuc thu cong, ban sao chuong trinh duoc giu tai '+$rollbackRoot+': '+$reason}
 @{Success=$false;Message=$message} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $appRoot 'update-result.json') -Encoding UTF8
 if(-not $SkipRestart -and $canRestart -and $rollbackComplete){Start-Process -FilePath (Join-Path $appRoot 'GameLogDesktop.exe') -WorkingDirectory $appRoot -WindowStyle Hidden}
}finally {
 $env:GAMELOG_UPDATE_READY=$null
 # Only the verified, dedicated update workspace can be recursively removed.
 if(($success -or $rollbackComplete) -and $stageRoot.StartsWith($appRoot+'\.gamelog-update-',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $stageRoot)){
  $links=Get-ChildItem -LiteralPath $stageRoot -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}
  if(-not $links){Remove-Item -LiteralPath $stageRoot -Recurse -Force}
 }
}
if(-not $success){exit 1}
