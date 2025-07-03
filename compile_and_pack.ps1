if ($PSVersionTable.PSVersion.Major -lt 3) {
	throw "PS Version $($PSVersionTable.PSVersion) is below 3.0."
}

Set-StrictMode -Version Latest
$ErrorActionPreference=[System.Management.Automation.ActionPreference]::Stop


[xml]$_Project=Get-Content Common.targets
$_Framework=$_Project.Project.PropertyGroup.TargetFramework
$_PackageVersion=$_Project.Project.PropertyGroup.Version
$_PublishDir="publish"

Write-Host "Framework:" $_Framework
Write-Host "PackageVersion:" $_PackageVersion
Write-Host "Publish directory:" $_PublishDir

function packNuget($_Name, $_Runtime) {
  $_File='<?xml version="1.0" encoding="utf-8"?>
<package>
  <metadata>
    <id>JetBrains.SymbolStorage.' + $_Name + '.' + $_Runtime + '</id>
    <version>' + $_PackageVersion + '</version>
    <title>JetBrains SymbolStorage ' + $_Name + '</title>
    <authors>Mikhail Pilin</authors>
    <copyright>Copyright © 2020-' + $(get-date -Format yyyy) + ' JetBrains s.r.o.</copyright>
    <projectUrl>https://github.com/JetBrains/symbol-storage</projectUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <license type="expression">MIT</license>
    <description>JetBrains SymbolStorage ' + $_Name + '</description>
  </metadata>
  <files>
    <file src="' + $_Name + '\' + $_Runtime + '\**\*" target="tools\' + $_Runtime + '" />
  </files>
</package>'


  $_NuSpec="$_PublishDir\Package.$_Name.$_Runtime.nuspec"
  Out-File -InputObject $_File -Encoding utf8 $_NuSpec
  nuget pack $_NuSpec -OutputDirectory "$_PublishDir\nuget\"
}

function packZipArchive($_Name, $_Runtime) {
  if(!(Test-Path -PathType Container "$_PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$_PublishDir\archive\"
  }
  Compress-Archive -Path "$_PublishDir\$_Name\$_Runtime\*" -DestinationPath "$_PublishDir\archive\JetBrains.SymbolStorage.$_Name.$_Runtime.zip"
}

function packTarArchive($_Name, $_Runtime) {
  If(!(Test-Path -PathType Container "$_PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$_PublishDir\archive\"
  }
  Write-Host "$_PublishDir\$_Name\$_Runtime\"
  
  $_Location= Get-Location
  Push-Location
  cd "$_PublishDir\$_Name\$_Runtime"
  tar -czvf "$_Location\$_PublishDir\archive\JetBrains.SymbolStorage.$_Name.$_Runtime.tar.gz" "."
  Pop-Location
  
  if (0 -ne $LastExitCode) {
    throw "Tar exited with error"
  }
}

function packArchive($_ArchType, $_Name, $_Runtime) {
  switch ($_ArchType) {
    "tar" { packTarArchive $_Name $_Runtime }
	"zip" { packZipArchive $_Name $_Runtime }
	default { throw "Unknown archive type" }
  }
}

function compileAndPack($_Runtime, $_ArchType) {
  dotnet publish -f $_Framework -r $_Runtime -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "$_PublishDir\Manager\$_Runtime" Manager
  dotnet publish -f $_Framework -r $_Runtime -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "$_PublishDir\Uploader\$_Runtime" Uploader
  packNuget Manager $_Runtime
  packNuget Uploader $_Runtime
  packArchive $_ArchType Manager $_Runtime
  packArchive $_ArchType Uploader $_Runtime
}

compileAndPack "linux-arm" "tar"
compileAndPack "linux-arm64" "tar"
compileAndPack "linux-x64" "tar"
compileAndPack "linux-musl-arm" "tar"
compileAndPack "linux-musl-arm64" "tar"
compileAndPack "linux-musl-x64" "tar"
compileAndPack "osx-arm64" "tar"
compileAndPack "osx-x64" "tar"
compileAndPack "win-arm64" "zip"
compileAndPack "win-x64" "zip"
compileAndPack "win-x86" "zip"
