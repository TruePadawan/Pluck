using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Shared.Dtos;
using Pluck.Shared.Models;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Miscellaneous endpoints
/// </summary>
public static class MiscEndpoints
{
    public static void MapMiscEndpoints(this WebApplication app)
    {
        // Returns the authenticated user's name
        app.MapGet("/api/ping",
            Results<UnauthorizedHttpResult, Ok<PingUserResponseDto>> (HttpContext context) =>
            {
                if (context.Items["User"] is not User user)
                {
                    return TypedResults.Unauthorized();
                }

                return TypedResults.Ok(new PingUserResponseDto(user.Name));
            });
    }
}