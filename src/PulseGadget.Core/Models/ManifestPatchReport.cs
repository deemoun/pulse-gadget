namespace PulseGadget.Core.Models;

public sealed class ManifestPatchReport
{
    public bool Changed { get; init; }
    public List<string> Changes { get; init; } = [];
}
