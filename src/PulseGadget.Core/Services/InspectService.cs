using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class InspectService
{
    private readonly ArchitectureDetectionService _architectureDetectionService;
    private readonly ActivityDetectionService _activityDetectionService;
    private readonly ApktoolService _apktoolService;
    private readonly ToolRepository _toolRepository;

    public InspectService(
        ArchitectureDetectionService architectureDetectionService,
        ActivityDetectionService activityDetectionService,
        ApktoolService apktoolService,
        ToolRepository toolRepository)
    {
        _architectureDetectionService = architectureDetectionService;
        _activityDetectionService = activityDetectionService;
        _apktoolService = apktoolService;
        _toolRepository = toolRepository;
    }

    public async Task<InspectResult> InspectAsync(string apkPath, string? apktoolCommandOverride = null, CancellationToken cancellationToken = default)
    {
        var archs = _architectureDetectionService.DetectAllFromApkLibs(apkPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "pulse-gadget-inspect", Path.GetFileNameWithoutExtension(apkPath) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var apktool = await _toolRepository.ResolveApktoolJarAsync(cancellationToken).ConfigureAwait(false);
            await _apktoolService.RunDecompileAsync(apktool.Path, apktoolCommandOverride, apkPath, tempDir, false, false, [], cancellationToken).ConfigureAwait(false);
            var activity = _activityDetectionService.DetectFromDecompiledManifest(tempDir);

            return new InspectResult
            {
                ApkPath = apkPath,
                PackageName = activity.PackageName,
                MainActivity = activity.MainActivity,
                MainActivityCandidates = activity.MainActivityCandidates,
                ArchitecturesInApk = archs
            };
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
