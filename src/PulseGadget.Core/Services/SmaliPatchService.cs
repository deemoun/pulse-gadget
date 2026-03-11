namespace PulseGadget.Core.Services;

public sealed class SmaliPatchService
{
    public SmaliPatchResult InjectLoadLibrary(string decompiledDir, string mainActivity, string gadgetLibraryName)
    {
        var smaliRelative = mainActivity.Replace('.', Path.DirectorySeparatorChar) + ".smali";
        var decompiledPath = new DirectoryInfo(decompiledDir);

        FileInfo? targetFile = null;
        int? dexClassNumber = null;

        foreach (var dir in decompiledPath.EnumerateDirectories().Where(d => d.Name.StartsWith("smali", StringComparison.Ordinal)))
        {
            var candidate = new FileInfo(Path.Combine(dir.FullName, smaliRelative));
            if (!candidate.Exists)
            {
                continue;
            }

            targetFile = candidate;
            if (dir.Name.StartsWith("smali_classes", StringComparison.Ordinal) &&
                int.TryParse(dir.Name["smali_classes".Length..], out var parsed))
            {
                dexClassNumber = parsed;
            }
            break;
        }

        if (targetFile is null)
        {
            throw new FileNotFoundException($"Unable to locate smali file for activity {mainActivity}");
        }

        var loadName = gadgetLibraryName.StartsWith("lib", StringComparison.Ordinal)
            ? gadgetLibraryName[3..]
            : gadgetLibraryName;
        loadName = loadName.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
            ? loadName[..^3]
            : loadName;

        var lines = File.ReadAllLines(targetFile.FullName).ToList();
        var inserted = false;

        for (var i = 0; i < lines.Count - 2; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith(".method", StringComparison.Ordinal))
            {
                continue;
            }

            if (!trimmed.Contains(" onCreate(", StringComparison.Ordinal) && !trimmed.Contains("<init>", StringComparison.Ordinal))
            {
                continue;
            }

            if (!lines[i + 1].Contains(".locals", StringComparison.Ordinal))
            {
                continue;
            }

            if (lines[i + 1].Contains(".locals 0", StringComparison.Ordinal))
            {
                lines[i + 1] = lines[i + 1].Replace(".locals 0", ".locals 1", StringComparison.Ordinal);
            }

            lines.Insert(i + 2, $"    const-string v0, \"{loadName}\"");
            lines.Insert(i + 3, "    invoke-static {v0}, Ljava/lang/System;->loadLibrary(Ljava/lang/String;)V");
            inserted = true;
            break;
        }

        if (!inserted)
        {
            throw new InvalidOperationException($"Could not find patchable entrypoint in {targetFile.FullName}");
        }

        File.WriteAllLines(targetFile.FullName, lines);
        return new SmaliPatchResult(targetFile.FullName, dexClassNumber);
    }
}

public sealed record SmaliPatchResult(string SmaliPath, int? DexClassNumber);
