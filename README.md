# JetBrains.SymbolStorage.Manager [![official JetBrains project](https://jb.gg/badges/official.svg)](https://confluence.jetbrains.com/display/ALL/JetBrains+on+GitHub)

[![Build and run tests](https://github.com/JetBrains/symbol-storage/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/JetBrains/symbol-storage/actions/workflows/build-and-test.yml)

This repository contains tools for mantaining the company or private symbol storage in accordance [Simple Symbol Query Protocol (SSQP)](https://github.com/dotnet/symstore/blob/master/docs/specs/Simple_Symbol_Query_Protocol.md) and [SSQP Key Conventions](https://github.com/dotnet/symstore/blob/master/docs/specs/SSQP_Key_Conventions.md).

##### Main features:
- [x] Add metadata for each set of uploaded files
- [x] Storage validation with fix some inconsistency with reference counting and file name checking
- [x] Validate and fix access rights on Amazon S3
- [x] Delete unnecessary files from storage with using some kinds of filtering
- [x] Creating new storage
- [x] Casing support for data files for working in cooperation with Amazon Cloud Front lambdas
- [x] Support Amazon Cloud Front invalidation for updated files
- [x] Uploading one storage to another with consistensy checking
- [x] Gather files on user directories and generate storage for them
- [x] Working with archives
- [x] Generate .symref files to ability to download symbols with scripts

##### Supported storages
- Local filesystem
- Amazon Simple Storage Service (Amazon S3) + Amazon Cloud Front

##### Supported formats
- Portable PDB
- Windows PDB
- Linux debug symbols
- macOS DWARF symbols
- PE binaries
- ELF binaries
- Mach-O binaries

##### Supported platforms (same as .NET 6.0)
- Windows arm64/x64/x86
- Linux Glibc/Musl arm/arm64/x64
- macOS arm64/x64

##### Tested on
- Windows 10 Pro x64 20H2 Build 19042.804
- Ubuntu 18.04.5 LTS 5.4.0-65-generic x86_64
- Ubuntu 20.10 LTS 5.8.0-1011-raspi aarch64
- macOS Big Sur 11.2.1 arm64 + Rosetta2
- macOS Mojave 10.14.4 x64
