using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "users", Description = "Lists all the users in the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class UsersCliCommand
{
    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
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
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to list users: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}