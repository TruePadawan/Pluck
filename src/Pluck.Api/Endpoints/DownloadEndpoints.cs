using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for download-related actions
/// </summary>
public static class DownloadEndpoints
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all download-related endpoints
        /// </summary>
        public void MapDownloadEndpoints()
        {
            app.MapDownloadFile();
        }

        /// <summary>
        /// Streams the uploaded file to the client
        /// </summary>
        private void MapDownloadFile()
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
                    }).WithName("DownloadFile")
                .WithSummary("Streams the uploaded file to the client")
                .WithDescription(
                    """
                    Streams the uploaded file to the client.
                    It returns 404 if the file is not found or is not downloadable.
                    A file is not downloadable if it has expired or if the download limit has been reached.
                    """);
        }
    }
}