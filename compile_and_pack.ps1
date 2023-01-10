if ($PSVersionTable.PSVersion.Major -lt 3) {
	throw "PS Version $($PSVersionTable.PSVersion) is below 3.0."
}

Set-StrictMode -Version Latest
$ErrorActionPreference=[System.Management.Automation.ActionPreference]::Stop


[xml]$_Project=Get-Content Common.targets
$_Framework=$_Project.Project.PropertyGroup.TargetFramework
$_PackageVersion=$_Project.Project.PropertyGroup.Version

Write-Host "Framework:" $_Framework
Write-Host "PackageVersion:" $_PackageVersion

function pack($_Name, $_Runtime) {
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
    <file src="' + $_Name + '\bin\Release\' + $_Framework + '\' + $_Runtime + '\publish\**\*" target="tools\' + $_Runtime + '" />
  </files>
</package>'

  $_NuSpec="Package.$_Name.$_Runtime.nuspec"
  Out-File -InputObject $_File -Encoding utf8 $_NuSpec
  nuget pack $_NuSpec
}

function compileAndPack($_Runtime) {
  dotnet publish -f $_Framework -r $_Runtime -c Release --self-contained true -p:PublishTrimmed=true SymbolStorage.sln
  pack Manager $_Runtime
  pack Uploader $_Runtime
}

compileAndPack "linux-arm"
compileAndPack "linux-arm64"
compileAndPack "linux-x64"
compileAndPack "linux-musl-arm"
compileAndPack "linux-musl-arm64"
compileAndPack "linux-musl-x64"
compileAndPack "osx-arm64"
compileAndPack "osx-x64"
compileAndPack "win-arm64"
compileAndPack "win-x64"
compileAndPack "win-x86"
