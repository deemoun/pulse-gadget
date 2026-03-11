namespace PulseGadget.Core.Models;

public sealed class ArchitectureResolutionResult
{
    public required Architecture Architecture { get; init; }
    public required ArchitectureDetectionSource Source { get; init; }
    public required string Message { get; init; }
}
