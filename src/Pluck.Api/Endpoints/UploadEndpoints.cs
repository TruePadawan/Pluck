using Microsoft.AspNetCore.Http.HttpResults;
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

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this WebApplication app)
    {
        app.MapPost("/api/upload",
            async Task<Results<BadRequest<ErrorResponseDto>,
                InternalServerError<ErrorResponseDto>,
                UnauthorizedHttpResult,
                Created<CreateFileResponseDto>>> (
                HttpContext context, IOptions<PluckApiOptions> apiOptions,
                FileRepository fileRepository) =>
            {
                var request = context.Request;
                var config = apiOptions.Value;

                // Verify the request is a multipart request
                if (!MultipartRequestHelper.IsMultipartRequest(request.ContentType))
                {
                    return TypedResults.BadRequest(
                        new ErrorResponseDto("Invalid content type, Expected a multipart request"));
                }

                var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType!));
                var reader = new MultipartReader(boundary, request.Body);
                var fileSection = await reader.ReadNextSectionAsync();

                // Validate section headers and process the file segment
                if (fileSection != null &&
                    ContentDispositionHeaderValue.TryParse(fileSection.ContentDisposition, out var contentDisposition))
                {
                    if (contentDisposition.DispositionType.Equals("form-data") &&
                        !string.IsNullOrEmpty(contentDisposition.FileName.Value))
                    {
                        var originalFileName = Path.GetFileName(contentDisposition.FileName.Value);
                        var diskFileName = Utilities.GenerateId(8) + ".dat";
                        var uploadDirectory = config.UploadDirectory;
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

                        double ttl = 24;
                        int? maxDownloads = null;
                        if (context.Request.Headers.TryGetValue("X-PLUCK-TTL", out var ttlHeader))
                        {
                            ttl = double.Parse(ttlHeader.ToString());
                        }

                        if (context.Request.Headers.TryGetValue("X-PLUCK-MAX-DOWNLOADS", out var maxDownloadsHeader))
                        {
                            maxDownloads = int.Parse(maxDownloadsHeader.ToString());
                        }

                        if (context.Items["User"] is not User user)
                        {
                            return TypedResults.Unauthorized();
                        }

                        var fileDto = new CreateFileDto(user.Id, diskFileName, originalFileName, request.ContentType!,
                            ttl, maxDownloads);
                        var file = await fileRepository.CreateFile(fileDto);
                        var result = new CreateFileResponseDto(file.Token, file.OriginalFileName, file.DownloadsLeft,
                            file.ExpiresAt);
                        return TypedResults.Created($"{uploadDirectory}/{file.Token}", result);
                    }
                }

                return TypedResults.BadRequest(new ErrorResponseDto("No valid file content was provided"));
            }).Accepts<IFormFile>("multipart/form-data");
    }
}