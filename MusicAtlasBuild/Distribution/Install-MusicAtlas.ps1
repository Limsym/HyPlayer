param([switch]$TrustCertificate,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$meta=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($file in $meta.Files){
 $target=Join-Path $PSScriptRoot $file.Name
 if(!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.SHA256){throw ('Missing or modified file: '+$file.Name)}
}
$certPath=Join-Path $PSScriptRoot 'MusicAtlas.cer'
$cert=Get-PfxCertificate -FilePath $certPath
if($cert.Thumbprint -ne $meta.Thumbprint -or $cert.Subject -ne 'CN=HyPlayer Team'){throw 'Unexpected signing certificate'}
$installed=Get-AppxPackage -Name '48848aaaaaaccd.HyPlayer'
if($installed -and $installed.Publisher -ne 'CN=HyPlayer Team'){throw 'Existing application identity differs'}
if($installed -and [version]$installed.Version -gt [version]$meta.Version){throw 'A newer HyPlayer is installed; automatic downgrade is disabled'}
$trusted=Test-Path ('Cert:\LocalMachine\TrustedPeople\'+$meta.Thumbprint)
$classificationsPath=Join-Path $PSScriptRoot 'classifications.json'
$classifications=[IO.File]::ReadAllText($classificationsPath)
$classificationData=$classifications|ConvertFrom-Json
if($classificationData.schema -ne 4 -or !$classificationData.categories -or $null -eq $classificationData.overrides){throw 'Invalid bundled Music Atlas classifications'}
Write-Output ('Package: '+$meta.Version+' x64; installed: '+$installed.Version+'; machine certificate trusted: '+$trusted)
Write-Output ('Bundled published manual classifications: '+@($classificationData.overrides.PSObject.Properties).Count+' songs. Existing classifications will not be overwritten.')
if($CheckOnly){Write-Output 'All release file hashes verified. No changes made.';exit 0}
if(!$trusted){
 if(!$TrustCertificate){throw 'First installation: run in an Administrator PowerShell with -TrustCertificate to trust this publisher in LocalMachine\TrustedPeople.'}
 $admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
 if(!$admin){throw '-TrustCertificate requires an Administrator PowerShell'}
 Import-Certificate -FilePath $certPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople|Out-Null
}
$dependencies=@()
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Dependencies') -File){
 $zip=[IO.Compression.ZipFile]::OpenRead($file.FullName)
 try{$reader=[IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open());[xml]$xml=$reader.ReadToEnd();$reader.Dispose()}finally{$zip.Dispose()}
 $existing=Get-AppxPackage -Name $xml.Package.Identity.Name
 if(!$existing -or [version]$existing.Version -lt [version]$xml.Package.Identity.Version){$dependencies+=$file.FullName}
}
$arguments=@{Path=(Join-Path $PSScriptRoot 'HyPlayer-MusicAtlas.msixbundle');ForceApplicationShutdown=$true}
if($dependencies.Count){$arguments.DependencyPath=$dependencies}
if($installed){$volume=Get-AppxVolume|Where-Object {$installed.InstallLocation.StartsWith($_.PackageStorePath,[StringComparison]::OrdinalIgnoreCase)}|Select-Object -First 1;if($volume){$arguments.Volume=$volume}}
Add-AppxPackage @arguments
Write-Output ('Installed HyPlayer Music Atlas '+$meta.Version+'. Existing application data was retained.')
$updated=Get-AppxPackage -Name '48848aaaaaaccd.HyPlayer'
if(!$updated -or [version]$updated.Version -ne [version]$meta.Version){throw 'Unable to verify installed Music Atlas version'}
$stateFolder=Join-Path $env:LOCALAPPDATA ('Packages\'+$updated.PackageFamilyName+'\LocalState')
$statePath=Join-Path $stateFolder 'music-atlas-state-v4.json'
if(!(Test-Path -LiteralPath $statePath)){
 New-Item -ItemType Directory -Path $stateFolder -Force|Out-Null
 $temporary=Join-Path $stateFolder ('music-atlas-initial-'+[guid]::NewGuid().ToString('N')+'.tmp')
 try{
  [IO.File]::WriteAllText($temporary,$classifications,(New-Object Text.UTF8Encoding($false)))
  try{[IO.File]::Move($temporary,$statePath);Write-Output 'Loaded the published manual classifications. They are ready when Music Atlas opens.'}
  catch{
   if(!(Test-Path -LiteralPath $statePath)){throw}
   Write-Output 'An existing classification file was detected and retained. To explicitly import the published set, close HyPlayer and run:'
   Write-Output 'powershell -ExecutionPolicy Bypass -File .\Transfer-AtlasSettings.ps1 -Mode Import -Path .\classifications.json'
  }
 }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}}
}else{
 Write-Output 'Existing classifications were retained. To explicitly import the published set, close HyPlayer and run:'
 Write-Output 'powershell -ExecutionPolicy Bypass -File .\Transfer-AtlasSettings.ps1 -Mode Import -Path .\classifications.json'
}
