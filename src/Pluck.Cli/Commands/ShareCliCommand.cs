using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotMake.CommandLine;
using MimeMapping;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "share", Description = "Uploads a file/folder to the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class ShareCliCommand
{
    [CliOption(Name = "ttl", Description = "How long the file/folder should exist for in hours")]
    public double Ttl { get; set; } = 24;

    [CliOption(Name = "downloads", Description = "How many downloads the file/folder should allow", Required = false)]
    public double? Downloads { get; set; } = null;

    [CliOption(Name = "pwd", Description = "Password to protect the file/folder", Required = false)]
    public string? Password { get; set; }

    [CliArgument(Name = "path", Description = "The path to the file/folder to upload")]
    public required string ItemPath { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        string? tempZipPath = null;
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-TTL", Ttl.ToString(CultureInfo.InvariantCulture));
            if (Downloads.HasValue)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-MAX-DOWNLOADS",
                    Downloads.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (Password is not null)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-PASSWORD", Password);
            }

            var absoluteItemPath = Path.GetFullPath(ItemPath);
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
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to upload file: {e.Message}");
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
}