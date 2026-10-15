# AscEmu Launcher

Native AscEmu launcher project for the Forever client. The project owns its UI, configuration, build profiles and Forever patch flow under the `AscEmu.Launcher` namespace.

## Installation

`AscEmu Launcher.exe` is intentionally designed to live directly beside `WowB.exe`.

```text
_classic_beta_/
  WowB.exe
  AscEmu Launcher.exe
  WTF/
    Config.wtf
```

The launcher does not store or search for a World of Warcraft path. It always uses `WowB.exe` from its own directory and reports a clear error if the executable is missing.


## Current Forever profile

The runtime client patch is intentionally verified only for Forever build **69893-70205**. Builds 70009, 70124 and 70205 are recognized by the UI, but direct memory patches are not silently inherited until they are proven compatible.

For build 69893-70205 the launcher:

- writes the configured Battle.net portal to `WTF/Config.wtf`;
- launches WoW with `-config Config.wtf` (the client resolves that name inside `WTF`, matching the known-good launcher behavior);
- uses the known-good Forever `none` launch path: no pre-launch portal/registry/GameCrypto memory patches are applied;
- starts `WowB.exe` suspended and applies the verified 69893-70205 startup patch;
- patches the embedded portal suffix, launcher registry path and AscEmu Ed25519 public key;
- resumes the client;
- scans for the runtime `RegionGroup 1..12` table;
- replaces all twelve 32-byte region public keys when the table becomes available;
- keeps monitoring and re-applies them if the client rebuilds the table;
- plays a Windows notification sound after a successful region-key patch.

## Build requirements

- Windows 10/11 x64
- Visual Studio 2022 with CMake tools (optional but recommended)
- CMake 3.24+
- .NET 8 SDK

## Build with CMake

From a Visual Studio Developer Command Prompt:

```bat
configure.bat
build.bat
```

Publish a standalone single-file launcher:

```bat
publish.bat
```

Published output:

```text
build\publish\ascemu_launcher\
```

Or manually:

```bat
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release --target ascemu_launcher_publish
```

## Settings

Settings are stored outside the source tree at:

```text
%LOCALAPPDATA%\AscEmu\Launcher\launcher.json
```

This prevents CMake/build operations from overwriting user settings.

## account UI / launcher backend

The launcher already has a account UI area and a direct TLS reachability check against the configured Battle.net endpoint. A real account UI should come from an authenticated AscEmu launcher API on `bnetserver`; it is intentionally not read directly from the database or fabricated locally.

## Build note

The project uses `Microsoft.NET.Sdk` with `<UseWPF>true</UseWPF>` and explicit global .NET usings. This keeps the WPF launcher compatible with current .NET SDKs (including newer SDKs that can target .NET 8) without relying on the deprecated `Microsoft.NET.Sdk.WindowsDesktop` root SDK declaration.

## Battle.net availability

The launcher checks the configured Battle.net service every 10 seconds. The PLAY button is enabled only when WowB.exe is present, the Forever build is supported, and Battle.net is reachable. If the service goes offline, PLAY is disabled automatically and becomes available again when the service recovers.
