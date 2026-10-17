using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Pluck.Cli.Utils;

/// <summary>
/// Helper class for resumable file downloads and decryption.
/// </summary>
public static class ResumableDownloadHelper
{
    public static string GetTempFilePath(string saveDir, string token)
    {
        return Path.Combine(saveDir, $"{token}.pluck.tmp");
    }

    private static long? GetPartialDownloadLength(string saveDir, string token)
    {
        var tmpFilePath = GetTempFilePath(saveDir, token);
        try
        {
            var length = new FileInfo(tmpFilePath).Length;
            return length > 0 ? length : null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task DownloadEncryptedFileAsync(
        string downloadUrl,
        string tmpFilePath,
        HttpClient client,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        var partialLength = GetPartialDownloadLength(Path.GetDirectoryName(tmpFilePath)!,
            Path.GetFileNameWithoutExtension(tmpFilePath));

        var isResuming = partialLength is > 0;
        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

        if (isResuming)
        {
            request.Headers.Range = new RangeHeaderValue(partialLength, null);
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (response.StatusCode != HttpStatusCode.OK &&
            response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new Exception($"Download failed with status code: {response.StatusCode}");
        }

        var fileMode = isResuming ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(tmpFilePath, fileMode, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous);

        var contentStream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        var totalBytesRead = 0L;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }

    public static async Task DecryptFileAsync(
        string tmpFilePath,
        string outputFilePath,
        byte[] encryptionKey,
        byte[] iv,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        await using var inputStream = new FileStream(tmpFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete,
            81920, FileOptions.Asynchronous);
        await using var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write,
            FileShare.None, 81920, FileOptions.Asynchronous);

        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        await using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

        var buffer = new byte[81920];
        var totalBytesRead = 0L;
        int bytesRead;

        while ((bytesRead = await cryptoStream.ReadAsync(buffer, ct)) > 0)
        {
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }

        File.Delete(tmpFilePath);
    }
}