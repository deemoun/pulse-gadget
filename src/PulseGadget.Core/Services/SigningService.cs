using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class SigningService
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogger _logger;

    public SigningService(ProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task SignAsync(string signerJarPath, string apkPath, PatchRequest request, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "-jar", signerJarPath, "--apks", apkPath };
        AppendIfPresent(args, "--ks", request.KeystorePath);
        AppendIfPresent(args, "--ksAlias", request.KeystoreAlias);
        AppendIfPresent(args, "--ksPass", request.KeystorePassword);
        AppendIfPresent(args, "--ksKeyPass", request.KeystoreKeyPassword);

        var result = await _processRunner.RunAsync("java", args, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"APK signing failed: {result.StdErr}\n{result.StdOut}");
        }

        _logger.Debug(result.StdOut);
    }

    private static void AppendIfPresent(ICollection<string> args, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add(key);
            args.Add(value);
        }
    }
}
