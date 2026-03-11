namespace PulseGadget.Core.Models;

public sealed class ToolPaths
{
    public required string CacheRoot { get; init; }
    public required string ApktoolJarPath { get; init; }
    public required string UberApkSignerJarPath { get; init; }
    public required string FridaRoot { get; init; }
}
