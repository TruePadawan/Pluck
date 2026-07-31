using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Users;
using Pluck.Shared.Models;

namespace Pluck.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var adminRouteGroup = app.MapGroup("/api/admin");
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
                return TypedResults.Ok(new CreateUserResponseDto(newApiKey));
            });

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

                // Prevent deletion of the admin user
                if (name == adminUser.Name)
                {
                    return TypedResults.Conflict(new ErrorResponseDto("Cannot delete admin user"));
                }

                await userRepository.DeleteUserByName(name);
                return TypedResults.NoContent();
            });
    }
}