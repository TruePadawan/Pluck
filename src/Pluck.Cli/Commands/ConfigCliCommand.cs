using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;

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
            var response = await PluckHttpClient.GetAsync("/api");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("Invalid API key. Ensure the API key and server is correct");
                return;
            }

            var user = await response.Content.ReadFromJsonAsync<PingUserResponseDto>();
            if (user is null)
            {
                throw new Exception("Failed to get user info");
            }

            PluckConfigManager.Save(new PluckConfig(ServerUrl, ApiKey));
            Console.WriteLine($"Welcome, {user.Name}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to configure Pluck CLI: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}