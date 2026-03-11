using System.Xml.Linq;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class ActivityDetectionService
{
    public ActivityDetectionResult DetectFromDecompiledManifest(string decompiledDir)
    {
        var manifestPath = Path.Combine(decompiledDir, "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("AndroidManifest.xml not found in decompiled output.", manifestPath);
        }

        var doc = XDocument.Load(manifestPath);
        var manifest = doc.Root ?? throw new InvalidOperationException("Invalid AndroidManifest.xml");
        var androidNs = manifest.GetNamespaceOfPrefix("android") ?? XNamespace.Get("http://schemas.android.com/apk/res/android");

        var packageName = manifest.Attribute("package")?.Value;
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        IEnumerable<XElement> activityElements = manifest
            .Descendants("activity")
            .Concat(manifest.Descendants("activity-alias"));

        foreach (var activity in activityElements)
        {
            var enabled = activity.Attribute(androidNs + "enabled")?.Value;
            if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var intentFilters = activity.Elements("intent-filter");
            var hasMain = intentFilters.Descendants("action")
                .Any(a => string.Equals(a.Attribute(androidNs + "name")?.Value, "android.intent.action.MAIN", StringComparison.Ordinal));
            var hasLauncher = intentFilters.Descendants("category")
                .Any(c => string.Equals(c.Attribute(androidNs + "name")?.Value, "android.intent.category.LAUNCHER", StringComparison.Ordinal));

            if (!hasMain || !hasLauncher)
            {
                continue;
            }

            var targetActivity = activity.Attribute(androidNs + "targetActivity")?.Value;
            var name = targetActivity ?? activity.Attribute(androidNs + "name")?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                candidates.Add(ResolveActivityName(packageName, name));
            }
        }

        string? mainActivity = null;
        string note;

        if (candidates.Count == 1)
        {
            mainActivity = candidates.First();
            note = "Resolved main activity from MAIN+LAUNCHER intent-filter.";
        }
        else if (candidates.Count > 1)
        {
            note = "Multiple main activity candidates found. Pass --main-activity explicitly.";
        }
        else
        {
            var fallback = manifest.Descendants("activity")
                .Select(a => a.Attribute(androidNs + "name")?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                mainActivity = ResolveActivityName(packageName, fallback);
                note = "Main activity not found by intent-filter; used first activity as fallback.";
            }
            else
            {
                note = "No activity entries were found in manifest.";
            }
        }

        return new ActivityDetectionResult
        {
            PackageName = packageName,
            MainActivity = mainActivity,
            MainActivityCandidates = [..candidates],
            DetectionNote = note
        };
    }

    private static string ResolveActivityName(string? packageName, string activityName)
    {
        if (activityName.StartsWith('.'))
        {
            return (packageName ?? string.Empty) + activityName;
        }

        if (!activityName.Contains('.') && !string.IsNullOrWhiteSpace(packageName))
        {
            return packageName + "." + activityName;
        }

        return activityName;
    }
}
