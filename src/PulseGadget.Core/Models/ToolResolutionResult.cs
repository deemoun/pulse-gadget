namespace PulseGadget.Core.Models;

public sealed class ToolResolutionResult
{
    public required string Path { get; init; }
    public bool Downloaded { get; init; }
    public string Description => Downloaded ? "downloaded" : "cached";
}
