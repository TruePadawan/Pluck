using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class PurgeCommandSettings : CommandSettings
{
    [CommandArgument(0, "<token>")]
    [Description("The token of the file to delete")]
    public required string Token { get; set; }
}

public class PurgeCommand : AsyncCommand<PurgeCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, PurgeCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Deleting file...", async _ =>
                {
                    var response = await PluckHttpClient.DeleteAsync($"/api/files/{Uri.EscapeDataString(settings.Token)}");

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("You're not authorized to delete this file.");
                        }

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new Exception(
                                "File not found. It may have already expired or been deleted.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }
                });

            SpectreOutput.Success($"File '{settings.Token}' has been deleted.");
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to delete file: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}