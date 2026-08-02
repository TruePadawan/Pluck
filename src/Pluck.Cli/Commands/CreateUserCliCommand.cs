using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "create-user",
    Description = "Generates a new non-admin user and copies the API key to the clipboard",
    Parent = typeof(PluckCliCommand))]
public class CreateUserCliCommand
{
    [CliArgument(Name = "name", Description = "The name of the user to create")]
    public required string Name { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            CreateUserResponseDto? successResponse = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync($"Creating user '{Name}'...", async _ =>
                {
                    var response = await PluckHttpClient.PostAsync($"/api/admin/users?name={Name}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception(
                                "You're not authorized to create users. Only admins can create users.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }

                    successResponse = await response.Content.ReadFromJsonAsync<CreateUserResponseDto>();
                    if (successResponse is null)
                    {
                        throw new Exception("Unable to parse API Key");
                    }
                });

            try
            {
                ClipboardHelper.Copy(successResponse!.ApiKey);
                SpectreOutput.Copied("API Key");
            }
            catch (Exception e)
            {
                SpectreOutput.Warn($"Failed to copy API Key to clipboard: {e.Message}");
            }

            SpectreOutput.Success($"User '{Name}' created successfully.");
            SpectreOutput.ApiKeyPanel(successResponse!.ApiKey);
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to create user: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}