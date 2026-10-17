using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class FileCommandSettings : CommandSettings
{
    [CommandArgument(0, "<token>")]
    [Description("The token of the file to retrieve")]
    public required string Token { get; set; }

    [CommandOption("-a|--analytics")]
    [Description("Show the file's analytics instead of general details")]
    public bool Analytics { get; set; }
}

public class FileCommand : AsyncCommand<FileCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, FileCommandSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            if (settings.Analytics)
            {
                var analytics = await FetchWithSpinnerAsync(
                    "Fetching file analytics...", async () =>
                    {
                        var response = await PluckHttpClient.GetAsync(
                            $"/api/files/{Uri.EscapeDataString(settings.Token)}/analytics", cancellationToken);
                        ThrowOnError(response, "File not found. It may have expired or been deleted.");
                        var analytics =
                            await response.Content.ReadFromJsonAsync<FileAnalyticsDto>(
                                cancellationToken: cancellationToken);
                        if (analytics is null)
                        {
                            throw new Exception("Unable to parse file analytics");
                        }

                        return analytics;
                    });
                SpectreOutput.FileAnalytics(analytics!);
            }
            else
            {
                var file = await FetchWithSpinnerAsync(
                    "Fetching file details...", async () =>
                    {
                        var response =
                            await PluckHttpClient.GetAsync($"/api/files/{Uri.EscapeDataString(settings.Token)}",
                                cancellationToken);
                        ThrowOnError(response, "File not found. It may have expired or reached its download limit.");
                        var file = await response.Content.ReadFromJsonAsync<FileResponseDto>(
                            cancellationToken: cancellationToken);
                        if (file is null)
                        {
                            throw new Exception("Unable to parse file details");
                        }

                        return file;
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

            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to retrieve file: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }

    /// <summary>
    /// Runs an async fetch operation with a spinner, displaying the provided status message.
    /// </summary>
    private static async Task<T> FetchWithSpinnerAsync<T>(string statusMessage, Func<Task<T>> fetchAsync)
    {
        T result = default!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Color.DodgerBlue1))
            .StartAsync(statusMessage, async _ => { result = await fetchAsync(); });
        return result;
    }

    /// <summary>
    /// Throws an appropriate exception if the HTTP response indicates an error.
    /// </summary>
    private static void ThrowOnError(HttpResponseMessage response, string notFoundMessage)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new Exception("You're not authorized to view this file.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception(notFoundMessage);
        }

        var errorResponse = response.Content.ReadFromJsonAsync<ErrorResponseDto>().Result;
        throw new Exception(errorResponse?.Error);
    }
}