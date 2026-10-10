using System.Threading;
using System.ComponentModel;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Spectre.Console;

namespace Pluck.Cli.Commands;

public class GetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("The URL of the file/folder to download")]
    public required string UrlLink { get; set; }

    [CommandOption("--save-dir")]
    [Description("The directory to download the file/folder to")]
    public string? DownloadPath { get; set; }

    [CommandOption("--pwd")]
    [Description("Password for password-protected files")]
    public string? Password { get; set; }
}

public class GetCommand : AsyncCommand<GetCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, GetCommandSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();

            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            var uri = new Uri(settings.UrlLink);
            var password = settings.Password;
            if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(uri.Fragment))
            {
                password = uri.Fragment.TrimStart('#');
            }

            var downloadUrl = uri.GetLeftPart(UriPartial.Path);
            byte[]? finalEncryptionKey = null;
            byte[]? finalIv = null;

            var response = await PluckHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Handle encrypted files (which requires an AuthKey)
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                response.Headers.TryGetValues("X-PLUCK-AUTH-REQUIRED", out _))
            {
                if (!response.Headers.TryGetValues("X-Pluck-Salt", out var saltVals) ||
                    !response.Headers.TryGetValues("X-Pluck-IV", out var ivVals))
                {
                    throw new Exception("Server did not return cryptographic metadata.");
                }

                var salt = Convert.FromBase64String(saltVals.First());
                var iv = Convert.FromBase64String(ivVals.First());

                if (string.IsNullOrEmpty(password))
                {
                    password = AnsiConsole.Prompt(
                        new TextPrompt<string>("This file is encrypted. Enter password:")
                            .Secret());
                }

                var (encryptionKey, authKey) = SecurityUtilities.DeriveKeys(password, salt);
                var authKeyBase64 = Convert.ToBase64String(authKey);

                PluckHttpClient.DefaultRequestHeaders.Add("X-Pluck-AuthKey", authKeyBase64);
                response = await PluckHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Incorrect password.");
                }

                finalEncryptionKey = encryptionKey;
                finalIv = iv;
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("File/Folder not found. It may have expired or reached its download limit.");
                }

                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                throw new Exception(errorResponse?.Error);
            }

            // Get the filename from Content-Disposition header, falling back to the URL segment
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName
                           ?? Path.GetFileName(downloadUrl);
            if (settings.DownloadPath is not null && !Directory.Exists(settings.DownloadPath))
            {
                Directory.CreateDirectory(settings.DownloadPath);
            }

            var saveDir = settings.DownloadPath is not null
                ? Path.GetFullPath(settings.DownloadPath)
                : Directory.GetCurrentDirectory();
            var saveFilePath = Path.Combine(saveDir, fileName);

            var contentLength = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();

            Stream readStream = contentStream;
            System.Security.Cryptography.ICryptoTransform? decryptor = null;
            System.Security.Cryptography.Aes? aes = null;

            if (finalEncryptionKey != null && finalIv != null)
            {
                aes = System.Security.Cryptography.Aes.Create();
                aes.Key = finalEncryptionKey;
                aes.IV = finalIv;
                decryptor = aes.CreateDecryptor();
                readStream = new System.Security.Cryptography.CryptoStream(contentStream, decryptor,
                    System.Security.Cryptography.CryptoStreamMode.Read);
            }

            await using (var fileStream = File.Create(saveFilePath))
            {
                if (contentLength.HasValue)
                {
                    await AnsiConsole.Progress()
                        .Columns(
                            new TaskDescriptionColumn(),
                            new ProgressBarColumn(),
                            new PercentageColumn(),
                            new TransferSpeedColumn(),
                            new RemainingTimeColumn())
                        .StartAsync(async ctx =>
                        {
                            var downloadTask = ctx.AddTask(
                                $"Downloading [bold]{Markup.Escape(fileName)}[/]",
                                maxValue: contentLength.Value);

                            var buffer = new byte[81920];
                            int bytesRead;
                            while ((bytesRead = await readStream.ReadAsync(buffer)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                downloadTask.Increment(bytesRead);
                            }
                        });
                }
                else
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(new Style(Color.DodgerBlue1))
                        .StartAsync($"Downloading {Markup.Escape(fileName)}...",
                            async _ => { await readStream.CopyToAsync(fileStream); });
                }
            }

            // If it was a folder that was downloaded, extract it
            if (response.Headers.TryGetValues("X-PLUCK-IS-DIRECTORY", out _) && fileName.EndsWith(".zip"))
            {
                await ZipFile.ExtractToDirectoryAsync(saveFilePath, saveDir, overwriteFiles: true);
                File.Delete(saveFilePath);
                SpectreOutput.Success($"Saved to {saveDir}");
            }
            else
            {
                SpectreOutput.Success($"Saved to {saveFilePath}");
            }

            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to download file: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
            // Dispose of decryptor if it was used
            // CryptoStream takes care of itself and the underlying stream when disposed
        }
    }
}