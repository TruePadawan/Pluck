using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using File = System.IO.File;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for upload-related actions
/// </summary>
public static class UploadEndpoints
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all upload-related endpoints
        /// </summary>
        public void MapUploadEndpoints()
        {
            app.MapUploadFile();
        }

        /// <summary>
        /// Uploads a file to the server and returns a download link
        /// </summary>
        private void MapUploadFile()
        {
            app.MapPost("/api/upload",
                    async Task<Results<BadRequest<ErrorResponseDto>,
                        InternalServerError<ErrorResponseDto>,
                        UnauthorizedHttpResult,
                        Created<FileResponseDto>>> (
                        HttpContext context, IOptions<PluckApiOptions> apiOptions,
                        FileRepository fileRepository,
                        [FromHeader(Name = "X-PLUCK-TTL")] double fileTtlInHours = 24,
                        [FromHeader(Name = "X-PLUCK-MAX-DOWNLOADS")]
                        int? fileMaxDownloads = null) =>
                    {
                        if (context.Items["User"] is not User user)
                        {
                            return TypedResults.Unauthorized();
                        }

                        // Verify that ttl is positive and maxDownloads is null or positive
                        if (fileTtlInHours <= 0)
                        {
                            return TypedResults.BadRequest(new ErrorResponseDto("TTL must be greater than 0"));
                        }

                        if (fileMaxDownloads <= 0)
                        {
                            return TypedResults.BadRequest(
                                new ErrorResponseDto("Max downloads if set must be greater than 0"));
                        }

                        var request = context.Request;
                        // Verify the request is a multipart request
                        if (!MultipartRequestHelper.IsMultipartRequest(request.ContentType))
                        {
                            return TypedResults.BadRequest(
                                new ErrorResponseDto("Invalid content type, Expected a multipart request"));
                        }

                        var boundary =
                            MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType!));
                        var reader = new MultipartReader(boundary, request.Body);
                        var fileSection = await reader.ReadNextSectionAsync();

                        // Validate section headers and process the file segment
                        if (fileSection != null &&
                            ContentDispositionHeaderValue.TryParse(fileSection.ContentDisposition,
                                out var contentDisposition))
                        {
                            if (contentDisposition.DispositionType.Equals("form-data") &&
                                !string.IsNullOrEmpty(contentDisposition.FileName.Value))
                            {
                                var originalFileName = Path.GetFileName(contentDisposition.FileName.Value);
                                var diskFileName = Utilities.GenerateId(8) + ".dat";
                                var uploadDirectory = apiOptions.Value.UploadDirectory;
                                if (string.IsNullOrEmpty(uploadDirectory))
                                {
                                    return TypedResults.InternalServerError(
                                        new ErrorResponseDto("Upload directory not configured"));
                                }

                                Directory.CreateDirectory(uploadDirectory);
                                var savePath = Path.Combine(uploadDirectory, diskFileName);
                                // stream file to disk
                                await using (var destinationStream = File.Create(savePath))
                                {
                                    await fileSection.Body.CopyToAsync(destinationStream);
                                }

                                var fileContentType = fileSection.ContentType ?? "application/octet-stream";
                                var fileDto = new CreateFileDto(user.Id, diskFileName, originalFileName,
                                    fileContentType,
                                    fileTtlInHours, fileMaxDownloads);
                                // Save the file entry in the database
                                var file = await fileRepository.CreateFile(fileDto);

                                // return data about the file and its download link
                                var result = Utilities.GenerateFileResponse(file, request);
                                return TypedResults.Created($"{uploadDirectory}/{file.Token}", result);
                            }
                        }

                        return TypedResults.BadRequest(new ErrorResponseDto("No valid file content was provided"));
                    }).Accepts<IFormFile>("multipart/form-data")
                .WithName("UploadFile")
                .WithSummary("Uploads a file to the server and returns a download link")
                .WithDescription("""
                                 It returns 401 if not authenticated or 400 if the request is not a multipart request
                                 or the expected header values are invalid.
                                 """);
        }
    }
}