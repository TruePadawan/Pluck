using System.Security.Cryptography;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Pluck.Shared.Dtos.Files;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Utils;

public static class Utilities
{
    public static string GenerateId(int length)
    {
        // Defines allowed alphanumeric characters
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNPQRSTUVWXYZ23456789";
        return string.Create(length, chars, (buffer, alphabet) =>
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }

    public static FileResponseDto GenerateFileResponse(File file, HttpRequest request)
    {
        var serverBaseUrl = $"{request.Scheme}://{request.Host}";
        var fileDownloadUrl = $"{serverBaseUrl}/f/{file.Token}";
        return new FileResponseDto(file.Token, file.OriginalFileName, file.DownloadsLeft, file.ExpiresAt,
            fileDownloadUrl, file.IsDirectory);
    }

    public static ApiVersionSet GetApiVersionSet(WebApplication app)
    {
        return app.NewApiVersionSet().HasApiVersion(new ApiVersion(1.0)).ReportApiVersions().Build();
    }
}