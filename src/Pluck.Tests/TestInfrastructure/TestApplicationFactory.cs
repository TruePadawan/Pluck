using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Api.Services.Analytics;
using Moq;
using Pluck.Shared.Models;
using Pluck.Shared.Params;

namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// WebApplicationFactory that mocks external services and provides a temporary upload directory.
/// </summary>
public class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Mock<IAnalyticsQueue> _mockQueue;
    private readonly Mock<IGeoLocationService> _mockGeo;
    private SqliteConnection? _connection;

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
    private string TempDir { get; }

    /// <summary>Mock of the analytics queue service for assertion setup.</summary>
    public Mock<IAnalyticsQueue> MockQueue => _mockQueue;

    /// <summary>Mock of the geolocation service for assertion setup.</summary>
    public Mock<IGeoLocationService> MockGeo => _mockGeo;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "PluckApi:AdminKey", SharedData.AdminApiKey },
                { "PluckApi:UploadDirectory", TempDir }
            });
        });
        builder.ConfigureServices(services =>
        {
            // Replace the DB context with an in-memory SQLite instance
            var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            services.AddDbContext<AppDbContext>(options => { options.UseSqlite(_connection); });

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

        SeedTestData(db);
        return host;
    }

    private static void SeedTestData(AppDbContext db)
    {
        var userKeyHash = KeyHasher.ComputeHash(SharedData.UserApiKey);
        var user = User.Create(new UserParams
        {
            Name = "test_user",
            ApiKeyHash = userKeyHash,
            Role = "User"
        });
        db.Users.Add(user);

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Close();
        _connection?.Dispose();
        if (Directory.Exists(TempDir))
        {
            try
            {
                Directory.Delete(TempDir, recursive: true);
            }
            catch
            {
                // Ignore clean-up errors
            }
        }
    }
}