using Pluck.Api.Security;
using Pluck.Shared.Models;
using Pluck.Shared.Params;

namespace Pluck.Tests.TestInfrastructure.Factories;

/// <summary>
/// Factory helpers for creating <see cref="User"/> test entities.
/// </summary>
public static class UserFactory
{
    /// <summary>Creates a user with the given overrides; defaults to the "User" role.</summary>
    public static User Create(
        string? name = null,
        string? apiKeyHash = null,
        string? role = "User")
    {
        var rawKey = name ?? "testuser";
        return User.Create(new UserParams
        {
            Name = name ?? "testuser",
            ApiKeyHash = apiKeyHash ?? KeyHasher.ComputeHash(rawKey),
            Role = role ?? "User"
        });
    }

    /// <summary>Creates a user with the "Admin" role.</summary>
    public static User CreateAdmin(string? name = null)
    {
        return Create(name: name ?? "admin", role: "Admin");
    }
}