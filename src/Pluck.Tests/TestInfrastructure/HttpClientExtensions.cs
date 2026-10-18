namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// Extension methods for authenticating <see cref="HttpClient"/> requests with seeded API keys.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>API key for the seeded admin user (matches <see cref="AppDbContextFactory.AdminApiKey"/>).</summary>
    public const string AdminApiKey = "test-admin-key";

    /// <summary>API key for the seeded regular user (matches <see cref="AppDbContextFactory.UserApiKey"/>).</summary>
    public const string UserApiKey = "test-user-key";

    /// <summary>Sets the <c>X-PLUCK-API-KEY</c> header to the admin user's key.</summary>
    public static void SetAdminApiKey(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", AdminApiKey);
    }

    /// <summary>Sets the <c>X-PLUCK-API-KEY</c> header to the regular user's key.</summary>
    public static void SetUserApiKey(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", UserApiKey);
    }
}
