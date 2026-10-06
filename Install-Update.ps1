param([string]$TargetExe,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$stageRoot=$null
try {
 if(-not $TargetExe){$picker=New-Object System.Windows.Forms.OpenFileDialog;$picker.Title='Chon GameLogDesktop.exe dang dung trong E:\Desktop App';$picker.Filter='GameLog Desktop|GameLogDesktop.exe';if($picker.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){exit};$TargetExe=$picker.FileName}
 $exe=[IO.Path]::GetFullPath($TargetExe)
 $appRoot=[IO.Path]::GetDirectoryName($exe)
 $ancestor=$appRoot;while($ancestor){if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked install folders are not supported'};$ancestor=[IO.Path]::GetDirectoryName($ancestor)}
 $packageRoot=Join-Path $PSScriptRoot 'NewFiles'
 $manifest=Get-Content -LiteralPath (Join-Path $packageRoot 'update-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Product -ne 'GameLogDesktop'){throw 'Wrong update product'}
 $identity=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
 if($identity.ProductName -ne 'GameLogDesktop'){throw 'File da chon khong phai GameLog Desktop'}
 $oldVersion=$identity.FileVersion
 if([version]$oldVersion -ge [version]$manifest.Version){throw 'App dang dung cung phien ban hoac moi hon goi nay'}
 function Resolve-Safe([string]$base,[string]$relative){
  if([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative.Contains('\')){throw 'Invalid file path'}
  $parts=$relative.Split('/');foreach($part in $parts){if(-not $part -or $part.StartsWith('.') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw 'Invalid file path'}}
  if($parts[0] -in @('desktop-data','data','backup','work')){throw 'Data folders are protected'}
  $full=[IO.Path]::GetFullPath((Join-Path $base $relative))
  if(-not $full.StartsWith($base.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'File outside selected folder'}
  $cursor=$full;while($cursor -and $cursor.Length -ge $base.Length){if(Test-Path -LiteralPath $cursor){if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked folders are not supported'}};$cursor=[IO.Path]::GetDirectoryName($cursor)}
  return $full
 }
 function File-Hash([string]$file){$sha=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($file);try{return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}}
 $stageRoot=Join-Path $appRoot ('.gamelog-update-'+[guid]::NewGuid().ToString('N'))
 $payloadRoot=Join-Path $stageRoot 'payload'
 [IO.Directory]::CreateDirectory($payloadRoot)|Out-Null
 foreach($file in $manifest.Files){
  $newFile=Resolve-Safe $packageRoot $file.Path
  $source=if(Test-Path -LiteralPath $newFile){$newFile}else{Resolve-Safe $appRoot $file.Path}
  if(-not (Test-Path -LiteralPath $source) -or (File-Hash $source) -ne $file.Sha256){throw ('Thieu hoac khac file '+$file.Path+'. Hay dung goi ban day du.')}
  $target=Resolve-Safe $payloadRoot $file.Path
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
  [IO.File]::Copy($source,$target,$true)
 }
 [IO.File]::Copy((Join-Path $packageRoot 'update-manifest.json'),(Join-Path $payloadRoot 'update-manifest.json'),$true)
 $obsolete=@();$oldManifest=Join-Path $appRoot 'update-manifest.json'
 if(Test-Path -LiteralPath $oldManifest){$old=Get-Content -LiteralPath $oldManifest -Raw | ConvertFrom-Json;if($old.Product -eq 'GameLogDesktop'){foreach($file in $old.Files){if($file.Path -notin $manifest.Files.Path){$candidate=Resolve-Safe $appRoot $file.Path;if((Test-Path -LiteralPath $candidate) -and (File-Hash $candidate) -eq $file.Sha256){$obsolete+=$file.Path}}}}}
 $running=@(Get-Process -Name GameLogDesktop -ErrorAction SilentlyContinue | Where-Object {try{$_.MainModule.FileName -eq $exe}catch{$false}})
 if($running.Count -gt 1){throw 'Hay dong cac cua so GameLog truoc khi cap nhat'}
 $oldId=if($running.Count -eq 1){$running[0].Id}else{0}
 @{Root=$appRoot;Stage=$stageRoot;ProcessId=$oldId;Files=@($manifest.Files.Path)+@('update-manifest.json');Obsolete=@($obsolete)} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stageRoot 'plan.json') -Encoding UTF8
 $helper=Join-Path $stageRoot 'Apply-Update.ps1'
 [IO.File]::Copy((Join-Path $packageRoot 'Apply-Update.ps1'),$helper,$true)
 if($PrepareOnly){Write-Output (Join-Path $stageRoot 'plan.json');exit}
 if($running.Count -eq 1){[void]$running[0].CloseMainWindow()}
 $planFile=Join-Path $stageRoot 'plan.json'
 Start-Process -FilePath powershell.exe -ArgumentList @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"'+$helper+'"'),'-PlanPath',('"'+$planFile+'"')) -WindowStyle Hidden
 [Windows.Forms.MessageBox]::Show('Dang cap nhat. App se tu mo lai, du lieu duoc giu nguyen.','GameLog Desktop')|Out-Null
}catch {
 if(-not $PrepareOnly){[Windows.Forms.MessageBox]::Show(('Chua cap nhat: '+$_.Exception.Message),'GameLog Desktop')|Out-Null}
 if($stageRoot -and $stageRoot.StartsWith($appRoot+'\.gamelog-update-',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $stageRoot)){
  if(-not (Get-ChildItem -LiteralPath $stageRoot -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})){Remove-Item -LiteralPath $stageRoot -Recurse -Force}
 }
 if($PrepareOnly){Write-Error $_}
 exit 1
}
