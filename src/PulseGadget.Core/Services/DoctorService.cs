using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class DoctorService
{
    private readonly ProcessRunner _processRunner;
    private readonly ToolRepository _toolRepository;

    public DoctorService(ProcessRunner processRunner, ToolRepository toolRepository)
    {
        _processRunner = processRunner;
        _toolRepository = toolRepository;
    }

    public async Task<DoctorResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = new DoctorResult();

        result.CachePath = _toolRepository.Paths.CacheRoot;
        result.ApktoolCached = File.Exists(_toolRepository.Paths.ApktoolJarPath);
        result.UberApkSignerCached = File.Exists(_toolRepository.Paths.UberApkSignerJarPath);

        var java = await TryRunAsync("java", ["-version"], cancellationToken).ConfigureAwait(false);
        result.JavaAvailable = java;
        result.Notes.Add(java ? "java available" : "java missing (required for apktool and signer)");

        var adb = await TryRunAsync("adb", ["version"], cancellationToken).ConfigureAwait(false);
        result.AdbAvailable = adb;
        result.Notes.Add(adb ? "adb available" : "adb missing (optional for auto arch detection)");

        return result;
    }

    private async Task<bool> TryRunAsync(string cmd, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        try
        {
            var output = await _processRunner.RunAsync(cmd, args, cancellationToken).ConfigureAwait(false);
            return output.Success;
        }
        catch
        {
            return false;
        }
    }
}
