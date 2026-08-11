using Microsoft.AspNetCore.Http.HttpResults;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Models;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Miscellaneous endpoints
/// </summary>
public static class MiscEndpoints
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all miscellaneous endpoints
        /// </summary>
        public void MapMiscEndpoints()
        {
            app.MapPingUser();
        }

        /// <summary>
        /// Returns the authenticated user's name
        /// </summary>
        private void MapPingUser()
        {
            app.MapGet("/api/ping",
                    Results<UnauthorizedHttpResult, Ok<PingUserResponseDto>> (HttpContext context) =>
                    {
                        if (context.Items["User"] is not User user)
                        {
                            return TypedResults.Unauthorized();
                        }

                        return TypedResults.Ok(new PingUserResponseDto(user.Name));
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("PingUser")
                .WithSummary("Pings the authenticated user")
                .WithDescription("Returns the authenticated user's name. Returns 401 if not authenticated.");
        }
    }
}