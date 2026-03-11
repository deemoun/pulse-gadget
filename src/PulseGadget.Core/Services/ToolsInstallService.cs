using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class ToolsInstallService
{
    private readonly ToolRepository _toolRepository;
    private readonly FridaArtifactService _fridaArtifactService;

    public ToolsInstallService(ToolRepository toolRepository, FridaArtifactService fridaArtifactService)
    {
        _toolRepository = toolRepository;
        _fridaArtifactService = fridaArtifactService;
    }

    public async Task<List<string>> InstallAsync(bool apktool, bool signer, string? fridaVersion, Architecture? arch, CancellationToken cancellationToken = default)
    {
        var logs = new List<string>();

        if (apktool)
        {
            var r = await _toolRepository.ResolveApktoolJarAsync(cancellationToken).ConfigureAwait(false);
            logs.Add($"apktool {r.Description}: {r.Path}");
        }

        if (signer)
        {
            var r = await _toolRepository.ResolveUberApkSignerAsync(cancellationToken).ConfigureAwait(false);
            logs.Add($"uber-apk-signer {r.Description}: {r.Path}");
        }

        if (!string.IsNullOrWhiteSpace(fridaVersion) && arch is not null)
        {
            var r = await _fridaArtifactService.ResolveGadgetAsync(fridaVersion, arch.Value, cancellationToken).ConfigureAwait(false);
            logs.Add($"frida gadget {r.Description}: {r.Path}");
        }

        return logs;
    }
}
