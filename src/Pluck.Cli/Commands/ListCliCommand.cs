using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Spectre.Console;

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
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            List<FileResponseDto>? files = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Fetching files...", async _ =>
                {
                    var url = Name is not null
                        ? $"/api/files?name={Uri.EscapeDataString(Name)}"
                        : "/api/files";
                    var response = await PluckHttpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception(
                                "You're not authorized to list files on this Pluck instance.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }

                    files = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();
                    if (files is null)
                    {
                        throw new Exception("Unable to parse file list");
                    }
                });

            SpectreOutput.FileTable(files!);
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to list files: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}