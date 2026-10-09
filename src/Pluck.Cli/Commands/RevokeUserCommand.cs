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

public class RevokeUserCommandSettings : CommandSettings
{
    [CommandArgument(0, "<name>")]
    [Description("The name of the user to revoke")]
    public required string Name { get; set; }

    [CommandOption("-f|--force")]
    [Description("Skip confirmation prompt")]
    public bool Force { get; set; } = false;
}

public class RevokeUserCommand : AsyncCommand<RevokeUserCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, RevokeUserCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            if (!settings.Force)
            {
                var confirmed = AnsiConsole.Confirm(
                    $"Are you sure you want to revoke user [bold red]{Markup.Escape(settings.Name)}[/]?",
                    defaultValue: false);
                if (!confirmed)
                {
                    SpectreOutput.Info("Operation cancelled.");
                    return 0;
                }
            }

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync($"Revoking user '{settings.Name}'...", async _ =>
                {
                    var response = await PluckHttpClient.DeleteAsync($"/api/admin/users/{settings.Name}");
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

            SpectreOutput.Success($"User '{settings.Name}' revoked successfully.");
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to revoke user: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}