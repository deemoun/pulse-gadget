namespace PulseGadget.Core.Models;

public sealed class ActivityDetectionResult
{
    public string? PackageName { get; init; }
    public string? MainActivity { get; init; }
    public List<string> MainActivityCandidates { get; init; } = [];
    public string? DetectionNote { get; init; }
}
