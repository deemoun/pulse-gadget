using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;
using PulseGadget.Core.Services;

return await new CliApp().RunAsync(args);

internal sealed class CliApp
{
    private readonly ILogger _logger = new ConsoleLogger();

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var processRunner = new ProcessRunner();
        var cacheProvider = new CachePathProvider();
        var downloader = new ToolDownloadService();
        var toolRepository = new ToolRepository(cacheProvider, downloader, _logger);
        var archDetector = new ArchitectureDetectionService(processRunner, _logger);
        var apktoolService = new ApktoolService(processRunner, _logger);
        var fridaArtifacts = new FridaArtifactService(toolRepository, downloader);
        var activityDetector = new ActivityDetectionService();
        var manifestPatch = new ManifestPatchService();
        var gadgetInjection = new GadgetInjectionService(_logger);
        var smaliPatch = new SmaliPatchService();
        var signer = new SigningService(processRunner, _logger);
        var dexMerge = new DexMergeService(_logger);

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "patch":
                    return await HandlePatchAsync(args[1..], toolRepository, fridaArtifacts, archDetector, activityDetector, manifestPatch, gadgetInjection, smaliPatch, apktoolService, signer, dexMerge);
                case "inspect":
                    return await HandleInspectAsync(args[1..], archDetector, activityDetector, apktoolService, toolRepository);
                case "doctor":
                    return await HandleDoctorAsync(processRunner, toolRepository);
                case "tools":
                    return await HandleToolsAsync(args[1..], toolRepository, fridaArtifacts);
                default:
                    _logger.Error($"Unknown command: {args[0]}");
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            return 1;
        }
    }

    private async Task<int> HandlePatchAsync(
        string[] args,
        ToolRepository toolRepository,
        FridaArtifactService fridaArtifacts,
        ArchitectureDetectionService archDetector,
        ActivityDetectionService activityDetector,
        ManifestPatchService manifestPatch,
        GadgetInjectionService gadgetInjection,
        SmaliPatchService smaliPatch,
        ApktoolService apktoolService,
        SigningService signer,
        DexMergeService dexMerge)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintPatchHelp();
            return 0;
        }

        var apkPath = args[0];
        var options = CliArgs.Parse(args[1..]);
        if (options.TryGetInt("js-delay", out var parsedDelay) && parsedDelay < 0)
        {
            throw new InvalidOperationException("--js-delay must be >= 0");
        }
        if (options.TryGetInt("js-delay", out _) && !options.TryGet("js", out _))
        {
            throw new InvalidOperationException("--js-delay requires --js");
        }

        Architecture? explicitArch = null;
        if (options.TryGet("arch", out var archText))
        {
            if (!ArchitectureMapper.TryParse(archText, out var parsed))
            {
                throw new InvalidOperationException("Invalid --arch. Supported: arm64, arm, x86, x86_64");
            }

            explicitArch = parsed;
        }

        var request = new PatchRequest
        {
            ApkPath = apkPath,
            OutputPath = options.TryGet("output", out var output) ? output : null,
            MainActivity = options.TryGet("main-activity", out var main) ? main : null,
            FridaVersion = options.TryGet("frida-version", out var version) ? version : null,
            GadgetName = options.TryGet("gadget-name", out var gadget) ? gadget :
                         options.TryGet("custom-gadget-name", out var customGadget) ? customGadget : null,
            ConfigPath = options.TryGet("config", out var config) ? config : null,
            ScriptPath = options.TryGet("js", out var script) ? script : null,
            ScriptDelaySeconds = options.TryGetInt("js-delay", out var delay) ? delay : null,
            Sign = options.HasFlag("sign"),
            ForceManifest = options.HasFlag("force-manifest"),
            NoRes = options.HasFlag("no-res"),
            UseAapt2 = options.HasFlag("use-aapt2"),
            ApktoolCommandOverride = options.TryGet("apktool-path", out var apktoolPath) ? apktoolPath : null,
            DecompileOptionsRaw = options.TryGet("decompile-opts", out var decompileOpts) ? decompileOpts : null,
            RecompileOptionsRaw = options.TryGet("recompile-opts", out var recompileOpts) ? recompileOpts : null,
            SkipDecompile = options.HasFlag("skip-decompile"),
            SkipRecompile = options.HasFlag("skip-recompile"),
            KeystorePath = options.TryGet("ks", out var ks) ? ks : null,
            KeystoreAlias = options.TryGet("ks-alias", out var alias) ? alias : null,
            KeystorePassword = options.TryGet("ks-pass", out var kspass) ? kspass : null,
            KeystoreKeyPassword = options.TryGet("ks-key-pass", out var keypass) ? keypass : null,
            ExplicitArchitecture = explicitArch
        };

        var pipeline = new PatchPipelineService(
            toolRepository,
            fridaArtifacts,
            archDetector,
            activityDetector,
            manifestPatch,
            gadgetInjection,
            smaliPatch,
            apktoolService,
            signer,
            dexMerge,
            _logger);

        var result = await pipeline.RunAsync(request);
        _logger.Info($"Patch success: {result.Success}");
        if (result.OutputApkPath is not null)
        {
            _logger.Info($"Output APK: {result.OutputApkPath}");
        }

        foreach (var warning in result.Warnings)
        {
            _logger.Warn(warning);
        }

        foreach (var error in result.Errors)
        {
            _logger.Error(error);
        }

        return result.Success ? 0 : 1;
    }

    private async Task<int> HandleInspectAsync(string[] args, ArchitectureDetectionService archDetector, ActivityDetectionService activityDetector, ApktoolService apktoolService, ToolRepository toolRepository)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            _logger.Info("Usage: pulse-gadget inspect <apk-path> [--apktool-path <command-or-path>]");
            return 0;
        }

        var apkPath = args[0];
        var options = CliArgs.Parse(args[1..]);
        var service = new InspectService(archDetector, activityDetector, apktoolService, toolRepository);
        var report = await service.InspectAsync(apkPath, options.TryGet("apktool-path", out var overrideCmd) ? overrideCmd : null);

        _logger.Info($"Package: {report.PackageName ?? "<unknown>"}");
        _logger.Info($"Main activity: {report.MainActivity ?? "<unknown>"}");
        _logger.Info($"Main activity candidates: {(report.MainActivityCandidates.Count == 0 ? "<none>" : string.Join(", ", report.MainActivityCandidates))}");
        _logger.Info($"Architectures in APK: {(report.ArchitecturesInApk.Count == 0 ? "<none>" : string.Join(", ", report.ArchitecturesInApk.Select(ArchitectureMapper.ToFridaArch)))}");
        return 0;
    }

    private async Task<int> HandleDoctorAsync(ProcessRunner processRunner, ToolRepository toolRepository)
    {
        var doctor = new DoctorService(processRunner, toolRepository);
        var report = await doctor.RunAsync();

        _logger.Info($"Cache path: {report.CachePath}");
        _logger.Info($"Java available: {(report.JavaAvailable ? "yes" : "no")}");
        _logger.Info($"ADB available: {(report.AdbAvailable ? "yes" : "no")}");
        _logger.Info($"apktool cached: {(report.ApktoolCached ? "yes" : "no")}");
        _logger.Info($"uber-apk-signer cached: {(report.UberApkSignerCached ? "yes" : "no")}");
        foreach (var note in report.Notes)
        {
            _logger.Info(note);
        }

        return 0;
    }

    private async Task<int> HandleToolsAsync(string[] args, ToolRepository toolRepository, FridaArtifactService fridaArtifacts)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            _logger.Info("Usage: pulse-gadget tools install [--all] [--apktool] [--signer] [--frida-version <v> --arch <arch>]");
            return 0;
        }

        if (!string.Equals(args[0], "install", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only 'tools install' is currently implemented.");
        }

        var options = CliArgs.Parse(args[1..]);
        var installAll = options.HasFlag("all");

        Architecture? arch = null;
        if (options.TryGet("arch", out var archRaw))
        {
            if (!ArchitectureMapper.TryParse(archRaw, out var parsed))
            {
                throw new InvalidOperationException("Invalid --arch for tools install.");
            }
            arch = parsed;
        }

        var installer = new ToolsInstallService(toolRepository, fridaArtifacts);
        var logs = await installer.InstallAsync(
            apktool: installAll || options.HasFlag("apktool"),
            signer: installAll || options.HasFlag("signer"),
            fridaVersion: options.TryGet("frida-version", out var version) ? version : null,
            arch: arch);

        if (logs.Count == 0)
        {
            _logger.Warn("No install targets selected. Use --all, --apktool, --signer, or --frida-version with --arch.");
            return 1;
        }

        foreach (var log in logs)
        {
            _logger.Info(log);
        }

        return 0;
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private void PrintHelp()
    {
        _logger.Info("pulse-gadget (.NET) - standalone APK patching utility");
        _logger.Info("Commands:");
        _logger.Info("  patch <apk-path> [options]");
        _logger.Info("  inspect <apk-path> [options]");
        _logger.Info("  doctor");
        _logger.Info("  tools install [options]");
        _logger.Info("Run 'pulse-gadget patch --help' for patch options.");
    }

    private void PrintPatchHelp()
    {
        _logger.Info("Usage: pulse-gadget patch <apk-path> [options]");
        _logger.Info("Options:");
        _logger.Info("  --arch <arm64|arm|x86|x86_64>");
        _logger.Info("  --frida-version <version>");
        _logger.Info("  --main-activity <full.activity.Name>");
        _logger.Info("  --sign");
        _logger.Info("  --output <apk-path>");
        _logger.Info("  --apktool-path <command-or-path>");
        _logger.Info("  --gadget-name <name> (alias: --custom-gadget-name)");
        _logger.Info("  --config <path>, --js <path>, --js-delay <seconds>");
        _logger.Info("  --force-manifest, --no-res, --use-aapt2");
        _logger.Info("  --skip-decompile, --skip-recompile");
        _logger.Info("  --decompile-opts <args>, --recompile-opts <args>");
        _logger.Info("  --ks <path>, --ks-alias <alias>, --ks-pass <pass>, --ks-key-pass <pass>");
    }
}

internal sealed class ConsoleLogger : ILogger
{
    public void Info(string message) => Write("INFO", ConsoleColor.White, message);
    public void Warn(string message) => Write("WARN", ConsoleColor.Yellow, message);
    public void Error(string message) => Write("ERROR", ConsoleColor.Red, message);
    public void Success(string message) => Write("SUCCESS", ConsoleColor.Green, message);
    public void Debug(string message)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PULSE_GADGET_DEBUG")))
        {
            Write("DEBUG", ConsoleColor.Cyan, message);
        }
    }

    private static void Write(string level, ConsoleColor color, string message)
    {
        var prior = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{level}] {message}");
        Console.ForegroundColor = prior;
    }
}

internal sealed class CliArgs
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    private CliArgs() { }

    public static CliArgs Parse(string[] args)
    {
        var parsed = new CliArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = token[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                parsed._values[key] = args[++i];
            }
            else
            {
                parsed._flags.Add(key);
            }
        }

        return parsed;
    }

    public bool HasFlag(string name) => _flags.Contains(name);

    public bool TryGet(string name, out string value) => _values.TryGetValue(name, out value!);

    public bool TryGetInt(string name, out int value)
    {
        value = 0;
        if (!_values.TryGetValue(name, out var raw))
        {
            return false;
        }

        return int.TryParse(raw, out value);
    }
}
