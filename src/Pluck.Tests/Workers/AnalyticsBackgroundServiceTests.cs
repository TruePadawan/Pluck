using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Api.Services.Analytics;
using Pluck.Api.Workers;
using Pluck.Shared.Models;
using Pluck.Shared.Models.Events;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;
using Pluck.Tests.TestInfrastructure.Factories;
using File = Pluck.Shared.Models.File;

namespace Pluck.Tests.Workers;

/// <summary>
/// Tests for <see cref="AnalyticsBackgroundService"/>.
/// Calls ProcessAnalyticsMessageAsync directly to test analytics processing.
/// </summary>
public class AnalyticsBackgroundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AnalyticsBackgroundService _service;
    private readonly Mock<IGeoLocationService> _geoLocationService;
    private readonly User _user;

    public AnalyticsBackgroundServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options, Options.Create(new PluckApiOptions
        {
            AdminKey = SharedData.AdminApiKey,
            UploadDirectory = Path.GetTempPath()
        }));
        _db.Database.EnsureCreated();

        // Seed a test user
        var userKeyHash = KeyHasher.ComputeHash(SharedData.UserApiKey);
        _user = User.Create(new UserParams
        {
            Name = "test_user",
            ApiKeyHash = userKeyHash,
            Role = "User"
        });
        _db.Users.Add(_user);
        _db.SaveChanges();

        _geoLocationService = new Mock<IGeoLocationService>();

        // Set up service with scope factory
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(_geoLocationService.Object);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = Mock.Of<ILogger<AnalyticsBackgroundService>>();
        var mockQueue = new Mock<IAnalyticsQueue>();

        _service = new AnalyticsBackgroundService(
            mockQueue.Object, scopeFactory, _geoLocationService.Object, logger);
    }

    private async Task InvokeProcessMessageAsync(DownloadAnalyticsMessage message)
    {
        await _service.ProcessAnalyticsMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_IncrementsDownloadCount()
    {
        // Arrange
        var file = CreateFileInDb();
        var initialCount = file.DownloadCount;
        var message = DownloadAnalyticsMessageFactory.Create(fileId: file.Id);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        await _db.Entry(file).ReloadAsync();
        file.DownloadCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task ProcessMessage_CreatesDownloadEvent()
    {
        // Arrange
        var file = CreateFileInDb();
        var message = DownloadAnalyticsMessageFactory.Create(fileId: file.Id);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        var events = await _db.FileDownloadEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].FileId.Should().Be(file.Id);
    }

    [Fact]
    public async Task ProcessMessage_HashesIpAddress()
    {
        // Arrange
        var file = CreateFileInDb();
        const string ip = "192.168.1.1";
        var message = new DownloadAnalyticsMessage(file.Id, ip, "PluckCli/1.0", ClientType.PluckCli);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        var downloadEvent = await _db.FileDownloadEvents.FirstAsync();
        downloadEvent.HashedIp.Should().NotBe(ip);
        downloadEvent.HashedIp.Should().NotBeNullOrEmpty();
        // SHA-256 produces a 64-character hex string
        downloadEvent.HashedIp.Length.Should().Be(64);
    }

    [Fact]
    public async Task ProcessMessage_WithNullIp_SetsUnknown()
    {
        // Arrange
        var file = CreateFileInDb();
        var message = new DownloadAnalyticsMessage(file.Id, null, null, ClientType.Unknown);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        var downloadEvent = await _db.FileDownloadEvents.FirstAsync();
        downloadEvent.HashedIp.Should().Be("Unknown");
    }

    [Fact]
    public async Task ProcessMessage_DeletedFile_NoError()
    {
        // Arrange
        var message = DownloadAnalyticsMessageFactory.Create(fileId: Guid.NewGuid());

        // Act & Assert - should not throw when file doesn't exist
        await InvokeProcessMessageAsync(message);

        var events = await _db.FileDownloadEvents.ToListAsync();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessage_WithGeoLocation_PopulatesCountry()
    {
        // Arrange
        var file = CreateFileInDb();
        var ip = "8.8.8.8";
        _geoLocationService.Setup(g => g.LookupLocation(ip))
            .Returns(("Mountain View", "US"));

        var message = new DownloadAnalyticsMessage(file.Id, ip, "PluckCli/1.0", ClientType.PluckCli);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        var downloadEvent = await _db.FileDownloadEvents.FirstAsync();
        downloadEvent.Country.Should().Be("US");
        downloadEvent.City.Should().Be("Mountain View");
    }

    [Fact]
    public async Task ProcessMessage_WithoutGeoLocation_NullCountry()
    {
        // Arrange
        var file = CreateFileInDb();
        var ip = "1.2.3.4";
        _geoLocationService.Setup(g => g.LookupLocation(ip))
            .Returns((null, null));

        var message = new DownloadAnalyticsMessage(file.Id, ip, "PluckCli/1.0", ClientType.PluckCli);

        // Act
        await InvokeProcessMessageAsync(message);

        // Assert
        var downloadEvent = await _db.FileDownloadEvents.FirstAsync();
        downloadEvent.Country.Should().BeNull();
        downloadEvent.City.Should().BeNull();
    }

    private File CreateFileInDb()
    {
        var file = FileFactory.Create(ownerId: _user.Id);
        _db.Files.Add(file);
        _db.SaveChanges();
        return file;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}