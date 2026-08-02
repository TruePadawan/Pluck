using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;

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
            var pluckConfig = PluckConfigManager.GetConfig();
            if (pluckConfig is null)
            {
                throw new Exception("Pluck config not found. Please run 'pluck config' to set up the config");
            }

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            var response = await PluckHttpClient.GetAsync(UrlLink);

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

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(savePath);
            await contentStream.CopyToAsync(fileStream);

            Console.WriteLine($"File downloaded successfully: {savePath}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to download file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}