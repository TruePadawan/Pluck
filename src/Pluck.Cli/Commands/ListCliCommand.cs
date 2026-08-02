using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "list", Description = "Lists files on the Pluck instance", Parent = typeof(PluckCliCommand))]
public class ListCliCommand
{
    [CliOption(Name = "name", Description = "Optional username to filter files by", Required = false)]
    public string? Name { get; set; }

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

            var url = Name is not null ? $"/api/files?name={Uri.EscapeDataString(Name)}" : "/api/list";
            var response = await PluckHttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("You're not authorized to list files on this Pluck instance.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            var files = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();
            if (files is null)
            {
                throw new Exception("Unable to parse file list");
            }

            foreach (var file in files)
            {
                Console.WriteLine(
                    $"Token: {file.Token} | Name: {file.OriginalFileName} | Downloads Left: {file.DownloadsLeft} | Expires At: {file.ExpiresAt} | Download URL: {file.DownloadUrl}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to list files: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}