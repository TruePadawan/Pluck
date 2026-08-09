using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Repositories;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for file-related actions
/// </summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var filesRouteGroup = app.MapGroup("/api/files");

        // Return the unexpired files uploaded by the current user; it returns all files if the user is an admin
        filesRouteGroup.MapGet("",
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
                var response = files.Select(f => Utilities.GenerateFileResponse(f, request)).ToList();
                return TypedResults.Ok(response);
            });

        // Returns the details about the file associated with the token
        filesRouteGroup.MapGet("/{token}",
            async Task<Results<UnauthorizedHttpResult, NotFound<ErrorResponseDto>, Ok<FileResponseDto>>> (
                HttpContext context, string token, FileRepository fileRepository) =>
            {
                if (context.Items["User"] is not User user)
                {
                    return TypedResults.Unauthorized();
                }

                var file = await fileRepository.GetFileByToken(token);
                if (file is null)
                {
                    return TypedResults.NotFound(new ErrorResponseDto("Could not find file with specified token"));
                }

                if (file.OwnerId != user.Id)
                {
                    return TypedResults.Unauthorized();
                }

                return TypedResults.Ok(Utilities.GenerateFileResponse(file, context.Request));
            });
    }
}