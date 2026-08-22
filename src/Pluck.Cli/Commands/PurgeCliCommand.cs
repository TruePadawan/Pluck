using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "purge", Description = "Deletes a file from the Pluck instance ahead of expiration",
    Parent = typeof(PluckCliCommand))]
public class PurgeCliCommand
{
    [CliArgument(Name = "token", Description = "The token of the file to delete")]
    public required string Token { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Deleting file...", async _ =>
                {
                    var response = await PluckHttpClient.DeleteAsync($"/api/files/{Uri.EscapeDataString(Token)}");

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("You're not authorized to delete this file.");
                        }

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new Exception(
                                "File not found. It may have already expired or been deleted.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }
                });

            SpectreOutput.Success($"File '{Token}' has been deleted.");
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to delete file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}