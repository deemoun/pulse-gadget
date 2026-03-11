using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class CachePathProvider
{
    public ToolPaths GetPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDir = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pulse-gadget")
            : Path.Combine(localAppData, "PulseGadget");

        var toolsRoot = Path.Combine(baseDir, "tools");
        var fridaRoot = Path.Combine(baseDir, "frida");

        Directory.CreateDirectory(toolsRoot);
        Directory.CreateDirectory(fridaRoot);

        return new ToolPaths
        {
            CacheRoot = baseDir,
            ApktoolJarPath = Path.Combine(toolsRoot, "apktool.jar"),
            UberApkSignerJarPath = Path.Combine(toolsRoot, "uber-apk-signer.jar"),
            FridaRoot = fridaRoot
        };
    }
}
