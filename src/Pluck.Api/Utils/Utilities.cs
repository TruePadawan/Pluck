using Asp.Versioning;
using Asp.Versioning.Builder;
using Pluck.Shared.Dtos.Files;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Utils;

public static class Utilities
{
    public static FileResponseDto GenerateFileResponse(File file, HttpRequest request)
    {
        var serverBaseUrl = $"{request.Scheme}://{request.Host}";
        var fileDownloadUrl = $"{serverBaseUrl}/f/{file.Token}";
        return new FileResponseDto(file.Token, file.OriginalFileName, file.DownloadsLeft, file.ExpiresAt,
            fileDownloadUrl, file.IsDirectory, file.IsPasswordProtected, file.DownloadCount);
    }

    public static ApiVersionSet GetApiVersionSet(WebApplication app)
    {
        return app.NewApiVersionSet().HasApiVersion(new ApiVersion(1.0)).ReportApiVersions().Build();
    }
}