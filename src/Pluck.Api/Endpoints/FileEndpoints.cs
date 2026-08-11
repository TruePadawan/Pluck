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
    private static RouteGroupBuilder GetRouteBuilder(WebApplication app) => app.MapGroup("/api/files");

    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all file-related endpoints
        /// </summary>
        public void MapFileEndpoints()
        {
            app.MapGetFile();
            app.MapGetFiles();
        }

        /// <summary>
        /// Return the unexpired files uploaded by the current user; it returns all files if the user is an admin
        /// </summary>
        private void MapGetFiles()
        {
            var builder = GetRouteBuilder(app);
            builder.MapGet("",
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
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("GetFiles")
                .WithSummary("Returns the files uploaded by the authenticated user")
                .WithDescription("""
                                 Returns the files uploaded by the authenticated user or all files if the user is an admin.
                                 It returns 401 if not authenticated or if a non-admin user tries to get files of another user.
                                 """);
        }

        /// <summary>
        /// Returns the details about the file associated with the token
        /// </summary>
        private void MapGetFile()
        {
            var builder = GetRouteBuilder(app);
            builder.MapGet("{token}",
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
                            return TypedResults.NotFound(
                                new ErrorResponseDto("Could not find file with specified token"));
                        }

                        if (file.OwnerId != user.Id)
                        {
                            return TypedResults.Unauthorized();
                        }

                        return TypedResults.Ok(Utilities.GenerateFileResponse(file, context.Request));
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("GetFile")
                .WithSummary("Returns the details about the file associated with the token")
                .WithDescription("""
                                 Returns the details about the file associated with the token.
                                 It returns 401 if not authenticated or if the file is not owned by the authenticated user,
                                 or 404 if the file is not found.
                                 """);
        }
    }
}