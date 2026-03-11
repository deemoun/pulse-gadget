using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class ManifestPatchService
{
    public ManifestPatchReport Patch(string decompiledDir)
    {
        var manifestPath = Path.Combine(decompiledDir, "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("AndroidManifest.xml not found in decompiled output.", manifestPath);
        }

        var text = File.ReadAllText(manifestPath);
        var report = new ManifestPatchReport();

        if (!text.Contains("android.permission.INTERNET", StringComparison.Ordinal))
        {
            var insertAt = text.IndexOf("</manifest>", StringComparison.Ordinal);
            if (insertAt > -1)
            {
                text = text.Insert(insertAt, "<uses-permission android:name='android.permission.INTERNET'/>");
                report.Changes.Add("Added INTERNET permission");
            }
        }

        if (text.Contains(":extractNativeLibs=\"false\"", StringComparison.Ordinal))
        {
            text = text.Replace(":extractNativeLibs=\"false\"", ":extractNativeLibs=\"true\"", StringComparison.Ordinal);
            report.Changes.Add("Set extractNativeLibs=true");
        }

        if (report.Changes.Count > 0)
        {
            File.WriteAllText(manifestPath, text);
            return new ManifestPatchReport { Changed = true, Changes = report.Changes };
        }

        return new ManifestPatchReport { Changed = false, Changes = ["No manifest updates required"] };
    }
}
