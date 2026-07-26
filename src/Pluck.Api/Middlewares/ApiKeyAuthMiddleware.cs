using Pluck.Api.Repositories;
using Pluck.Api.Security;

namespace Pluck.Api.Middlewares;

public class ApiKeyAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, UserRepository userRepository)
    {
        // Skip auth for non-api paths
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Pluck-Api-Key", out var apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Error = "API Key missing" });
            return;
        }

        var apiKeyHash = KeyHasher.ComputeHash(apiKey.ToString());
        var user = await userRepository.GetByApiKeyHash(apiKeyHash);
        if (user == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Error = "Invalid API Key" });
            return;
        }

        context.Items["User"] = user;

        await next(context);
    }
}