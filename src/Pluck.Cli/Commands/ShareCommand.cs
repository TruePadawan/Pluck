using System.Threading;
using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public override async Task<int> ExecuteAsync(CommandContext context, ShareCommandSettings settings, CancellationToken cancellationToken)
    {
        string? tempZipPath = null;
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);
            // If a custom token was specified, check that it is unique and valid
            await ValidateCustomToken(settings.Token);

            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-TTL", settings.Ttl.ToString(CultureInfo.InvariantCulture));
            if (settings.Downloads.HasValue)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-MAX-DOWNLOADS",
                    settings.Downloads.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (settings.Password is not null)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-PASSWORD", settings.Password);
            }

            var absoluteItemPath = Path.GetFullPath(settings.ItemPath);
            if (Directory.Exists(absoluteItemPath))
            {
                // Zip the folder and extract its info
                var directoryInfo = new DirectoryInfo(absoluteItemPath);
                tempZipPath = Path.Combine(Path.GetTempPath(), $"{directoryInfo.Name}.zip");
                await ZipFile.CreateFromDirectoryAsync(absoluteItemPath, tempZipPath, CompressionLevel.Fastest, true);
                absoluteItemPath = tempZipPath;
                // Add header that tells the backend that a folder is being uploaded
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-IS-DIRECTORY", "true");
            }

            var fileName = Path.GetFileName(absoluteItemPath);
            var fileSize = new FileInfo(absoluteItemPath).Length;

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
                    var uploadTask = ctx.AddTask($"Uploading [bold]{Markup.Escape(fileName)}[/]",
                        maxValue: fileSize);

                    await using var fileStream = File.OpenRead(absoluteItemPath);
                    await using var progressStream = new ProgressStream(fileStream,
                        bytesRead => uploadTask.Increment(bytesRead));
                    using var fileContent = new StreamContent(progressStream);

                    // Set the content-type of the file
                    var fileMimeType = MimeUtility.GetMimeMapping(absoluteItemPath);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileMimeType);

                    using var form = new MultipartFormDataContent();
                    // Attach file to form body
                    form.Add(fileContent, "file", fileName);

                    var response = await PluckHttpClient.PostAsync("/api/upload", form);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception(
                                "You're not authorized to upload to this Pluck instance. Please contact the server admin.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }

                    successResponse = await response.Content.ReadFromJsonAsync<FileResponseDto>();
                    if (successResponse is null)
                    {
                        throw new Exception("Unable to parse file info");
                    }

                    // Ensure the progress bar reaches 100%
                    uploadTask.Value = uploadTask.MaxValue;
                });

            try
            {
                ClipboardHelper.Copy(successResponse!.DownloadUrl);
                SpectreOutput.Copied("Download URL");
            }
            catch (Exception e)
            {
                SpectreOutput.Warn($"Failed to copy download url to clipboard: {e.Message}");
            }

            SpectreOutput.FileDetail(successResponse!);
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
    /// Validates the custom token provided by the user.
    /// </summary>
    private async Task ValidateCustomToken(string? token)
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
                    finalToken = AnsiConsole.Ask<string>(
                        "Please enter a valid token or leave it blank to generate a random one: ", "");
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