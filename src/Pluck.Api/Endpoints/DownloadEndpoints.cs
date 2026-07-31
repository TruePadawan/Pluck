using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos;

namespace Pluck.Api.Endpoints;

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this WebApplication app)
    {
        app.MapGet("/f/{token}",
            async Task<Results<NotFound<ErrorResponseDto>, FileStreamHttpResult>> (string token,
                FileRepository fileRepository, IOptions<PluckApiOptions> apiOptions) =>
            {
                var file = await fileRepository.GetFileByToken(token);
                if (file is null)
                {
                    return TypedResults.NotFound(new ErrorResponseDto("File not found"));
                }

                var config = apiOptions.Value;
                if (!file.IsDownloadable(config.UploadDirectory))
                {
                    return TypedResults.NotFound(new ErrorResponseDto("File not found"));
                }

                await fileRepository.DecrementDownloadsLeft(file);
                // Stream the file from the disk
                var filePath = Path.Combine(config.UploadDirectory, file.DiskFileName);
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return TypedResults.File(fileStream, file.ContentType, file.OriginalFileName,
                    enableRangeProcessing: true);
            });
    }
}