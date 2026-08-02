using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "get", Description = "Downloads a file from a Pluck instance", Parent = typeof(PluckCliCommand))]
public class GetCliCommand
{
    [CliArgument(Name = "url", Description = "The URL of the file to download")]
    public required string UrlLink { get; set; }

    [CliOption(Name = "save-dir", Description = "The directory to download the file to", Required = false)]
    public string? DownloadPath { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            var response = await PluckHttpClient.GetAsync(UrlLink, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("File not found. It may have expired or reached its download limit.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            // Get the filename from Content-Disposition header, falling back to the URL segment
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName
                           ?? Path.GetFileName(new Uri(UrlLink).AbsolutePath);
            if (DownloadPath is not null && !Directory.Exists(DownloadPath))
            {
                Directory.CreateDirectory(DownloadPath);
            }

            var saveDir = DownloadPath is not null
                ? Path.GetFullPath(DownloadPath)
                : Directory.GetCurrentDirectory();
            var savePath = Path.Combine(saveDir, fileName);

            var contentLength = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(savePath);

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

            SpectreOutput.Success($"Saved to {savePath}");
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to download file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}