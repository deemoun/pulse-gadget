namespace PulseGadget.Core.Services;

using PulseGadget.Core.Models;

public static class ArchitectureMapper
{
    public static bool TryParse(string? value, out Architecture architecture)
    {
        architecture = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "arm64":
            case "arm64-v8a":
                architecture = Architecture.Arm64;
                return true;
            case "arm":
            case "armeabi-v7a":
                architecture = Architecture.Arm;
                return true;
            case "x86":
                architecture = Architecture.X86;
                return true;
            case "x86_64":
            case "x64":
                architecture = Architecture.X64;
                return true;
            default:
                return false;
        }
    }

    public static string ToFridaArch(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "arm64",
        Architecture.Arm => "arm",
        Architecture.X86 => "x86",
        Architecture.X64 => "x86_64",
        _ => throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null)
    };

    public static string ToApkLibFolder(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "arm64-v8a",
        Architecture.Arm => "armeabi-v7a",
        Architecture.X86 => "x86",
        Architecture.X64 => "x86_64",
        _ => throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null)
    };
}
