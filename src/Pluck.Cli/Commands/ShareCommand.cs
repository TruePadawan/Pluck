using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Spectre.Console.Cli;
using MimeMapping;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Lib;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class ShareCommandSettings : CommandSettings
{
    [CommandOption("--ttl")]
    [Description("How long the file/folder should exist for in hours")]
    public double Ttl { get; set; } = 24;

    [CommandOption("--downloads")]
    [Description("How many downloads the file/folder should allow")]
    public double? Downloads { get; set; } = null;

    [CommandOption("--pwd")]
    [Description("Password to protect the file/folder")]
    public string? Password { get; set; }

    [CommandOption("--token")]
    [Description("Token to be used instead of randomly generated token")]
    public string? Token { get; set; }

    [CommandArgument(0, "<path>")]
    [Description("The path to the file/folder to upload")]
    public required string ItemPath { get; set; }
}

public class ShareCommand : AsyncCommand<ShareCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, ShareCommandSettings settings,
        CancellationToken cancellationToken)
    {
        string? tempZipPath = null;
        try
        {
            var info = await SetupUploadAsync(settings, cancellationToken);
            tempZipPath = info.TempZipPath;
            var successResponse = await UploadFileAsync(info, cancellationToken);
            OutputUploadResult(successResponse, info.Password, settings.Password);
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to upload file: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
            // Delete the zip file used for folders if it was created
            if (tempZipPath is not null && File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }
    }

    /// <summary>
    /// Configures the HTTP client, validates custom tokens, sets up encryption, and prepares the file for upload.
    /// </summary>
    private static async Task<UploadInfo> SetupUploadAsync(ShareCommandSettings settings,
        CancellationToken cancellationToken)
    {
        var pluckConfig = PluckConfigManager.GetConfigOrThrow();

        PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
        PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);
        await ValidateCustomToken(settings.Token);

        PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-TTL",
            settings.Ttl.ToString(CultureInfo.InvariantCulture));
        if (settings.Downloads.HasValue)
        {
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-MAX-DOWNLOADS",
                settings.Downloads.Value.ToString(CultureInfo.InvariantCulture));
        }

        var (password, encryptor) = SetupEncryptionAsync(settings.Password);
        var absoluteItemPath = PrepareFileForUpload(settings.ItemPath);
        var (tempZipPath, fileName, fileSize) = ExtractFileInfo(absoluteItemPath, settings.ItemPath);

        if (tempZipPath != null)
        {
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-IS-DIRECTORY", "true");
        }

        return new UploadInfo(absoluteItemPath, fileName, fileSize, encryptor, password, tempZipPath);
    }

    /// <summary>
    /// Derives encryption keys from the password and configures the encryption headers.
    /// </summary>
    private static (string Password, ICryptoTransform Encryptor) SetupEncryptionAsync(string? password)
    {
        password = password ?? SharedUtilities.GenerateId(16);
        var salt = SecurityUtilities.GenerateSalt();
        var (encryptionKey, authKey) = SecurityUtilities.DeriveKeys(password, salt);
        var (IV, encryptor) = SecurityUtilities.SetupEncryption(encryptionKey);

        var saltBase64 = Convert.ToBase64String(salt);
        var IVBase64 = Convert.ToBase64String(IV);
        var authKeyBase64 = Convert.ToBase64String(authKey);

        PluckHttpClient.DefaultRequestHeaders.Add("X-Pluck-Salt", saltBase64);
        PluckHttpClient.DefaultRequestHeaders.Add("X-Pluck-IV", IVBase64);
        PluckHttpClient.DefaultRequestHeaders.Add("X-Pluck-AuthKey", authKeyBase64);

        return (password, encryptor);
    }

    /// <summary>
    /// Resolves the file path, zipping directories if necessary.
    /// </summary>
    private static string PrepareFileForUpload(string itemPath)
    {
        var absoluteItemPath = Path.GetFullPath(itemPath);
        if (Directory.Exists(absoluteItemPath))
        {
            var directoryInfo = new DirectoryInfo(absoluteItemPath);
            var tempZipPath = Path.Combine(Path.GetTempPath(), $"{directoryInfo.Name}.zip");
            ZipFile.CreateFromDirectory(absoluteItemPath, tempZipPath, CompressionLevel.Fastest, true);
            absoluteItemPath = tempZipPath;
        }
        return absoluteItemPath;
    }

    /// <summary>
    /// Extracts file metadata from the prepared file path.
    /// </summary>
    private static (string? TempZipPath, string FileName, long FileSize) ExtractFileInfo(
        string absoluteItemPath, string originalItemPath)
    {
        var tempZipPath = Directory.Exists(originalItemPath) ? absoluteItemPath : null;
        var fileName = Path.GetFileName(absoluteItemPath);
        var fileSize = new FileInfo(absoluteItemPath).Length;
        return (tempZipPath, fileName, fileSize);
    }

    /// <summary>
    /// Uploads the file with encryption and progress indication.
    /// </summary>
    private static async Task<FileResponseDto> UploadFileAsync(UploadInfo info, CancellationToken cancellationToken)
    {
        FileResponseDto? successResponse = null;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn())
            .StartAsync(async ctx =>
            {
                var uploadTask = ctx.AddTask($"Uploading [bold]{Markup.Escape(info.FileName)}[/]",
                    maxValue: info.FileSize);

                await using var fileStream = File.OpenRead(info.AbsoluteItemPath);
                await using var cryptoStream = new CryptoStream(fileStream, info.Encryptor, CryptoStreamMode.Read);
                await using var progressStream = new ProgressStream(cryptoStream,
                    bytesRead => uploadTask.Increment(bytesRead));
                using var fileContent = new StreamContent(progressStream);

                var fileMimeType = MimeUtility.GetMimeMapping(info.AbsoluteItemPath);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileMimeType);

                using var form = new MultipartFormDataContent();
                form.Add(fileContent, "file", info.FileName);

                var response = await PluckHttpClient.PostAsync("/api/upload", form, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new Exception(
                            "You're not authorized to upload to this Pluck instance. Please contact the server admin.");
                    }

                    var errorResponse =
                        await response.Content.ReadFromJsonAsync<ErrorResponseDto>(
                            cancellationToken: cancellationToken);
                    throw new Exception(errorResponse?.Error);
                }

                successResponse =
                    await response.Content.ReadFromJsonAsync<FileResponseDto>(cancellationToken: cancellationToken);
                if (successResponse is null)
                {
                    throw new Exception("Unable to parse file info");
                }

                uploadTask.Value = uploadTask.MaxValue;
            });

        return successResponse!;
    }

    /// <summary>
    /// Outputs the download URL, file details, and zero-knowledge warning.
    /// </summary>
    private static void OutputUploadResult(FileResponseDto successResponse, string password, string? originalPassword)
    {
        var isAutoGenerated = originalPassword == null;
        var finalDownloadUrl = isAutoGenerated
            ? $"{successResponse.DownloadUrl}#{password}"
            : successResponse.DownloadUrl;

        try
        {
            ClipboardHelper.Copy(finalDownloadUrl);
            SpectreOutput.Copied("Download URL");
        }
        catch (Exception e)
        {
            SpectreOutput.Warn($"Failed to copy download url to clipboard: {e.Message}");
        }

        var fileDetail = successResponse with { DownloadUrl = finalDownloadUrl };
        SpectreOutput.FileDetail(fileDetail);
        SpectreOutput.ZeroKnowledgeWarning(isAutoGenerated);
    }

    /// <summary>
    /// Validates the custom token provided by the user.
    /// </summary>
    private static async Task ValidateCustomToken(string? token)
    {
        if (token is not null)
        {
            string? finalToken = token;
            bool tokenIsUniqueAndValid = false;

            // We only enter this validation/reprompt flow because the user explicitly used the --token flag
            while (!tokenIsUniqueAndValid)
            {
                // If finalToken is null (because a previous iteration failed validation), reprompt the user
                if (finalToken == null)
                {
                    finalToken = AnsiConsole.Prompt(
                        new TextPrompt<string>(
                                "Please enter a valid token or leave it blank to generate a random one: ")
                            .AllowEmpty());
                }

                // If they left it blank during a reprompt, they opted out of a custom token. Break the loop.
                if (string.IsNullOrWhiteSpace(finalToken))
                {
                    finalToken = null;
                    break;
                }

                // Sanitize and validate locally
                var (sanitizedToken, isValid, errorMessage) = TokenSanitizer.Sanitize(finalToken);

                if (!isValid)
                {
                    Console.WriteLine($"[Error]: {errorMessage}");
                    finalToken = null;
                    continue;
                }

                // Validate uniqueness on the server
                var response = await PluckHttpClient.GetAsync($"/api/files/{sanitizedToken}");
                var tokenIsTaken = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;

                if (tokenIsTaken)
                {
                    Console.WriteLine($"[Error]: The token '{sanitizedToken}' is already taken on this server.");
                    finalToken = null;
                    continue;
                }

                // If we reach here, the token is both valid and unique
                finalToken = sanitizedToken;
                tokenIsUniqueAndValid = true;
            }

            // Attach the token to the upload request
            if (!string.IsNullOrEmpty(finalToken))
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-TOKEN", finalToken);
            }
        }
    }
}

/// <summary>
/// Carries all metadata needed for an upload operation between the setup, upload, and output phases.
/// </summary>
internal record UploadInfo(
    string AbsoluteItemPath,
    string FileName,
    long FileSize,
    ICryptoTransform Encryptor,
    string Password,
    string? TempZipPath);