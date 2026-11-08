param([Parameter(Mandatory=$true)][ValidateSet('Export','Import')][string]$Mode,[Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
$package=Get-AppxPackage -Name '48848aaaaaaccd.HyPlayer'
if(!$package){throw 'HyPlayer is not installed for the current user'}
$folder=Join-Path $env:LOCALAPPDATA ('Packages\'+$package.PackageFamilyName+'\LocalState')
$state=Join-Path $folder 'music-atlas-state-v4.json'
if($Mode -eq 'Export'){
 if(!(Test-Path -LiteralPath $state)){throw 'Open Music Atlas once before exporting'}
 if(Test-Path -LiteralPath $Path){throw 'Destination already exists; choose a new filename'}
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Path),[IO.File]::ReadAllText($state))
 Write-Output 'Exported personal classifications only; no login data or playback cache.'
}else{
 if(Get-Process HyPlayer -ErrorAction SilentlyContinue){throw 'Close HyPlayer before importing classifications'}
 $text=[IO.File]::ReadAllText([IO.Path]::GetFullPath($Path));$data=$text|ConvertFrom-Json
 if($data.schema -ne 4 -or !$data.categories -or $null -eq $data.overrides){throw 'Not a Music Atlas v4 classification file'}
 if(Test-Path -LiteralPath $state){[IO.File]::WriteAllText(($state+'.before-import-'+(Get-Date -Format 'yyyyMMddHHmmss')),[IO.File]::ReadAllText($state))}
 [IO.File]::WriteAllText(($state+'.tmp'),$text)
 Move-Item -LiteralPath ($state+'.tmp') -Destination $state -Force
 Write-Output 'Imported classifications. They will load when HyPlayer next opens Music Atlas.'
}
