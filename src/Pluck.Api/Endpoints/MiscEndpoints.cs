using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Repositories;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Endpoints;

public static class MiscEndpoints
{
    public static void MapMiscEndpoints(this WebApplication app)
    {
        app.MapGet("/api",
            Results<UnauthorizedHttpResult, Ok<PingUserResponseDto>> (HttpContext context) =>
            {
                if (context.Items["User"] is not User user)
                {
                    return TypedResults.Unauthorized();
                }

                return TypedResults.Ok(new PingUserResponseDto(user.Name));
            });

        // Return the unexpired files uploaded by the current user; it returns all files if the user is an admin
        app.MapGet("/api/list",
            async Task<Results<UnauthorizedHttpResult, Ok<List<FileResponseDto>>>> (HttpContext context,
                FileRepository fileRepository, string? name) =>
            {
                if (context.Items["User"] is not User user)
                {
                    return TypedResults.Unauthorized();
                }

                List<File> files;
                if (user.Role == "Admin")
                {
                    if (name is not null)
                    {
                        files = await fileRepository.GetFilesByName(name);
                    }
                    else
                    {
                        files = await fileRepository.GetAllFiles();
                    }
                }
                else
                {
                    // A non-admin user can only see their own files
                    if (name is not null && name != user.Name)
                    {
                        return TypedResults.Unauthorized();
                    }

                    files = await fileRepository.GetFilesByName(user.Name);
                }

                var request = context.Request;
                var serverBaseUrl = $"{request.Scheme}://{request.Host}";
                var response = files.Select(f =>
                {
                    var fileDownloadUrl = $"{serverBaseUrl}/f/{f.Token}";
                    return new FileResponseDto(f.Token, f.OriginalFileName, f.DownloadsLeft, f.ExpiresAt,
                        fileDownloadUrl);
                }).ToList();
                return TypedResults.Ok(response);
            });
    }
}