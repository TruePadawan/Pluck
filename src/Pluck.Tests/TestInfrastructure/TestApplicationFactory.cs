using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Api.Services.Analytics;
using Moq;

namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// WebApplicationFactory that mocks external services and provides a temporary upload directory.
/// </summary>
public class TestApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly Mock<IAnalyticsQueue> _mockQueue;
    private readonly Mock<IGeoLocationService> _mockGeo;

    /// <summary>Creates a new test host with a fresh temp directory and mocked services.</summary>
    public TestApplicationFactory()
    {
        TempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(TempDir);
        _mockQueue = new Mock<IAnalyticsQueue>();
        _mockGeo = new Mock<IGeoLocationService>();
        _mockGeo.Setup(g => g.LookupLocation(It.IsAny<string>()))
            .Returns((null, null));
    }

    /// <summary>Path to the temporary upload directory for this test instance.</summary>
    public string TempDir { get; }

    /// <summary>Mock of the analytics queue service for assertion setup.</summary>
    public Mock<IAnalyticsQueue> MockQueue => _mockQueue;

    /// <summary>Mock of the geolocation service for assertion setup.</summary>
    public Mock<IGeoLocationService> MockGeo => _mockGeo;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            // Replace existing configuration with test-specific values
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "PluckApi:AdminKey", "test-admin-key" },
                { "PluckApi:UploadDirectory", TempDir }
            });
        });
        builder.ConfigureServices(services =>
        {
            // Replace the DB context with an in-memory SQLite instance
            var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite("Data Source=:memory:");
            });

            // Replace background services with mocked versions to avoid interference
            var queueDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAnalyticsQueue));
            if (queueDescriptor != null)
                services.Remove(queueDescriptor);
            services.AddSingleton(_mockQueue.Object);
            services.AddSingleton(_mockGeo.Object);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // After the host is created, ensure the in-memory DB schema exists and seed users
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        SeedUsers(db);
        return host;
    }

    private static void SeedUsers(AppDbContext db)
    {
        // Only seed if no users exist
        if (db.Users.Any())
            return;

        var adminKeyHash = KeyHasher.ComputeHash(AppDbContextFactory.AdminApiKey);
        var admin = Pluck.Shared.Models.User.Create(new Pluck.Shared.Params.UserParams
        {
            Name = "admin",
            ApiKeyHash = adminKeyHash,
            Role = "Admin"
        });
        db.Users.Add(admin);

        var userKeyHash = KeyHasher.ComputeHash(AppDbContextFactory.UserApiKey);
        var user = Pluck.Shared.Models.User.Create(new Pluck.Shared.Params.UserParams
        {
            Name = "testuser",
            ApiKeyHash = userKeyHash,
            Role = "User"
        });
        db.Users.Add(user);

        db.SaveChanges();
    }

    public new void Dispose()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected new virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            base.Dispose();
            if (Directory.Exists(TempDir))
            {
                try
                {
                    Directory.Delete(TempDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}