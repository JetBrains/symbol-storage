#!/bin/bash

# Parameters
PROJECT="All"
RUNTIMES=()
ACTION="All"

# Parsing arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2 ;;
    --runtimes) IFS=',' read -ra RUNTIMES <<< "$2"; shift 2 ;;
    --action) ACTION="$2"; shift 2 ;;
    *) echo "Usage: $0 --project PROJECT --runtimes RUNTIME1,RUNTIME2 --action ACTION"; exit 1 ;;
  esac
done


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
  local PROJECT=$1
  local RUNTIME=$2
  
  local TEMPLATE_FILE="NugetPackProjectTemplate.csproj.template"
  local CSPROJ_SPEC="$PUBLISH_DIR/Package.$PROJECT.$RUNTIME.csproj"
  
  sed -e "s|{{ROOT_PATH}}|..|g" \
      -e "s|{{NAME}}|$PROJECT|g" \
      -e "s|{{RUNTIME}}|$RUNTIME|g" \
      -e "s|{{CURRENT_YEAR}}|$(date +'%Y')|g" \
      "$TEMPLATE_FILE" > "$CSPROJ_SPEC"  
  
  $DOTNET pack $CSPROJ_SPEC --output "$PUBLISH_DIR/nuget/" --artifacts-path "$PUBLISH_DIR/NugetBuild/$PROJECT/$RUNTIME/" 
  if [ $? -ne 0 ]; then
    echo "dotnet pack exited with error"
    exit 1
  fi
}

packZipArchive() {
  local PROJECT=$1
  local RUNTIME=$2

  if [ ! -d "$PUBLISH_DIR/archive/" ]; then
    mkdir -p "$PUBLISH_DIR/archive/"
  fi
  LOCATION=$PWD
  pushd $LOCATION
  cd "$PUBLISH_DIR/$PROJECT/$RUNTIME/"
  zip -r "$PUBLISH_DIR/archive/JetBrains.SymbolStorage.$PROJECT.$RUNTIME.zip" .
  popd
  if [ $? -ne 0 ]; then
    echo "Zip exited with error"
    exit 1
  fi
}

packTarArchive() {
  local PROJECT=$1
  local RUNTIME=$2

  if [ ! -d "$PUBLISH_DIR/archive/" ]; then
    mkdir -p "$PUBLISH_DIR/archive/"
  fi
  tar -czvf "$PUBLISH_DIR/archive/JetBrains.SymbolStorage.$PROJECT.$RUNTIME.tar.gz" -C "$PUBLISH_DIR/$PROJECT/$RUNTIME" .
  if [ $? -ne 0 ]; then
    echo "Tar exited with error"
    exit 1
  fi
}

packArchive() {
  local ARCHIVE_TYPE=$1
  local PROJECT=$2
  local RUNTIME=$3

  case $ARCHIVE_TYPE in
    "tar") 
      packTarArchive $PROJECT $RUNTIME
      ;;
    "zip")
      packZipArchive $PROJECT $RUNTIME
      ;;
    *)
      echo "Unknown archive type"
      exit 1
      ;;
  esac
}


compileProject() {
  local PROJECT="$1"
  local RUNTIME="$2"
  echo "Compile $PROJECT for $RUNTIME"
  $DOTNET publish -f $FRAMEWORK -r $RUNTIME -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -warnAsMessage:IL2104 -o "$PUBLISH_DIR/$PROJECT/$RUNTIME" $PROJECT
}

runAllTests() {
  echo "Run all tests"
  $DOTNET test -f $FRAMEWORK
  if [ $? -ne 0 ]; then
    echo "Tests failed"
    exit 1
  fi
  echo ""
}

packProjectToNuget() {
  local PROJECT="$1"
  local RUNTIME="$2"
  echo "Pack $PROJECT nuget for $RUNTIME"
  packNuget $PROJECT $RUNTIME
}

packProjectToArchive() {
  local PROJECT="$1"
  local RUNTIME="$2"
  local ARCHIVE_TYPE="$3"

  if [ -z "$ARCHIVE_TYPE" ]; then
    if [[ "$RUNTIME" == win-* ]]; then
      ARCHIVE_TYPE="zip"
    else
      ARCHIVE_TYPE="tar"
    fi
  fi

  echo "Pack $PROJECT for $RUNTIME into $ARCHIVE_TYPE archive"
  packArchive $ARCHIVE_TYPE $PROJECT $RUNTIME
}

processProjectOnRuntime() {
  local PROJECT="$1"
  local RUNTIME="$2"
  local ACTION="$3"
  local processedByAnyStep=false

  if [[ "$ACTION" == "All" || "$ACTION" == "Build" ]]; then
    compileProject "$PROJECT" "$RUNTIME"
    processedByAnyStep=true
  fi
  if [[ "$ACTION" == "All" || "$ACTION" == "Pack" || "$ACTION" == "PackNuget" ]]; then
    packProjectToNuget $PROJECT $RUNTIME
    processedByAnyStep=true
  fi
  if [[ "$ACTION" == "All" || "$ACTION" == "Pack" || "$ACTION" == "PackArchive" ]]; then
    packProjectToArchive $PROJECT $RUNTIME ""
    processedByAnyStep=true
  fi

  if [[ "$processedByAnyStep" == true ]]; then
    echo "$PROJECT for $RUNTIME processed"
  fi
}


# Main script logic

if [[ "$ACTION" == "All" || "$ACTION" == "Build" || "$ACTION" == "Test" || "$ACTION" == "Pack" || "$ACTION" == "PackNuget" ]]; then
  installDotnet
fi

TARGET_RUNTIMES=("linux-arm" "linux-arm64" "linux-x64" "linux-musl-arm" "linux-musl-arm64" "linux-musl-x64" "osx-arm64" "osx-x64" "win-arm64" "win-x64" "win-x86")
if [[ "$RUNTIMES" != "All" && -n "$RUNTIMES" && ${#RUNTIMES[@]} -gt 0 && "${RUNTIMES[0]}" != "All" ]]; then
  TARGET_RUNTIMES=("${RUNTIMES[@]}")
fi

TARGET_PROJECTS=("Manager" "Uploader")
if [[ "$PROJECT" != "All" && -n "$PROJECT" ]]; then
  TARGET_PROJECTS=("$PROJECT")
fi

if [[ "$ACTION" == "All" || "$ACTION" == "Test" ]]; then
  runAllTests
fi

for CUR_RUNTIME in "${TARGET_RUNTIMES[@]}"; do
  for CUR_PROJECT in "${TARGET_PROJECTS[@]}"; do
    processProjectOnRuntime $CUR_PROJECT $CUR_RUNTIME $ACTION
  done
done

