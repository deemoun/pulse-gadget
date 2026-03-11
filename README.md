# PulseGadget

PulseGadget is a standalone .NET 8 CLI utility for patching Android APKs with Frida Gadget.

## Features

- Decompile and rebuild APKs using apktool
- Auto-download and cache required tooling:
  - `apktool`
  - `uber-apk-signer`
  - Frida Gadget binaries
- Architecture resolution:
  - explicit `--arch`
  - ADB detection
  - APK native library inspection fallback
- Manifest patching and smali gadget injection
- Optional APK signing
- Cross-platform CLI design (Linux/macOS/Windows)

## Requirements

- Runtime:
  - `java` available in `PATH` (required for apktool and signing)
- Build from source:
  - .NET SDK 8.0+

## Project layout

- `src/PulseGadget.Core` - reusable patching logic
- `src/PulseGadget.Cli` - CLI entrypoint
- `scripts/` - publish helper scripts

## Build

```bash
dotnet build PulseGadget.sln -c Release
```

Run from source:

```bash
dotnet run --project src/PulseGadget.Cli -- patch ./app.apk
```

## Publish single-file artifacts

Linux/macOS (bash script):

```bash
./scripts/publish-single-cli.sh --rid linux-x64
./scripts/publish-single-cli.sh --rid osx-arm64
```

Windows (PowerShell script):

```powershell
pwsh ./scripts/publish-single-cli-win.ps1
pwsh ./scripts/publish-single-cli-win.ps1 -Rid win-arm64
```

Artifacts are written to:

- `artifacts/cli/<rid>/pulse-gadget`
- `artifacts/cli/<rid>/pulse-gadget.exe` (Windows)

## CLI commands

```text
pulse-gadget patch <apk-path> [options]
pulse-gadget inspect <apk-path> [options]
pulse-gadget doctor
pulse-gadget tools install [options]
```

## Common usage

Patch an APK:

```bash
./pulse-gadget patch ./app.apk
```

Patch and sign:

```bash
./pulse-gadget patch ./app.apk --sign
```

Patch with explicit architecture and Frida version:

```bash
./pulse-gadget patch ./app.apk --arch arm64 --frida-version 17.8.0 --sign
```

Patch with custom output path:

```bash
./pulse-gadget patch ./app.apk --output ./out/app-patched.apk
```

Inspect APK metadata and architecture hints:

```bash
./pulse-gadget inspect ./app.apk
```

Check environment and cache:

```bash
./pulse-gadget doctor
```

Pre-download tools/artifacts:

```bash
./pulse-gadget tools install --all
./pulse-gadget tools install --frida-version 17.8.0 --arch arm64
```

## Patch options (key)

- `--arch <arm64|arm|x86|x86_64>`
- `--frida-version <version>`
- `--main-activity <full.activity.Name>`
- `--sign`
- `--output <apk-path>`
- `--apktool-path <command-or-path>`
- `--gadget-name <name>` (alias: `--custom-gadget-name`)
- `--config <path>`
- `--js <path>`
- `--js-delay <seconds>`
- `--force-manifest`
- `--no-res`
- `--use-aapt2`
- `--decompile-opts <args>`
- `--recompile-opts <args>`
- `--ks <path> --ks-alias <alias> --ks-pass <pass> --ks-key-pass <pass>`

## Notes

- Tool downloads are cached under your local application data directory.
- If architecture cannot be detected automatically, pass `--arch` explicitly.
- If signing is skipped, the patched APK is left unsigned.
