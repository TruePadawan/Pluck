using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using TextCopy;

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
            var pluckConfig = PluckConfigManager.GetConfig();
            if (pluckConfig is null)
            {
                throw new Exception("Pluck config not found. Please run 'pluck config' to set up the config");
            }

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            var response = await PluckHttpClient.PostAsync($"/api/admin/users?name={Name}", null);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("You're not authorized to create users. Only admins can create users.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                Console.WriteLine($"Failed to create user: {errorResponse?.Error}");
            }

            var successResponse = await response.Content.ReadFromJsonAsync<CreateUserResponseDto>();
            if (successResponse is null)
            {
                throw new Exception("Unable to parse API Key");
            }

            await ClipboardService.SetTextAsync(successResponse.ApiKey);
            Console.WriteLine($"User '{Name}' created successfully. The API Key has been copied to your clipboard.");
            Console.WriteLine($"API Key: {successResponse.ApiKey}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to create user: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}