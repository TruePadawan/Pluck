using System.ComponentModel;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class GetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("The URL of the file/folder to download")]
    public required string UrlLink { get; set; }

    [CommandOption("--save-dir")]
    [Description("The directory to download the file/folder to")]
    public string? DownloadPath { get; set; }

    [CommandOption("--pwd")]
    [Description("Password for password-protected files")]
    public string? Password { get; set; }
}

public class GetCommand : AsyncCommand<GetCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, GetCommandSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await SetupDownloadAsync(settings, cancellationToken);
            await DownloadFileAsync(info, cancellationToken);
            await DecryptOrRenameAsync(info);
            PostProcessDownload(info);
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to download file: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }

    /// <summary>
    /// Parses the URL, negotiates authentication, and collects metadata needed for the download.
    /// </summary>
    private static async Task<DownloadInfo> SetupDownloadAsync(GetCommandSettings settings,
        CancellationToken cancellationToken)
    {
        var pluckConfig = PluckConfigManager.GetConfigOrThrow();

        PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
        PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

        var uri = new Uri(settings.UrlLink);
        var password = settings.Password;
        if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(uri.Fragment))
        {
            password = uri.Fragment.TrimStart('#');
        }

        var downloadUrl = uri.GetLeftPart(UriPartial.Path);
        byte[]? encryptionKey = null;
        byte[]? iv = null;

        var response = await PluckHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        // Handle encrypted files (which requires an AuthKey)
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            response.Headers.TryGetValues("X-PLUCK-AUTH-REQUIRED", out _))
        {
            (encryptionKey, iv) = await NegotiateAuthAsync(downloadUrl, password, cancellationToken);
            response = await PluckHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new Exception("Incorrect password.");
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new Exception("File/Folder not found. It may have expired or reached its download limit.");
            }

            var errorResponse =
                await response.Content.ReadFromJsonAsync<ErrorResponseDto>(cancellationToken: cancellationToken);
            throw new Exception(errorResponse?.Error);
        }

        var fileName = GetFileName(response, downloadUrl);
        var saveDir = GetSaveDir(settings);
        var saveFilePath = Path.Combine(saveDir, fileName);

        var token = downloadUrl.TrimEnd('/').Split('/').Last();
        var tmpFilePath = ResumableDownloadHelper.GetTempFilePath(saveDir, token);

        var contentLength = response.Content.Headers.ContentLength;
        var isDirectory = response.Headers.TryGetValues("X-PLUCK-IS-DIRECTORY", out _);

        response.Dispose();

        return new DownloadInfo(
            fileName, downloadUrl, saveDir, saveFilePath, tmpFilePath,
            contentLength, isDirectory, encryptionKey, iv);
    }

    /// <summary>
    /// Handles the password authentication flow for encrypted files.
    /// </summary>
    private static async Task<(byte[] EncryptionKey, byte[] Iv)> NegotiateAuthAsync(
        string downloadUrl, string? password, CancellationToken cancellationToken)
    {
        var response = await PluckHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.Headers.TryGetValues("X-Pluck-Salt", out var saltVals) ||
            !response.Headers.TryGetValues("X-Pluck-IV", out var ivVals))
        {
            throw new Exception("Server did not return cryptographic metadata.");
        }

        var salt = Convert.FromBase64String(saltVals.First());
        var iv = Convert.FromBase64String(ivVals.First());

        if (string.IsNullOrEmpty(password))
        {
            password = AnsiConsole.Prompt(
                new TextPrompt<string>("This file is encrypted. Enter password:")
                    .Secret());
        }

        var (encryptionKey, authKey) = SecurityUtilities.DeriveKeys(password, salt);
        var authKeyBase64 = Convert.ToBase64String(authKey);

        PluckHttpClient.DefaultRequestHeaders.Add("X-Pluck-AuthKey", authKeyBase64);

        return (encryptionKey, iv);
    }

    /// <summary>
    /// Downloads the file with a progress indication, showing a progress bar or spinner depending on whether the content length is known.
    /// </summary>
    private static async Task DownloadFileAsync(DownloadInfo info, CancellationToken cancellationToken)
    {
        if (info.ContentLength.HasValue)
        {
            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var downloadProgressTask = ctx.AddTask(
                        $"Downloading [bold]{Markup.Escape(info.FileName)}[/]",
                        maxValue: info.ContentLength.Value);

                    var progress = new Progress<long>(bytes => downloadProgressTask.Value = bytes);
                    await ResumableDownloadHelper.DownloadEncryptedFileAsync(
                        info.DownloadUrl, info.TmpFilePath, PluckHttpClient, progress, cancellationToken);
                });
        }
        else
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync($"Downloading {Markup.Escape(info.FileName)}...",
                    async _ =>
                    {
                        await ResumableDownloadHelper.DownloadEncryptedFileAsync(
                            info.DownloadUrl, info.TmpFilePath, PluckHttpClient, null, cancellationToken);
                    });
        }
    }

    /// <summary>
    /// Decrypts the downloaded file or renames the temp file if no encryption was used.
    /// </summary>
    private static async Task DecryptOrRenameAsync(DownloadInfo info)
    {
        if (info.EncryptionKey != null && info.Iv != null)
        {
            var tmpFileInfo = new FileInfo(info.TmpFilePath);
            var decryptMaxValue = tmpFileInfo.Length;

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var decryptProgressTask = ctx.AddTask(
                        $"Decrypting [bold]{Markup.Escape(info.FileName)}[/]",
                        maxValue: decryptMaxValue);

                    var progress = new Progress<long>(bytes => decryptProgressTask.Value = bytes);
                    await ResumableDownloadHelper.DecryptFileAsync(
                        info.TmpFilePath, info.SaveFilePath, info.EncryptionKey, info.Iv,
                        progress, CancellationToken.None);
                });
        }
        else
        {
            // Not encrypted: rename temp file to final file
            File.Move(info.TmpFilePath, info.SaveFilePath);
        }
    }

    /// <summary>
    /// Extracts the downloaded zip if it was a directory upload, and prints the success message.
    /// </summary>
    private static void PostProcessDownload(DownloadInfo info)
    {
        if (info.IsDirectory && info.FileName.EndsWith(".zip"))
        {
            // Synchronous extraction is fine here; the async version blocks on I/O anyway
            ZipFile.ExtractToDirectory(info.SaveFilePath, info.SaveDir, overwriteFiles: true);
            File.Delete(info.SaveFilePath);
            SpectreOutput.Success($"Saved to {info.SaveDir}");
        }
        else
        {
            SpectreOutput.Success($"Saved to {info.SaveFilePath}");
        }
    }

    /// <summary>
    /// Extracts the filename from the response's Content-Disposition header, falling back to the URL segment.
    /// </summary>
    private static string GetFileName(HttpResponseMessage response, string downloadUrl)
    {
        return response.Content.Headers.ContentDisposition?.FileNameStar
               ?? response.Content.Headers.ContentDisposition?.FileName
               ?? Path.GetFileName(downloadUrl);
    }

    /// <summary>
    /// Resolves the save directory from settings, creating it if necessary.
    /// </summary>
    private static string GetSaveDir(GetCommandSettings settings)
    {
        if (settings.DownloadPath is not null)
        {
            if (!Directory.Exists(settings.DownloadPath))
            {
                Directory.CreateDirectory(settings.DownloadPath);
            }

            return Path.GetFullPath(settings.DownloadPath);
        }

        return Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Carries all metadata needed for a download operation between the setup, download, and post-processing phases.
/// </summary>
internal record DownloadInfo(
    string FileName,
    string DownloadUrl,
    string SaveDir,
    string SaveFilePath,
    string TmpFilePath,
    long? ContentLength,
    bool IsDirectory,
    byte[]? EncryptionKey,
    byte[]? Iv);