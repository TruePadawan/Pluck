namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// Extension methods for authenticating <see cref="HttpClient"/> requests with seeded API keys.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>Sets the <c>X-PLUCK-API-KEY</c> header to the admin user's key.</summary>
    public static void SetAdminApiKey(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", SharedData.AdminApiKey);
    }

    /// <summary>Sets the <c>X-PLUCK-API-KEY</c> header to the regular user's key.</summary>
    public static void SetUserApiKey(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", SharedData.UserApiKey);
    }
}