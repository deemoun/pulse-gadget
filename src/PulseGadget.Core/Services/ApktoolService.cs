using PulseGadget.Core.Abstractions;

namespace PulseGadget.Core.Services;

public sealed class ApktoolService
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogger _logger;

    public ApktoolService(ProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task RunDecompileAsync(
        string apktoolJarPath,
        string? apktoolCommandOverride,
        string apkPath,
        string outputDir,
        bool forceManifest,
        bool noRes,
        IReadOnlyList<string> extraOptions,
        CancellationToken cancellationToken = default)
    {
        var toolArgs = new List<string>
        {
            "d",
            "-o",
            outputDir,
            "--force"
        };

        if (forceManifest)
        {
            toolArgs.Add("--force-manifest");
        }

        if (noRes)
        {
            toolArgs.Add("--no-res");
        }

        toolArgs.AddRange(extraOptions);

        var (fileName, args) = BuildApktoolCommand(apktoolJarPath, apktoolCommandOverride, [..toolArgs, apkPath]);
        var result = await _processRunner.RunAsync(fileName, args, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"apktool decompile failed: {result.StdErr}\n{result.StdOut}");
        }

        _logger.Debug(result.StdOut);
    }

    public async Task RunBuildAsync(
        string apktoolJarPath,
        string? apktoolCommandOverride,
        string decompiledDir,
        bool useAapt2,
        IReadOnlyList<string> extraOptions,
        CancellationToken cancellationToken = default)
    {
        var toolArgs = new List<string>
        {
            "b",
            decompiledDir
        };

        if (useAapt2)
        {
            toolArgs.Add("--use-aapt2");
        }

        toolArgs.AddRange(extraOptions);

        var (fileName, args) = BuildApktoolCommand(apktoolJarPath, apktoolCommandOverride, toolArgs);
        var result = await _processRunner.RunAsync(fileName, args, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"apktool rebuild failed: {result.StdErr}\n{result.StdOut}");
        }

        _logger.Debug(result.StdOut);
    }

    private static (string FileName, List<string> Args) BuildApktoolCommand(string apktoolJarPath, string? overrideCommand, IReadOnlyList<string> toolArgs)
    {
        if (string.IsNullOrWhiteSpace(overrideCommand))
        {
            return ("java", ["-jar", apktoolJarPath, .. toolArgs]);
        }

        var parts = SplitCommand(overrideCommand);
        if (parts.Count == 0)
        {
            throw new InvalidOperationException("Invalid --apktool-path/command value.");
        }

        var fileName = parts[0];
        var args = parts.Skip(1).ToList();
        args.AddRange(toolArgs);
        return (fileName, args);
    }

    private static List<string> SplitCommand(string command)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in command)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}
