using System.Collections.ObjectModel;

namespace PulseGadget.Core.Models;

public sealed class PatchRequest
{
    public required string ApkPath { get; init; }
    public string? OutputPath { get; init; }
    public string? MainActivity { get; init; }
    public string? FridaVersion { get; init; }
    public string? GadgetName { get; init; }
    public string? ConfigPath { get; init; }
    public string? ScriptPath { get; init; }
    public int? ScriptDelaySeconds { get; init; }
    public bool Sign { get; init; }
    public bool ForceManifest { get; init; }
    public bool NoRes { get; init; }
    public bool UseAapt2 { get; init; }
    public string? ApktoolCommandOverride { get; init; }
    public string? DecompileOptionsRaw { get; init; }
    public string? RecompileOptionsRaw { get; init; }
    public bool SkipDecompile { get; init; }
    public bool SkipRecompile { get; init; }
    public string? KeystorePath { get; init; }
    public string? KeystoreAlias { get; init; }
    public string? KeystorePassword { get; init; }
    public string? KeystoreKeyPassword { get; init; }
    public Architecture? ExplicitArchitecture { get; init; }

    public IReadOnlyList<string> DecompileOptions =>
        new ReadOnlyCollection<string>((DecompileOptionsRaw ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public IReadOnlyList<string> RecompileOptions =>
        new ReadOnlyCollection<string>((RecompileOptionsRaw ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
