using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Shared.Dtos;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "revoke-user", Description = "Revokes a user from the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class RevokeUserCliCommand
{
    [CliArgument(Name = "name", Description = "The name of the user to revoke")]
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

            var response = await PluckHttpClient.DeleteAsync($"/api/admin/users/{Name}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("You're not authorized to revoke users. Only admins can revoke users.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            Console.WriteLine($"User '{Name}' revoked successfully");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to revoke user: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}