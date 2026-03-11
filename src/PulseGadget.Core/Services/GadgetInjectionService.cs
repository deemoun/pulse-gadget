using System.Text.Json;
using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class GadgetInjectionService
{
    private readonly ILogger _logger;

    public GadgetInjectionService(ILogger logger)
    {
        _logger = logger;
    }

    public string InjectBinaryAndAssets(
        string decompiledDir,
        Architecture architecture,
        string sourceGadgetPath,
        string? customGadgetName,
        string? configPath,
        string? scriptPath,
        int? scriptDelaySeconds)
    {
        var libRoot = Path.Combine(decompiledDir, "lib", ArchitectureMapper.ToApkLibFolder(architecture));
        Directory.CreateDirectory(libRoot);

        var gadgetBase = string.IsNullOrWhiteSpace(customGadgetName)
            ? Path.GetFileName(sourceGadgetPath)
            : EnsureSoExtension(customGadgetName);

        var finalGadgetFileName = gadgetBase.StartsWith("lib", StringComparison.Ordinal)
            ? gadgetBase
            : "lib" + gadgetBase;

        var destinationGadgetPath = Path.Combine(libRoot, finalGadgetFileName);
        File.Copy(sourceGadgetPath, destinationGadgetPath, overwrite: true);

        var loadLibraryName = finalGadgetFileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
            ? finalGadgetFileName[..^3]
            : finalGadgetFileName;

        var filesToCopy = new Dictionary<string, string?>
        {
            ["config"] = configPath,
            ["script"] = scriptPath
        };

        if (!string.IsNullOrWhiteSpace(scriptPath) && !string.IsNullOrWhiteSpace(configPath))
        {
            var raw = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("interaction", out _))
            {
                throw new InvalidOperationException("Config file must contain 'interaction' key.");
            }

            var mutable = JsonSerializer.Deserialize<Dictionary<string, object>>(raw) ?? [];
            mutable["interaction"] = new Dictionary<string, object> { ["type"] = "script", ["path"] = loadLibraryName + ".script.so" };
            var rewrittenConfigPath = Path.Combine(libRoot, loadLibraryName + ".config.so");
            File.WriteAllText(rewrittenConfigPath, JsonSerializer.Serialize(mutable, new JsonSerializerOptions { WriteIndented = true }));
            filesToCopy["config"] = null;
        }
        else if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            var defaultConfig = new
            {
                interaction = new
                {
                    type = "script",
                    path = loadLibraryName + ".script.so"
                }
            };
            var rewrittenConfigPath = Path.Combine(libRoot, loadLibraryName + ".config.so");
            File.WriteAllText(rewrittenConfigPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
            filesToCopy["config"] = null;
        }

        foreach (var kv in filesToCopy)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                continue;
            }

            var input = kv.Value!;
            if (!File.Exists(input))
            {
                throw new FileNotFoundException($"Frida {kv.Key} file not found", input);
            }

            var fileToCopy = input;
            if (kv.Key == "script" && scriptDelaySeconds is > 0)
            {
                fileToCopy = WrapScriptWithDelay(input, scriptDelaySeconds.Value);
            }

            var targetName = $"{loadLibraryName}.{kv.Key}.so";
            File.Copy(fileToCopy, Path.Combine(libRoot, targetName), overwrite: true);

            if (!string.Equals(fileToCopy, input, StringComparison.Ordinal) && File.Exists(fileToCopy))
            {
                File.Delete(fileToCopy);
            }
        }

        _logger.Info($"Injected gadget binary into {libRoot}");
        return finalGadgetFileName;
    }

    private static string WrapScriptWithDelay(string scriptPath, int delaySeconds)
    {
        var wrappedPath = Path.Combine(Path.GetDirectoryName(scriptPath)!, Path.GetFileNameWithoutExtension(scriptPath) + "_wrapped" + Path.GetExtension(scriptPath));
        var original = File.ReadAllText(scriptPath);
        var wrapped = $"setTimeout(function() {{\n{original}\n}}, {delaySeconds * 1000});";
        File.WriteAllText(wrappedPath, wrapped);
        return wrappedPath;
    }

    private static string EnsureSoExtension(string name)
    {
        return name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? name : name + ".so";
    }
}
