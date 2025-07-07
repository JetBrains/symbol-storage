if ($PSVersionTable.PSVersion.Major -lt 3) {
	throw "PS Version $($PSVersionTable.PSVersion) is below 3.0."
}

Set-StrictMode -Version Latest
$ErrorActionPreference=[System.Management.Automation.ActionPreference]::Stop
$ProgressPreference="SilentlyContinue"


[xml]$Project=Get-Content Common.targets
$Framework=$Project.Project.PropertyGroup.TargetFramework
$PackageVersion=$Project.Project.PropertyGroup.Version
$PublishDir="$PSScriptRoot\publish"

$DotNetVersion="9.0"
$DotNetCustomInstallationDir="$env:LOCALAPPDATA\JetBrains\dotnet-sdk-temp\1e63e382e732473eab7845c59486bf30"
$DotNet="$DotNetCustomInstallationDir\dotnet.exe"

Write-Host "Framework:" $Framework
Write-Host "PackageVersion:" $PackageVersion
Write-Host "Publish directory:" $PublishDir


function installDotNet() {
  try {
    $DotNetInstalled=(Get-Command dotnet).Path
    if ((. $DotNetInstalled --list-sdks) -match "^$([Regex]::Escape($DotNetVersion))") {
      Write-Host "System .NET $DotNetVersion will be used (location: $DotNetInstalled)"
      return $DotNetInstalled
    }
  } catch {
  }
  
  Write-Host ".NET $DotNetVersion will be installed (location: $DotNetCustomInstallationDir)"
  if(!(Test-Path -PathType Container "$PublishDir"))
  {
    New-Item -ItemType Directory -Path "$PublishDir"
  }
  Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile "$PublishDir\dotnet-install.ps1"
  if (([regex]::Matches($DotNetVersion, "\." )).count -le 1) {
    . "$PublishDir\dotnet-install.ps1" -InstallDir $DotNetCustomInstallationDir -Channel $DotNetVersion -NoPath
  } else {
    . "$PublishDir\dotnet-install.ps1" -InstallDir $DotNetCustomInstallationDir -Version $DotNetVersion -NoPath
  }
  return $DotNet
}


function packNuget($Name, $Runtime) {
  $Template= Get-Content -Encoding UTF8 "NugetPackProjectTemplate.csproj.template"
  $Template = $Template -replace "{{ROOT_PATH}}", ".."
  $Template = $Template -replace "{{NAME}}", $Name
  $Template = $Template -replace "{{RUNTIME}}", $Runtime
  $Template = $Template -replace "{{CURRENT_YEAR}}", $(get-date -Format yyyy)

  $CsprojSpec="$PublishDir\Package.$Name.$Runtime.csproj"
  Out-File -InputObject $Template -Encoding utf8 $CsprojSpec
  . $DotNet pack $CsprojSpec --output "$PublishDir\nuget\" --artifacts-path "$PublishDir\NugetBuild\$Name\$Runtime\" 
  
  if (0 -ne $LastExitCode) {
    throw "dotnet pack exited with error"
  }
}

function packZipArchive($Name, $Runtime) {
  if(!(Test-Path -PathType Container "$PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$PublishDir\archive\"
  }
  Compress-Archive -Path "$PublishDir\$Name\$Runtime\*" -DestinationPath "$PublishDir\archive\JetBrains.SymbolStorage.$Name.$Runtime.zip"
}

function packTarArchive($Name, $Runtime) {
  If(!(Test-Path -PathType Container "$PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$PublishDir\archive\"
  }
  Write-Host "$PublishDir\$Name\$Runtime\"
  
  $Location= Get-Location
  Push-Location
  cd "$PublishDir\$Name\$Runtime"
  tar -czvf "$PublishDir\archive\JetBrains.SymbolStorage.$Name.$Runtime.tar.gz" "."
  Pop-Location
  
  if (0 -ne $LastExitCode) {
    throw "Tar exited with error"
  }
}

function packArchive($ArchType, $Name, $Runtime) {
  switch ($ArchType) {
    "tar" { packTarArchive $Name $Runtime }
    "zip" { packZipArchive $Name $Runtime }
    default { throw "Unknown archive type" }
  }
}

function compileAndPack($Runtime, $ArchType) {
  Write-Host "Compile and pack for $Runtime"

  . $DotNet publish -f $Framework -r $Runtime -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PublishDir\Manager\$Runtime" Manager
  . $DotNet publish -f $Framework -r $Runtime -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PublishDir\Uploader\$Runtime" Uploader
  packNuget Manager $Runtime
  packNuget Uploader $Runtime
  packArchive $ArchType Manager $Runtime
  packArchive $ArchType Uploader $Runtime
}


$DotNet = installDotNet

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
