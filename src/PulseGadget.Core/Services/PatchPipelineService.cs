using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class PatchPipelineService
{
    private readonly ToolRepository _toolRepository;
    private readonly FridaArtifactService _fridaArtifactService;
    private readonly ArchitectureDetectionService _architectureDetectionService;
    private readonly ActivityDetectionService _activityDetectionService;
    private readonly ManifestPatchService _manifestPatchService;
    private readonly GadgetInjectionService _gadgetInjectionService;
    private readonly SmaliPatchService _smaliPatchService;
    private readonly ApktoolService _apktoolService;
    private readonly SigningService _signingService;
    private readonly DexMergeService _dexMergeService;
    private readonly ILogger _logger;

    public PatchPipelineService(
        ToolRepository toolRepository,
        FridaArtifactService fridaArtifactService,
        ArchitectureDetectionService architectureDetectionService,
        ActivityDetectionService activityDetectionService,
        ManifestPatchService manifestPatchService,
        GadgetInjectionService gadgetInjectionService,
        SmaliPatchService smaliPatchService,
        ApktoolService apktoolService,
        SigningService signingService,
        DexMergeService dexMergeService,
        ILogger logger)
    {
        _toolRepository = toolRepository;
        _fridaArtifactService = fridaArtifactService;
        _architectureDetectionService = architectureDetectionService;
        _activityDetectionService = activityDetectionService;
        _manifestPatchService = manifestPatchService;
        _gadgetInjectionService = gadgetInjectionService;
        _smaliPatchService = smaliPatchService;
        _apktoolService = apktoolService;
        _signingService = signingService;
        _dexMergeService = dexMergeService;
        _logger = logger;
    }

    public async Task<PatchResult> RunAsync(PatchRequest request, CancellationToken cancellationToken = default)
    {
        var result = new PatchResult();

        try
        {
            if (!File.Exists(request.ApkPath))
            {
                throw new FileNotFoundException("APK file not found.", request.ApkPath);
            }

            var archResult = await _architectureDetectionService.ResolveAsync(request, request.ApkPath, cancellationToken).ConfigureAwait(false);
            result.SelectedArchitecture = archResult.Architecture;
            result.ArchitectureSource = archResult.Source;
            _logger.Info($"Selected architecture: {ArchitectureMapper.ToFridaArch(archResult.Architecture)} ({archResult.Source})");

            string apktoolJarPath;
            if (string.IsNullOrWhiteSpace(request.ApktoolCommandOverride))
            {
                var apktool = await _toolRepository.ResolveApktoolJarAsync(cancellationToken).ConfigureAwait(false);
                apktoolJarPath = apktool.Path;
                _logger.Info($"{(apktool.Downloaded ? "Downloading" : "Using cached")} apktool: {apktool.Path}");
            }
            else
            {
                apktoolJarPath = _toolRepository.Paths.ApktoolJarPath;
                _logger.Info($"Using custom apktool command: {request.ApktoolCommandOverride}");
            }

            var fridaVersion = await _toolRepository.ResolveFridaVersionAsync(request.FridaVersion, cancellationToken).ConfigureAwait(false);
            var gadget = await _fridaArtifactService.ResolveGadgetAsync(fridaVersion, archResult.Architecture, cancellationToken).ConfigureAwait(false);
            _logger.Info($"{(gadget.Downloaded ? "Downloading" : "Using cached")} Frida Gadget for {ArchitectureMapper.ToFridaArch(archResult.Architecture)}: {gadget.Path}");

            var decompiledDir = Path.Combine(Path.GetTempPath(), "pulse-gadget", Path.GetFileNameWithoutExtension(request.ApkPath));
            result.DecompiledDirectory = decompiledDir;

            if (!request.SkipDecompile)
            {
                if (Directory.Exists(decompiledDir))
                {
                    Directory.Delete(decompiledDir, recursive: true);
                }

                Directory.CreateDirectory(decompiledDir);
                await _apktoolService.RunDecompileAsync(
                    apktoolJarPath,
                    request.ApktoolCommandOverride,
                    request.ApkPath,
                    decompiledDir,
                    request.ForceManifest,
                    request.NoRes,
                    request.DecompileOptions,
                    cancellationToken).ConfigureAwait(false);
                _logger.Info("APK decompiled successfully");
            }
            else if (!Directory.Exists(decompiledDir))
            {
                throw new DirectoryNotFoundException($"Skip decompile requested but directory not found: {decompiledDir}");
            }

            var activityInfo = _activityDetectionService.DetectFromDecompiledManifest(decompiledDir);
            var mainActivity = request.MainActivity;
            if (string.IsNullOrWhiteSpace(mainActivity))
            {
                if (activityInfo.MainActivityCandidates.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple activity candidates found: {string.Join(", ", activityInfo.MainActivityCandidates)}. Pass --main-activity.");
                }

                mainActivity = activityInfo.MainActivity;
            }

            if (string.IsNullOrWhiteSpace(mainActivity))
            {
                throw new InvalidOperationException("Unable to determine main activity. Pass --main-activity explicitly.");
            }

            _logger.Info($"Main activity detected: {mainActivity}");

            if (!request.NoRes || request.ForceManifest)
            {
                var manifestReport = _manifestPatchService.Patch(decompiledDir);
                result.ManifestChanged = manifestReport.Changed;
                result.ManifestChangeSummary = string.Join("; ", manifestReport.Changes);
                _logger.Info($"Manifest modified: {(manifestReport.Changed ? "yes" : "no")}");
                _logger.Info($"Manifest patch details: {result.ManifestChangeSummary}");
            }
            else
            {
                result.ManifestChangeSummary = "Manifest patch skipped due to --no-res without --force-manifest.";
                _logger.Info("Manifest modified: no (skipped)");
            }

            var gadgetLibrary = _gadgetInjectionService.InjectBinaryAndAssets(
                decompiledDir,
                archResult.Architecture,
                gadget.Path,
                request.GadgetName,
                request.ConfigPath,
                request.ScriptPath,
                request.ScriptDelaySeconds);

            var smaliPatch = _smaliPatchService.InjectLoadLibrary(decompiledDir, mainActivity, gadgetLibrary);
            result.SmaliPatched = true;
            result.PatchedActivity = mainActivity;
            _logger.Info($"Injected gadget into activity: {mainActivity}");
            _logger.Debug($"Patched smali path: {smaliPatch.SmaliPath}");

            if (!request.SkipRecompile)
            {
                await _apktoolService.RunBuildAsync(
                    apktoolJarPath,
                    request.ApktoolCommandOverride,
                    decompiledDir,
                    request.UseAapt2,
                    request.RecompileOptions,
                    cancellationToken).ConfigureAwait(false);

                result.RebuildSucceeded = true;
                _logger.Info("APK rebuilt successfully");

                var builtApk = Path.Combine(decompiledDir, "dist", Path.GetFileName(request.ApkPath));
                if (!File.Exists(builtApk))
                {
                    throw new FileNotFoundException("Rebuilt APK not found.", builtApk);
                }

                _dexMergeService.PreserveOriginalDexes(request.ApkPath, builtApk, smaliPatch.DexClassNumber);

                var finalOutput = request.OutputPath;
                if (string.IsNullOrWhiteSpace(finalOutput))
                {
                    var parent = Path.GetDirectoryName(request.ApkPath) ?? Directory.GetCurrentDirectory();
                    finalOutput = Path.Combine(parent, Path.GetFileNameWithoutExtension(request.ApkPath) + "-patched.apk");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(finalOutput) ?? Directory.GetCurrentDirectory());
                File.Copy(builtApk, finalOutput, overwrite: true);
                result.OutputApkPath = finalOutput;

                if (request.Sign)
                {
                    var signer = await _toolRepository.ResolveUberApkSignerAsync(cancellationToken).ConfigureAwait(false);
                    _logger.Info($"{(signer.Downloaded ? "Downloading" : "Using cached")} uber-apk-signer: {signer.Path}");
                    await _signingService.SignAsync(signer.Path, finalOutput, request, cancellationToken).ConfigureAwait(false);
                    result.SigningSucceeded = true;
                    _logger.Info("APK signed successfully");
                }
                else
                {
                    result.Warnings.Add("Output APK is unsigned. Use --sign to sign automatically.");
                }
            }
            else
            {
                result.RebuildSucceeded = false;
                result.Warnings.Add("Rebuild skipped by --skip-recompile");
            }

            result.Success = true;
            _logger.Success("APK patched successfully");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger.Error(ex.Message);
            return result;
        }
    }
}
