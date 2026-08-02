using System.Net;
using System.Net.Http.Json;
using DotMake.CommandLine;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Spectre.Console;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "file", Description = "Displays the details of a file on the Pluck instance",
    Parent = typeof(PluckCliCommand))]
public class FileCliCommand
{
    [CliArgument(Name = "token", Description = "The token of the file to retrieve")]
    public required string Token { get; set; }

    private static readonly HttpClient PluckHttpClient = new();

    public async Task RunAsync()
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            FileResponseDto? file = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.DodgerBlue1))
                .StartAsync("Fetching file details...", async _ =>
                {
                    var response = await PluckHttpClient.GetAsync($"/api/files/{Uri.EscapeDataString(Token)}");

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("You're not authorized to view this file.");
                        }

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new Exception(
                                "File not found. It may have expired or reached its download limit.");
                        }

                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        throw new Exception(errorResponse?.Error);
                    }

                    file = await response.Content.ReadFromJsonAsync<FileResponseDto>();
                    if (file is null)
                    {
                        throw new Exception("Unable to parse file details");
                    }
                });

            try
            {
                ClipboardHelper.Copy(file!.DownloadUrl);
                SpectreOutput.Copied("Download URL");
            }
            catch (Exception e)
            {
                SpectreOutput.Warn($"Failed to copy download url to clipboard: {e.Message}");
            }

            SpectreOutput.FileDetail(file!);
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to retrieve file: {e.Message}");
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }
}