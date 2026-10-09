using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class UsersCommandSettings : CommandSettings
{
}

public class UsersCommand : AsyncCommand<UsersCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, UsersCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            List<UserResponseDto>? users = null;
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Fetching users...", async _ =>
                {
                    var response = await PluckHttpClient.GetAsync("/api/admin/users");
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

                    users = await response.Content.ReadFromJsonAsync<List<UserResponseDto>>();
                    if (users is null)
                    {
                        throw new Exception("Unable to parse user list");
                    }
                }
            );

            SpectreOutput.UserTable(users!);
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to list users: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}