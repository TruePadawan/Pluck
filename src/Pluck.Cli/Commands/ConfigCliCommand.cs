using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

/// <summary>
/// Defines the config subcommand which points the Pluck CLI to a Pluck Api instance
/// </summary>
[CliCommand(Name = "config",
    Description = "Link the Pluck CLI to a Pluck API instance",
    Parent = typeof(PluckCliCommand))]
public class ConfigCliCommand
{
    [CliOption(Name = "server", Description = "The URL of the Pluck API instance")]
    public required string ServerUrl { get; set; }

    [CliOption(Name = "key", Description = "The API key to use for authentication")]
    public required string ApiKey { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            PluckHttpClient.BaseAddress = new Uri(ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", ApiKey);

            PingUserResponseDto? user = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Validating credentials...", async _ =>
                {
                    var response = await PluckHttpClient.GetAsync("/api");
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Invalid API key. Ensure the API key and server is correct");
                    }

                    user = await response.Content.ReadFromJsonAsync<PingUserResponseDto>();
                    if (user is null)
                    {
                        throw new Exception("Unable to parse user info");
                    }

                    PluckConfigManager.Save(new PluckConfig(ServerUrl, ApiKey));
                });

            SpectreOutput.Success($"Welcome, {user!.Name}");
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to configure Pluck CLI: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}