using System.IO.Compression;
using PulseGadget.Core.Abstractions;

namespace PulseGadget.Core.Services;

public sealed class DexMergeService
{
    private readonly ILogger _logger;

    public DexMergeService(ILogger logger)
    {
        _logger = logger;
    }

    public void PreserveOriginalDexes(string originalApkPath, string rebuiltApkPath, int? modifiedDexClassNumber)
    {
        var modifiedDex = modifiedDexClassNumber is > 1
            ? $"classes{modifiedDexClassNumber}.dex"
            : "classes.dex";

        var tempDir = Path.Combine(Path.GetTempPath(), "pulse-gadget-dex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(originalApkPath, tempDir);
            var tempApk = Path.GetTempFileName();

            using (var rebuiltArchive = ZipFile.OpenRead(rebuiltApkPath))
            using (var outputArchive = ZipFile.Open(tempApk, ZipArchiveMode.Create))
            {
                foreach (var entry in rebuiltArchive.Entries)
                {
                    var isDex = entry.FullName.StartsWith("classes", StringComparison.Ordinal) && entry.FullName.EndsWith(".dex", StringComparison.Ordinal);
                    if (isDex && !string.Equals(entry.FullName, modifiedDex, StringComparison.Ordinal))
                    {
                        var originalDexPath = Path.Combine(tempDir, entry.FullName);
                        if (File.Exists(originalDexPath))
                        {
                            outputArchive.CreateEntryFromFile(originalDexPath, entry.FullName, CompressionLevel.NoCompression);
                            continue;
                        }
                    }

                    var newEntry = outputArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var from = entry.Open();
                    using var to = newEntry.Open();
                    from.CopyTo(to);
                }
            }

            File.Move(tempApk, rebuiltApkPath, overwrite: true);
            _logger.Info("Successfully preserved original dex files (except patched dex)");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
