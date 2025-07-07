#!/bin/bash

PROJECT_FILE="Common.targets"
PUBLISH_DIR="$PWD/publish"

DOTNET_VERSION="9.0"
DOTNET_CUSTOM_INSTALLATION_DIR="$HOME/.local/share/JetBrains/dotnet-sdk-temp/1e63e382e732473eab7845c59486bf30"
DOTNET="$DOTNET_CUSTOM_INSTALLATION_DIR/dotnet"


# FRAMEWORK=$(xmllint --xpath "string(//Project/PropertyGroup/TargetFramework)" $PROJECT_FILE)
# PACKAGE_VERSION=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" $PROJECT_FILE)

FRAMEWORK=$(sed -n 's/.*<TargetFramework>\(.*\)<\/TargetFramework>.*/\1/p' $PROJECT_FILE)
PACKAGE_VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' $PROJECT_FILE)

echo "Framework: $FRAMEWORK"
echo "PackageVersion: $PACKAGE_VERSION"
echo "Publish directory: $PUBLISH_DIR"


installDotnet() {
  # Check if dotnet is installed and matches the desired version
  if command -v dotnet &> /dev/null; then
    DOTNET_INSTALLED=$(command -v dotnet)
    INSTALLED_VERSION=$($DOTNET_INSTALLED --list-sdks | grep -E "^$DOTNET_VERSION" || true)
    if [[ -n "$INSTALLED_VERSION" ]]; then
      echo "System .NET $DOTNET_VERSION will be used (location: $DOTNET_INSTALLED)"
      DOTNET=$DOTNET_INSTALLED
      return
    fi
  fi

  # If not installed, proceed with installation
  echo ".NET $DOTNET_VERSION will be installed (location: $DOTNET_CUSTOM_INSTALLATION_DIR)"
  mkdir -p "$PUBLISH_DIR"
  # wget -O "$PUBLISH_DIR/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  curl -L https://dot.net/v1/dotnet-install.sh -o "$PUBLISH_DIR/dotnet-install.sh"
  if [ $? -ne 0 ]; then
    echo "curl exited with error"
    exit 1
  fi
  if [[ $(echo "$DOTNET_VERSION" | grep -o "\." | wc -l) -le 1 ]]; then
    bash "$PUBLISH_DIR/dotnet-install.sh" --install-dir "$DOTNET_CUSTOM_INSTALLATION_DIR" --channel "$DOTNET_VERSION" --no-path
  else
    bash "$PUBLISH_DIR/dotnet-install.sh" --install-dir "$DOTNET_CUSTOM_INSTALLATION_DIR" --version "$DOTNET_VERSION" --no-path
  fi
}


packNuget() {
  NAME=$1
  RUNTIME=$2
  
  TEMPLATE_FILE="NugetPackProjectTemplate.csproj.template"
  CSPROJ_SPEC="$PUBLISH_DIR/Package.$NAME.$RUNTIME.csproj"
  
  sed -e "s|{{ROOT_PATH}}|..|g" \
      -e "s|{{NAME}}|$NAME|g" \
      -e "s|{{RUNTIME}}|$RUNTIME|g" \
      -e "s|{{CURRENT_YEAR}}|$(date +'%Y')|g" \
      "$TEMPLATE_FILE" > "$CSPROJ_SPEC"  
  
  $DOTNET pack $CSPROJ_SPEC --output "$PUBLISH_DIR/nuget/" --artifacts-path "$PUBLISH_DIR/NugetBuild/$NAME/$RUNTIME/" 
  if [ $? -ne 0 ]; then
    echo "dotnet pack exited with error"
    exit 1
  fi
}

packZipArchive() {
  NAME=$1
  RUNTIME=$2

  if [ ! -d "$PUBLISH_DIR/archive/" ]; then
    mkdir -p "$PUBLISH_DIR/archive/"
  fi
  LOCATION=$PWD
  pushd $LOCATION
  cd "$PUBLISH_DIR/$NAME/$RUNTIME/"
  zip -r "$PUBLISH_DIR/archive/JetBrains.SymbolStorage.$NAME.$RUNTIME.zip" .
  popd
  if [ $? -ne 0 ]; then
    echo "Zip exited with error"
    exit 1
  fi
}

packTarArchive() {
  NAME=$1
  RUNTIME=$2

  if [ ! -d "$PUBLISH_DIR/archive/" ]; then
    mkdir -p "$PUBLISH_DIR/archive/"
  fi
  tar -czvf "$PUBLISH_DIR/archive/JetBrains.SymbolStorage.$NAME.$RUNTIME.tar.gz" -C "$PUBLISH_DIR/$NAME/$RUNTIME" .
  if [ $? -ne 0 ]; then
    echo "Tar exited with error"
    exit 1
  fi
}

packArchive() {
  ARCH_TYPE=$1
  NAME=$2
  RUNTIME=$3

  case $ARCH_TYPE in
    "tar") 
      packTarArchive $NAME $RUNTIME
      ;;
    "zip")
      packZipArchive $NAME $RUNTIME
      ;;
    *)
      echo "Unknown archive type"
      exit 1
      ;;
  esac
}

compileAndPack() {
  RUNTIME=$1
  ARCH_TYPE=$2

  $DOTNET publish -f $FRAMEWORK -r $RUNTIME -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PUBLISH_DIR/Manager/$RUNTIME" Manager
  $DOTNET publish -f $FRAMEWORK -r $RUNTIME -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PUBLISH_DIR/Uploader/$RUNTIME" Uploader
  packNuget Manager $RUNTIME
  packNuget Uploader $RUNTIME
  packArchive $ARCH_TYPE Manager $RUNTIME
  packArchive $ARCH_TYPE Uploader $RUNTIME
}


installDotnet

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
