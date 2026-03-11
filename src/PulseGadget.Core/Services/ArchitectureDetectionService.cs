using System.IO.Compression;
using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class ArchitectureDetectionService
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogger _logger;

    public ArchitectureDetectionService(ProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<ArchitectureResolutionResult> ResolveAsync(PatchRequest request, string apkPath, CancellationToken cancellationToken = default)
    {
        if (request.ExplicitArchitecture is { } explicitArch)
        {
            return new ArchitectureResolutionResult
            {
                Architecture = explicitArch,
                Source = ArchitectureDetectionSource.Explicit,
                Message = $"Using architecture from --arch: {ArchitectureMapper.ToFridaArch(explicitArch)}"
            };
        }

        var adbArch = await DetectFromAdbAsync(cancellationToken).ConfigureAwait(false);
        if (adbArch is not null)
        {
            return new ArchitectureResolutionResult
            {
                Architecture = adbArch.Value,
                Source = ArchitectureDetectionSource.Adb,
                Message = $"Detected architecture from ADB: {ArchitectureMapper.ToFridaArch(adbArch.Value)}"
            };
        }

        var apkArch = DetectFromApkLibs(apkPath);
        if (apkArch is not null)
        {
            return new ArchitectureResolutionResult
            {
                Architecture = apkArch.Value,
                Source = ArchitectureDetectionSource.ApkLibInspection,
                Message = $"Detected architecture from APK lib folders: {ArchitectureMapper.ToFridaArch(apkArch.Value)}"
            };
        }

        throw new InvalidOperationException("Unable to determine architecture. Pass --arch explicitly (arm64|arm|x86|x86_64).");
    }

    public List<Architecture> DetectAllFromApkLibs(string apkPath)
    {
        var found = new HashSet<Architecture>();
        using var zip = ZipFile.OpenRead(apkPath);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = entry.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (ArchitectureMapper.TryParse(parts[1], out var arch))
            {
                found.Add(arch);
            }
        }

        return [..found];
    }

    private async Task<Architecture?> DetectFromAdbAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner
                .RunAsync("adb", ["shell", "getprop", "ro.product.cpu.abi"], cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.Debug($"ADB architecture detection failed: {result.StdErr.Trim()}");
                return null;
            }

            var output = result.StdOut.Trim();
            if (ArchitectureMapper.TryParse(output, out var arch))
            {
                return arch;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private Architecture? DetectFromApkLibs(string apkPath)
    {
        var preferred = new[] { Architecture.Arm64, Architecture.Arm, Architecture.X64, Architecture.X86 };
        var available = DetectAllFromApkLibs(apkPath);
        return preferred.FirstOrDefault(a => available.Contains(a));
    }
}
