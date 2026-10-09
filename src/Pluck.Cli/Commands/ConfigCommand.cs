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

public class ConfigCommandSettings : CommandSettings
{
    [CommandOption("--server")]
    [Description("The URL of the Pluck API instance")]
    public required string ServerUrl { get; set; }

    [CommandOption("--key")]
    [Description("The API key to use for authentication")]
    public required string ApiKey { get; set; }
}

public class ConfigCommand : AsyncCommand<ConfigCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, ConfigCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            PluckHttpClient.BaseAddress = new Uri(settings.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", settings.ApiKey);

            PingUserResponseDto? user = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Validating credentials...", async _ =>
                {
                    var response = await PluckHttpClient.GetAsync("/api/ping");
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Invalid API key. Ensure the API key and server is correct");
                    }

                    user = await response.Content.ReadFromJsonAsync<PingUserResponseDto>();
                    if (user is null)
                    {
                        throw new Exception("Unable to parse user info");
                    }

                    PluckConfigManager.Save(new PluckConfig(settings.ServerUrl, settings.ApiKey));
                });

            SpectreOutput.Success($"Welcome, {user!.Name}");
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to configure Pluck CLI: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}