# PulseGadget .NET 8 Utility

PulseGadget is a standalone C#/.NET 8 CLI utility for APK patching.

## Solution layout

- `PulseGadget.sln`
- `src/PulseGadget.Core`
  - Reusable patching logic and services
- `src/PulseGadget.Cli`
  - CLI entrypoint and command parsing

## Implemented commands

- `patch <apk-path> [options]`
- `inspect <apk-path> [options]`
- `doctor`
- `tools install [options]`

## Key patch options

- `--arch`
- `--frida-version`
- `--main-activity`
- `--sign`
- `--output`
- `--apktool-path`
- `--gadget-name` (alias: `--custom-gadget-name`)
- `--config`
- `--js`
- `--js-delay`
- `--force-manifest`
- `--no-res`
- `--use-aapt2`
- `--decompile-opts`
- `--recompile-opts`
- `--ks`, `--ks-alias`, `--ks-pass`, `--ks-key-pass`

## Build and run

```bash
dotnet build PulseGadget.sln

dotnet run --project src/PulseGadget.Cli -- patch ./target.apk --sign
```

## Examples

```bash
# Patch with automatic architecture detection and signing
dotnet run --project src/PulseGadget.Cli -- patch ./app.apk --sign

# Patch with explicit architecture and specific Frida version
dotnet run --project src/PulseGadget.Cli -- patch ./app.apk --arch arm64 --frida-version 16.4.8 --sign

# Inspect APK metadata and architecture hints
dotnet run --project src/PulseGadget.Cli -- inspect ./app.apk

# Validate environment and cache status
dotnet run --project src/PulseGadget.Cli -- doctor

# Preinstall tools and one Frida gadget artifact
dotnet run --project src/PulseGadget.Cli -- tools install --apktool --signer --frida-version 16.4.8 --arch arm64
```
