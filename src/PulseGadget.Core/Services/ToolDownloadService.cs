using System.Security.Cryptography;
using System.Text.Json;
using SharpCompress.Compressors.Xz;

namespace PulseGadget.Core.Services;

public sealed class ToolDownloadService
{
    private readonly HttpClient _httpClient;

    public ToolDownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PulseGadget/1.0");
        }
    }

    public async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        var tempPath = destinationPath + ".download";

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(tempPath);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        output.Close();

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public async Task DecompressXzAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
        {
            return;
        }

        await using var input = File.OpenRead(inputPath);
        await using var xz = new XZStream(input);
        await using var output = File.Create(outputPath);
        await xz.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    public string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
