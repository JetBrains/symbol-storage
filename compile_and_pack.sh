#!/bin/bash

PROJECT_FILE="Common.targets"
PUBLISH_DIR="publish"

# FRAMEWORK=$(xmllint --xpath "string(//Project/PropertyGroup/TargetFramework)" $PROJECT_FILE)
# PACKAGE_VERSION=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" $PROJECT_FILE)

FRAMEWORK=$(sed -n 's/.*<TargetFramework>\(.*\)<\/TargetFramework>.*/\1/p' $PROJECT_FILE)
PACKAGE_VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' $PROJECT_FILE)

echo "Framework: $FRAMEWORK"
echo "PackageVersion: $PACKAGE_VERSION"
echo "Publish directory: $PUBLISH_DIR"

packNuget() {
  NAME=$1
  RUNTIME=$2

  FILE="<?xml version=\"1.0\" encoding=\"utf-8\"?>
<package>
  <metadata>
    <id>JetBrains.SymbolStorage.$NAME.$RUNTIME</id>
    <version>$PACKAGE_VERSION</version>
    <title>JetBrains SymbolStorage $NAME</title>
    <authors>Mikhail Pilin</authors>
    <copyright>Copyright © 2020-$(date +'%Y') JetBrains s.r.o.</copyright>
    <projectUrl>https://github.com/JetBrains/symbol-storage</projectUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <license type=\"expression\">MIT</license>
    <description>JetBrains SymbolStorage $NAME</description>
  </metadata>
  <files>
    <file src=\"$NAME\\$RUNTIME\\**\\*\" target=\"tools\\$RUNTIME\" />
  </files>
</package>"

  NU_SPEC="$PUBLISH_DIR/Package.$NAME.$RUNTIME.nuspec"
  echo "$FILE" > $NU_SPEC
  nuget pack $NU_SPEC -OutputDirectory "$PUBLISH_DIR/nuget/"
  if [ $? -ne 0 ]; then
    echo "Nuget exited with error"
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
  zip -r "$LOCATION/$PUBLISH_DIR/archive/JetBrains.SymbolStorage.$NAME.$RUNTIME.zip" .
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

  dotnet publish -f $FRAMEWORK -r $RUNTIME -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "$PUBLISH_DIR/Manager/$RUNTIME" Manager
  dotnet publish -f $FRAMEWORK -r $RUNTIME -c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "$PUBLISH_DIR/Uploader/$RUNTIME" Uploader
  packNuget Manager $RUNTIME
  packNuget Uploader $RUNTIME
  packArchive $ARCH_TYPE Manager $RUNTIME
  packArchive $ARCH_TYPE Uploader $RUNTIME
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
