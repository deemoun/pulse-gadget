namespace PulseGadget.Core.Models;

public sealed class DoctorResult
{
    public bool JavaAvailable { get; set; }
    public bool AdbAvailable { get; set; }
    public bool ApktoolCached { get; set; }
    public bool UberApkSignerCached { get; set; }
    public string? CachePath { get; set; }
    public List<string> Notes { get; } = [];
}
