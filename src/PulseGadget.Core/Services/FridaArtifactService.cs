using System.Text.Json;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class FridaArtifactService
{
    private readonly ToolRepository _toolRepository;
    private readonly ToolDownloadService _downloadService;

    public FridaArtifactService(ToolRepository toolRepository, ToolDownloadService downloadService)
    {
        _toolRepository = toolRepository;
        _downloadService = downloadService;
    }

    public async Task<ToolResolutionResult> ResolveGadgetAsync(string fridaVersion, Architecture architecture, CancellationToken cancellationToken = default)
    {
        var normalizedVersion = fridaVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? fridaVersion[1..]
            : fridaVersion;
        var fridaArch = ArchitectureMapper.ToFridaArch(architecture);

        var versionDir = Path.Combine(_toolRepository.Paths.FridaRoot, normalizedVersion, fridaArch);
        var soPath = Path.Combine(versionDir, $"frida-gadget-{normalizedVersion}-android-{fridaArch}.so");
        if (File.Exists(soPath))
        {
            return new ToolResolutionResult { Path = soPath, Downloaded = false };
        }

        Directory.CreateDirectory(versionDir);

        var releaseApi = $"https://api.github.com/repos/frida/frida/releases/tags/{fridaVersion}";
        using var releaseDoc = await _downloadService.GetJsonAsync(releaseApi, cancellationToken).ConfigureAwait(false);
        var assets = releaseDoc.RootElement.GetProperty("assets").EnumerateArray();
        var expectedName = $"frida-gadget-{normalizedVersion}-android-{fridaArch}.so.xz";
        JsonElement match = default;
        var found = false;

        foreach (var asset in assets)
        {
            if (string.Equals(asset.GetProperty("name").GetString(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                match = asset;
                found = true;
                break;
            }
        }

        if (!found)
        {
            throw new FileNotFoundException($"Unable to locate Frida gadget asset: {expectedName}");
        }

        var xzPath = soPath + ".xz";
        var url = match.GetProperty("browser_download_url").GetString()
                  ?? throw new InvalidOperationException("Frida gadget asset has no download URL.");

        await _downloadService.DownloadFileAsync(url, xzPath, cancellationToken).ConfigureAwait(false);
        await _downloadService.DecompressXzAsync(xzPath, soPath, cancellationToken).ConfigureAwait(false);

        return new ToolResolutionResult { Path = soPath, Downloaded = true };
    }
}
