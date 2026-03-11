using System.Text.Json;
using PulseGadget.Core.Abstractions;
using PulseGadget.Core.Models;

namespace PulseGadget.Core.Services;

public sealed class ToolRepository
{
    private const string ApktoolLatestUrl = "https://api.github.com/repos/iBotPeaches/Apktool/releases/latest";
    private const string SignerLatestUrl = "https://api.github.com/repos/patrickfav/uber-apk-signer/releases/latest";
    private const string FridaLatestUrl = "https://api.github.com/repos/frida/frida/releases/latest";

    private readonly CachePathProvider _cachePathProvider;
    private readonly ToolDownloadService _downloadService;
    private readonly ILogger _logger;

    public ToolRepository(CachePathProvider cachePathProvider, ToolDownloadService downloadService, ILogger logger)
    {
        _cachePathProvider = cachePathProvider;
        _downloadService = downloadService;
        _logger = logger;
    }

    public ToolPaths Paths => _cachePathProvider.GetPaths();

    public async Task<ToolResolutionResult> ResolveApktoolJarAsync(CancellationToken cancellationToken = default)
    {
        var destination = Paths.ApktoolJarPath;
        if (File.Exists(destination))
        {
            return new ToolResolutionResult { Path = destination, Downloaded = false };
        }

        using var doc = await _downloadService.GetJsonAsync(ApktoolLatestUrl, cancellationToken).ConfigureAwait(false);
        var assets = doc.RootElement.GetProperty("assets").EnumerateArray();
        var downloadUrl = assets
            .FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) == true)
            .GetProperty("browser_download_url").GetString();

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("Unable to find apktool .jar in latest release assets.");
        }

        await _downloadService.DownloadFileAsync(downloadUrl, destination, cancellationToken).ConfigureAwait(false);
        return new ToolResolutionResult { Path = destination, Downloaded = true };
    }

    public async Task<ToolResolutionResult> ResolveUberApkSignerAsync(CancellationToken cancellationToken = default)
    {
        var destination = Paths.UberApkSignerJarPath;
        if (File.Exists(destination))
        {
            return new ToolResolutionResult { Path = destination, Downloaded = false };
        }

        using var doc = await _downloadService.GetJsonAsync(SignerLatestUrl, cancellationToken).ConfigureAwait(false);
        var assets = doc.RootElement.GetProperty("assets").EnumerateArray().ToList();

        var jarAsset = assets.FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) == true);
        var checksumAsset = assets.FirstOrDefault(a => a.GetProperty("name").GetString()?.Contains("checksum", StringComparison.OrdinalIgnoreCase) == true);

        if (jarAsset.ValueKind == JsonValueKind.Undefined || checksumAsset.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Unable to find uber-apk-signer jar/checksum assets.");
        }

        var checksumPath = destination + ".sha256";
        var jarUrl = jarAsset.GetProperty("browser_download_url").GetString()!;
        var checksumUrl = checksumAsset.GetProperty("browser_download_url").GetString()!;

        await _downloadService.DownloadFileAsync(checksumUrl, checksumPath, cancellationToken).ConfigureAwait(false);
        await _downloadService.DownloadFileAsync(jarUrl, destination, cancellationToken).ConfigureAwait(false);

        var expected = (await File.ReadAllTextAsync(checksumPath, cancellationToken).ConfigureAwait(false))
            .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();
        var actual = _downloadService.ComputeSha256(destination);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destination);
            throw new InvalidOperationException("Downloaded uber-apk-signer checksum mismatch.");
        }

        return new ToolResolutionResult { Path = destination, Downloaded = true };
    }

    public async Task<string> ResolveFridaVersionAsync(string? explicitVersion, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(explicitVersion))
        {
            return explicitVersion;
        }

        using var doc = await _downloadService.GetJsonAsync(FridaLatestUrl, cancellationToken).ConfigureAwait(false);
        var tag = doc.RootElement.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidOperationException("Unable to resolve latest Frida version.");
        }

        _logger.Info($"Auto-detected Frida release tag: {tag}");
        return tag;
    }
}
