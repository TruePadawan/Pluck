using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Pluck.Shared.Models;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for performing admin-related actions
/// </summary>
public static class AdminEndpoints
{
    private static RouteGroupBuilder GetRouteBuilder(WebApplication app) => app.MapGroup("/api/admin");

    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all admin-related endpoints
        /// </summary>
        public void MapAdminEndpoints()
        {
            app.MapCreateNonAdminUser();
            app.MapRemoveUser();
            app.MapGetAllUsers();
        }

        /// <summary>
        /// Creates a new non-admin user with the given unique name
        /// </summary>
        private void MapCreateNonAdminUser()
        {
            var builder = GetRouteBuilder(app);
            builder.MapPost("users",
                    async Task<Results<UnauthorizedHttpResult, Conflict<ErrorResponseDto>, Ok<CreateUserResponseDto>>> (
                        string name,
                        HttpContext context,
                        UserRepository userRepository) =>
                    {
                        if (context.Items["User"] is not User { Role: "Admin" })
                        {
                            return TypedResults.Unauthorized();
                        }

                        var newApiKey = Guid.NewGuid().ToString("N");
                        var apiKeyHash = KeyHasher.ComputeHash(newApiKey);
                        if (await userRepository.NameExists(name))
                        {
                            return TypedResults.Conflict(
                                new ErrorResponseDto($"The name {name} has already been used"));
                        }

                        await userRepository.CreateUser(new CreateUserDto(name, apiKeyHash, "User"));
                        return TypedResults.Ok(new CreateUserResponseDto(name, newApiKey));
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("CreateNonAdminUser")
                .WithSummary("Creates a new non-admin user")
                .WithDescription("""
                                 Returns the new user and their API key.
                                 It returns 401 if not authenticated as admin or 409 if the name is already used
                                 """);
        }

        /// <summary>
        /// Deletes the user with the given name
        /// </summary>
        private void MapRemoveUser()
        {
            var builder = GetRouteBuilder(app);
            builder.MapDelete("users/{name}",
                    async Task<Results<UnauthorizedHttpResult,
                        NoContent,
                        Conflict<ErrorResponseDto>,
                        NotFound<ErrorResponseDto>>>
                    (string name,
                        HttpContext context, UserRepository userRepository) =>
                    {
                        if (context.Items["User"] is not User { Role: "Admin" } adminUser)
                        {
                            return TypedResults.Unauthorized();
                        }

                        var normalizedName = name.ToLowerInvariant();

                        // Prevent deletion of the admin user
                        if (normalizedName == adminUser.Name)
                        {
                            return TypedResults.Conflict(new ErrorResponseDto("Cannot delete admin user"));
                        }

                        if (!await userRepository.NameExists(normalizedName))
                        {
                            return TypedResults.NotFound(
                                new ErrorResponseDto($"User with name {normalizedName} not found"));
                        }

                        await userRepository.DeleteUserByName(normalizedName);
                        return TypedResults.NoContent();
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("RemoveUser")
                .WithSummary("Deletes the user with the given name")
                .WithDescription("""
                                 Returns if 204 if the deletion was successful.
                                 It returns 401 if not authenticated as admin,
                                 409 if the name belongs to the admin user,
                                 404 if the user with the given name does not exist.
                                 """);
        }

        private void MapGetAllUsers()
        {
            var builder = GetRouteBuilder(app);
            builder.MapGet("/users",
                    async Task<Results<UnauthorizedHttpResult, Ok<IEnumerable<UserResponseDto>>>> (HttpContext context,
                        UserRepository userRepository) =>
                    {
                        if (context.Items["User"] is not User { Role: "Admin" } adminUser)
                        {
                            return TypedResults.Unauthorized();
                        }

                        var allUsers = await userRepository.GetAllUsers();
                        var response = allUsers.Select(user => new UserResponseDto(user.Name, user.Role));
                        return TypedResults.Ok(response);
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("GetAllUsers")
                .WithSummary("Returns all users in the Pluck instance");
        }
    }
}