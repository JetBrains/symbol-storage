param(
    [ValidateSet("All", "Manager", "Uploader")]
    [string]$Project="All",
    [string[]]$Runtimes=@(),
    [ValidateSet("All", "Build", "Test", "Pack", "PackNuget", "PackArchive")]
    [string]$Action="All"
)

if ($PSVersionTable.PSVersion.Major -lt 3) {
	throw "PS Version $($PSVersionTable.PSVersion) is below 3.0."
}

Set-StrictMode -Version Latest
$ErrorActionPreference=[System.Management.Automation.ActionPreference]::Stop
$ProgressPreference="SilentlyContinue"

# Fix array parameters when script executed from cmd
if (($Runtimes) -and ($Runtimes.Length -eq 1) -and $Runtimes[0].Contains(",")) {
  $Runtimes=$Runtimes[0].Split(',')
}


[xml]$ProjectContent=Get-Content Common.targets
$Framework=$ProjectContent.Project.PropertyGroup.TargetFramework
$PackageVersion=$ProjectContent.Project.PropertyGroup.Version
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


function packNuget($Project, $Runtime) {
  $Template= Get-Content -Encoding UTF8 "NugetPackProjectTemplate.csproj.template"
  $Template = $Template -replace "{{ROOT_PATH}}", ".."
  $Template = $Template -replace "{{NAME}}", $Project
  $Template = $Template -replace "{{RUNTIME}}", $Runtime
  $Template = $Template -replace "{{CURRENT_YEAR}}", $(get-date -Format yyyy)

  $CsprojSpec="$PublishDir\Package.$Project.$Runtime.csproj"
  Out-File -InputObject $Template -Encoding utf8 $CsprojSpec
  . $DotNet pack $CsprojSpec --output "$PublishDir\nuget\" --artifacts-path "$PublishDir\NugetBuild\$Project\$Runtime\" 
  
  if (0 -ne $LastExitCode) {
    throw "dotnet pack exited with error"
  }
}

function packZipArchive($Project, $Runtime) {
  if(!(Test-Path -PathType Container "$PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$PublishDir\archive\"
  }
  Compress-Archive -Path "$PublishDir\$Project\$Runtime\*" -DestinationPath "$PublishDir\archive\JetBrains.SymbolStorage.$Project.$Runtime.zip" -Force
}

function packTarArchive($Project, $Runtime) {
  If(!(Test-Path -PathType Container "$PublishDir\archive\"))
  {
    New-Item -ItemType Directory -Path "$PublishDir\archive\"
  }
  Write-Host "$PublishDir\$Project\$Runtime\"
  
  $Location= Get-Location
  Push-Location
  cd "$PublishDir\$Project\$Runtime"
  tar -czvf "$PublishDir\archive\JetBrains.SymbolStorage.$Project.$Runtime.tar.gz" "."
  Pop-Location
  
  if (0 -ne $LastExitCode) {
    throw "Tar exited with error"
  }
}

function packArchive($ArchiveType, $Project, $Runtime) {
  switch ($ArchiveType) {
    "tar" { packTarArchive $Project $Runtime }
    "zip" { packZipArchive $Project $Runtime }
    default { throw "Unknown archive type" }
  }
}

function compileProject($Project, $Runtime) {
  Write-Host "Compile $Project for $Runtime"
  . $DotNet publish -f $Framework -r $Runtime -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PublishDir\$Project\$Runtime" $Project
}

function runAllTests() {
  Write-Host "Run all tests"
  . $DotNet test -f $Framework
  if (0 -ne $LastExitCode) {
    throw "Tests failed"
  }
  Write-Host ""
}

function packProjectToNuget($Project, $Runtime) {
  Write-Host "Pack $Project nuget for $Runtime"
  packNuget $Project $Runtime
}

function packProjectToArchive($Project, $Runtime, $ArchiveType) {
  if (-not $ArchiveType) {
    if ($Runtime.StartsWith("win-")) {
      $ArchiveType="zip"
    } else {
      $ArchiveType="tar"
    }
  }
  Write-Host "Pack $Project for $Runtime into $ArchiveType archive"
  packArchive $ArchiveType $Project $Runtime
}


function processProjectOnRuntime($Project, $Runtime, $Action) {
  $ProcessedByAnyStep=$false
  if (($Action -eq "All") -or ($Action -eq "Build")) {
    compileProject $Project $Runtime
    $ProcessedByAnyStep=$true
  }
  if (($Action -eq "All") -or ($Action -eq "Pack") -or ($Action -eq "PackNuget")) {
    packProjectToNuget $Project $Runtime
    $ProcessedByAnyStep=$true
  }
  if (($Action -eq "All") -or ($Action -eq "Pack") -or ($Action -eq "PackArchive")) {
    packProjectToArchive $Project $Runtime
    $ProcessedByAnyStep=$true
  }
  
  if ($ProcessedByAnyStep) {
    Write-Host "$Project for $Runtime processed"
  }
}


if (($Action -eq "All") -or ($Action -eq "Build") -or ($Action -eq "Test") -or ($Action -eq "Pack") -or ($Action -eq "PackNuget")) {
  $DotNet = installDotNet
}



$TargetRuntimes=@(
  "linux-arm", "linux-arm64", "linux-x64", "linux-musl-arm", "linux-musl-arm64", "linux-musl-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64", "win-x86"
)
if (($Runtimes) -and ($Runtimes.Length -gt 0) -and ($Runtimes[0] -ne "All")) {
  $TargetRuntimes=$Runtimes
}

$TargetProjects=@(
  "Manager", "Uploader"
)
if (($Project) -and ($Project -ne "All")) {
  $TargetProjects=@($Project)
}


if (($Action -eq "All") -or ($Action -eq "Test")) {
  runAllTests
}

foreach ($CurRuntime in $TargetRuntimes) {
  foreach ($CurProject in $TargetProjects) {
    processProjectOnRuntime $CurProject $CurRuntime $Action
  }
}
