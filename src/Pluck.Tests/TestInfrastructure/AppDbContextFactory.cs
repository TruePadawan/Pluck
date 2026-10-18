using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Shared.Models;
using Pluck.Shared.Params;

namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// Creates an in-memory SQLite <see cref="AppDbContext"/> seeded with known test users.
/// </summary>
public static class AppDbContextFactory
{
    /// <summary>API key for the seeded admin user.</summary>
    public const string AdminApiKey = "test-admin-key";

    /// <summary>API key for the seeded regular user.</summary>
    public const string UserApiKey = "test-user-key";

    /// <summary>Returns a fresh context with schema created and seed users inserted.</summary>
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new AppDbContext(options, null!);
        db.Database.EnsureCreated();
        SeedUsers(db);
        return db;
    }

    private static void SeedUsers(AppDbContext db)
    {
        var adminKeyHash = KeyHasher.ComputeHash(AdminApiKey);
        var admin = User.Create(new UserParams
        {
            Name = "admin",
            ApiKeyHash = adminKeyHash,
            Role = "Admin"
        });
        db.Users.Add(admin);

        var userKeyHash = KeyHasher.ComputeHash(UserApiKey);
        var user = User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = userKeyHash,
            Role = "User"
        });
        db.Users.Add(user);

        db.SaveChanges();
    }
}
