using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "file", Description = "Displays the details of a file on the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class FileCliCommand
{
    [CliArgument(Name = "token", Description = "The token of the file to retrieve")]
    public required string Token { get; set; }

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

            var response = await PluckHttpClient.GetAsync($"/api/files/{Uri.EscapeDataString(Token)}");

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("You're not authorized to view this file.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("File not found. It may have expired or reached its download limit.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            var file = await response.Content.ReadFromJsonAsync<FileResponseDto>();
            if (file is null)
            {
                throw new Exception("Unable to parse file details");
            }

            try
            {
                ClipboardHelper.Copy(file.DownloadUrl);
                Console.WriteLine("The file download url has been copied to your clipboard.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to copy download url to clipboard: {e.Message}");
            }

            Console.WriteLine($"Token: {file.Token}");
            Console.WriteLine($"Name: {file.OriginalFileName}");
            Console.WriteLine($"Downloads Left: {file.DownloadsLeft}");
            Console.WriteLine($"Expires At: {file.ExpiresAt}");
            Console.WriteLine($"Download URL: {file.DownloadUrl}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to retrieve file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}