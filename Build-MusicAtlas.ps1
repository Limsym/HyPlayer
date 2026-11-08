param([string]$Version='2.1.38.6043',[string]$SdkVersion='10.0.26100.0')
$ErrorActionPreference='Stop'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Install Visual Studio Build Tools with MSBuild, UWP/.NET Native and Windows SDK.'}
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$msbuild){throw 'MSBuild not found'}
& node (Join-Path $PSScriptRoot 'MusicAtlasBuild/build-hyplayer-atlas.cjs')
if($LASTEXITCODE){throw 'Map generation failed'}
$manifest=Join-Path $PSScriptRoot 'HyPlayer/Package.appxmanifest'
$text=[IO.File]::ReadAllText($manifest) -replace 'Version="2\.1\.38\.\d+"',('Version="'+$Version+'"')
[IO.File]::WriteAllText($manifest,$text)
$out=Join-Path $PSScriptRoot 'Artifacts'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$options=@('/p:Configuration=Release','/p:Platform=x64','/p:LangVersion=13.0',('/p:TargetPlatformVersion='+$SdkVersion),'/p:AppxBundle=Never','/p:AppxPackageSigningEnabled=false','/p:AppxAutoIncrementPackageRevision=false','/p:UapAppxPackageBuildMode=SideloadOnly',('/p:AppxPackageDir='+$out+'\'),'/verbosity:minimal','/nologo')
Push-Location $PSScriptRoot
try{
 & $msbuild HyPlayer.sln /t:Restore @options
 if($LASTEXITCODE){throw 'Solution restore failed'}
 & $msbuild HyPlayer.sln /m @options
 if($LASTEXITCODE){throw 'Solution build failed'}
}finally{Pop-Location}
