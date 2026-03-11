namespace PulseGadget.Core.Models;

public sealed class InspectResult
{
    public required string ApkPath { get; init; }
    public string? PackageName { get; init; }
    public string? MainActivity { get; init; }
    public List<string> MainActivityCandidates { get; init; } = [];
    public List<Architecture> ArchitecturesInApk { get; init; } = [];
}
