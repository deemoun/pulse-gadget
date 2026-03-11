namespace PulseGadget.Core.Models;

public sealed class PatchResult
{
    public bool Success { get; set; }
    public Architecture? SelectedArchitecture { get; set; }
    public ArchitectureDetectionSource ArchitectureSource { get; set; } = ArchitectureDetectionSource.Unknown;
    public string? PatchedActivity { get; set; }
    public bool ManifestChanged { get; set; }
    public string? ManifestChangeSummary { get; set; }
    public bool SmaliPatched { get; set; }
    public bool RebuildSucceeded { get; set; }
    public bool SigningSucceeded { get; set; }
    public string? OutputApkPath { get; set; }
    public string? DecompiledDirectory { get; set; }
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
}
