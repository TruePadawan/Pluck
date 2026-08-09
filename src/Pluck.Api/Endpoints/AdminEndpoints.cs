using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Pluck.Shared.Models;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for performing admin-related actions
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var adminRouteGroup = app.MapGroup("/api/admin");

        // CREATE A NON-ADMIN USER
        adminRouteGroup.MapPost("/users",
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
                    return TypedResults.Conflict(new ErrorResponseDto($"The name {name} has already been used"));
                }

                await userRepository.CreateUser(new CreateUserDto(name, apiKeyHash, "User"));
                return TypedResults.Ok(new CreateUserResponseDto(name, newApiKey));
            });

        // REMOVES A USER FROM THE PLUCK INSTANCE
        adminRouteGroup.MapDelete("/users/{name}",
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
                    return TypedResults.NotFound(new ErrorResponseDto($"User with name {normalizedName} not found"));
                }

                await userRepository.DeleteUserByName(normalizedName);
                return TypedResults.NoContent();
            });
    }
}