using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class ListCommandSettings : CommandSettings
{
    [CommandOption("--name")]
    [Description("Optional username to filter files by")]
    public string? Name { get; set; }
}

public class ListCommand : AsyncCommand<ListCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, ListCommandSettings settings, CancellationToken cancellationToken)
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
                    var url = settings.Name is not null
                        ? $"/api/files?name={Uri.EscapeDataString(settings.Name)}"
                        : "/api/files";
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

                    files = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();
                    if (files is null)
                    {
                        throw new Exception("Unable to parse file list");
                    }
                });

            SpectreOutput.FileTable(files!);
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to list files: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}