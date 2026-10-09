using System.Threading;
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

    public override async Task<int> ExecuteAsync(CommandContext context, GetCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            if (settings.Password is not null)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-PASSWORD", settings.Password);
            }

            var response = await PluckHttpClient.GetAsync(settings.UrlLink, HttpCompletionOption.ResponseHeadersRead);

            // Handle password-protected files
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                response.Headers.TryGetValues("X-PLUCK-PASSWORD-REQUIRED", out _))
            {
                if (settings.Password is not null)
                {
                    // Password was provided but was incorrect
                    throw new Exception("Incorrect password.");
                }

                // Prompt for password interactively
                var enteredPassword = AnsiConsole.Prompt(
                    new TextPrompt<string>("This file is password protected. Enter password:")
                        .Secret());

                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-PASSWORD", enteredPassword);
                response = await PluckHttpClient.GetAsync(settings.UrlLink, HttpCompletionOption.ResponseHeadersRead);

                // Check if the entered password was also wrong
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

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            // Get the filename from Content-Disposition header, falling back to the URL segment
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName
                           ?? Path.GetFileName(new Uri(settings.UrlLink).AbsolutePath);
            if (settings.DownloadPath is not null && !Directory.Exists(settings.DownloadPath))
            {
                Directory.CreateDirectory(settings.DownloadPath);
            }

            var saveDir = settings.DownloadPath is not null
                ? Path.GetFullPath(settings.DownloadPath)
                : Directory.GetCurrentDirectory();
            var saveFilePath = Path.Combine(saveDir, fileName);

            var contentLength = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using (var fileStream = File.Create(saveFilePath))
            {
                if (contentLength.HasValue)
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
                            var downloadTask = ctx.AddTask(
                                $"Downloading [bold]{Markup.Escape(fileName)}[/]",
                                maxValue: contentLength.Value);

                            var buffer = new byte[81920];
                            int bytesRead;
                            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                downloadTask.Increment(bytesRead);
                            }
                        });
                }
                else
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(new Style(Color.DodgerBlue1))
                        .StartAsync($"Downloading {Markup.Escape(fileName)}...",
                            async _ => { await contentStream.CopyToAsync(fileStream); });
                }
            }

            // If it was a folder that was downloaded, extract it
            if (response.Headers.TryGetValues("X-PLUCK-IS-DIRECTORY", out _) && fileName.EndsWith(".zip"))
            {
                await ZipFile.ExtractToDirectoryAsync(saveFilePath, saveDir, overwriteFiles: true);
                File.Delete(saveFilePath);
                SpectreOutput.Success($"Saved to {saveDir}");
            }
            else
            {
                SpectreOutput.Success($"Saved to {saveFilePath}");
            }
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
}