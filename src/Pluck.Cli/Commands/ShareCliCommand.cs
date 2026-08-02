using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotMake.CommandLine;
using MimeMapping;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using TextCopy;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "share", Description = "Uploads a file to the Pluck instance", Parent = typeof(PluckCliCommand))]
public class ShareCliCommand
{
    [CliOption(Name = "ttl", Description = "How long the file should exist for in hours")]
    public double Ttl { get; set; } = 24;

    [CliOption(Name = "downloads", Description = "How many downloads the file should allow")]
    public double? Downloads { get; set; } = null;

    [CliArgument(Name = "filepath", Description = "The path to the file to upload")]
    public required string FilePath { get; set; }

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
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-TTL", Ttl.ToString(CultureInfo.InvariantCulture));
            if (Downloads.HasValue)
            {
                PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-MAX-DOWNLOADS",
                    Downloads.Value.ToString(CultureInfo.InvariantCulture));
            }

            var absoluteFilePath = Path.GetFullPath(FilePath);

            await using var fileStream = File.OpenRead(absoluteFilePath);
            using var fileContent = new StreamContent(fileStream);

            // Set the content-type of the file
            var fileMimeType = MimeUtility.GetMimeMapping(absoluteFilePath);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileMimeType);

            using var form = new MultipartFormDataContent();
            // Attach file to form body
            form.Add(fileContent, "file", Path.GetFileName(absoluteFilePath));

            var response = await PluckHttpClient.PostAsync($"/api/upload", form);
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

            var successResponse = await response.Content.ReadFromJsonAsync<FileResponseDto>();
            if (successResponse is null)
            {
                throw new Exception("Unable to parse file info");
            }

            try
            {
                await ClipboardService.SetTextAsync(successResponse.DownloadUrl);
                Console.WriteLine("The download url has been copied to your clipboard.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to copy download url to clipboard: {e.Message}");
            }

            Console.WriteLine($"Download URL: {successResponse.DownloadUrl}");
            Console.WriteLine($"Token: {successResponse.Token}");
            Console.WriteLine($"Downloads Left: {successResponse.DownloadsLeft}");
            Console.WriteLine($"Expires At: {successResponse.ExpiresAt}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to upload file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}