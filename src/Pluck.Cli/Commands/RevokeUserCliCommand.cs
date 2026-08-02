using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "revoke-user", Description = "Revokes a user from the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class RevokeUserCliCommand
{
    [CliArgument(Name = "name", Description = "The name of the user to revoke")]
    public required string Name { get; set; }

    [CliOption(Name = "force", Description = "Skip confirmation prompt", Required = false)]
    public bool Force { get; set; } = false;

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            if (!Force)
            {
                var confirmed = AnsiConsole.Confirm(
                    $"Are you sure you want to revoke user [bold red]{Markup.Escape(Name)}[/]?",
                    defaultValue: false);
                if (!confirmed)
                {
                    SpectreOutput.Info("Operation cancelled.");
                    return;
                }
            }

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync($"Revoking user '{Name}'...", async _ =>
                {
                    var response = await PluckHttpClient.DeleteAsync($"/api/admin/users/{Name}");
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception(
                                "You're not authorized to revoke users. Only admins can revoke users.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }
                });

            SpectreOutput.Success($"User '{Name}' revoked successfully.");
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to revoke user: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}