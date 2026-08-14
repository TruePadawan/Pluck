using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Utils;
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
                    async Task<Results<NotFound<ErrorResponseDto>, UnauthorizedHttpResult, FileStreamHttpResult>> (
                        string token,
                        FileRepository fileRepository, IOptions<PluckApiOptions> apiOptions, HttpContext context,
                        [FromHeader(Name = "X-PLUCK-PASSWORD")]
                        string? providedPassword = null) =>
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

                        // Password check happens before decrementing downloads
                        if (file.IsPasswordProtected)
                        {
                            if (providedPassword is null)
                            {
                                context.Response.Headers.Append("X-PLUCK-PASSWORD-REQUIRED", "true");
                                return TypedResults.Unauthorized();
                            }

                            if (!PasswordHasher.Verify(providedPassword, file.PasswordHash!))
                            {
                                return TypedResults.Unauthorized();
                            }
                        }

                        await fileRepository.DecrementDownloadsLeft(file);
                        if (file.IsDirectory)
                        {
                            context.Response.Headers.Append("X-PLUCK-IS-DIRECTORY", "true");
                        }

                        // Stream the file from the disk
                        var filePath = Path.Combine(config.UploadDirectory, file.DiskFileName);
                        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        return TypedResults.File(fileStream, file.ContentType, file.OriginalFileName,
                            enableRangeProcessing: true);
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("DownloadFile")
                .WithSummary("Streams the uploaded file to the client")
                .WithDescription(
                    """
                    Streams the uploaded file to the client.
                    It returns 404 if the file is not found or is not downloadable.
                    A file is not downloadable if it has expired or if the download limit has been reached.
                    It returns 401 if the file is password protected and the password is missing or incorrect.
                    """);
        }
    }
}