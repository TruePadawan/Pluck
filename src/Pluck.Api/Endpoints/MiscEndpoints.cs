using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Shared.Dtos;
using Pluck.Shared.Models;

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
    }
}