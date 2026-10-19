using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Api.Workers;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;
using File = System.IO.File;
using FileModel = Pluck.Shared.Models.File;

namespace Pluck.Tests.Workers;

/// <summary>
/// Tests for <see cref="FileCleanupBackgroundService"/>.
/// Calls CleanUpFiles directly to avoid waiting for the 10-minute timer.
/// </summary>
public class FileCleanupBackgroundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly string _uploadDir;
    private readonly FileCleanupBackgroundService _service;
    private readonly User _user;

    public FileCleanupBackgroundServiceTests()
    {
        _uploadDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_uploadDir);

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options, Options.Create(new PluckApiOptions
        {
            AdminKey = SharedData.AdminApiKey,
            UploadDirectory = _uploadDir
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

        // Set up service with scope factory
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(Options.Create(new PluckApiOptions
        {
            AdminKey = SharedData.AdminApiKey,
            UploadDirectory = _uploadDir
        }));
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = Mock.Of<ILogger<FileCleanupBackgroundService>>();

        _service = new FileCleanupBackgroundService(logger, scopeFactory);
    }

    private async Task InvokeCleanUpFilesAsync()
    {
        await _service.CleanUpFiles(CancellationToken.None);
    }

    [Fact]
    public async Task CleanUpFiles_RemovesExpiredFilesFromDb()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "expired", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var file = await CreateFile(dto);
        var token = file.Token;

        // Bypass model validation to set expired date
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
            new SqliteParameter("@token", token));

        // Act
        await InvokeCleanUpFilesAsync();

        // Assert
        var remaining = await _db.Files.AnyAsync(f => f.Token == token);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task CleanUpFiles_RemovesExpiredFilesFromDisk()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "diskexpired", "diskfile.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var file = await CreateFile(dto);

        // Create the disk file
        var filePath = Path.Combine(_uploadDir, file.DiskFileName);
        await File.WriteAllTextAsync(filePath, "test content");
        File.Exists(filePath).Should().BeTrue();

        // Expire the file
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
            new SqliteParameter("@token", file.Token));

        // Act
        await InvokeCleanUpFilesAsync();

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task CleanUpFiles_RemovesExhaustedDownloadFiles()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "exhausted", "disk.txt", "original.txt", "text/plain",
            1024, 24, 0, false, "hash", "salt", "iv");
        var file = await CreateFile(dto);

        // Act
        await InvokeCleanUpFilesAsync();

        // Assert
        var remaining = await _db.Files.AnyAsync(f => f.Token == file.Token);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task CleanUpFiles_PreservesValidFiles()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "valid", "disk.txt", "original.txt", "text/plain",
            1024, 24, 5, false, "hash", "salt", "iv");
        var file = await CreateFile(dto);

        // Act
        await InvokeCleanUpFilesAsync();

        // Assert
        var remaining = await _db.Files.AnyAsync(f => f.Token == file.Token);
        remaining.Should().BeTrue();
    }

    [Fact]
    public async Task CleanUpFiles_HandlesMissingDiskFile()
    {
        // Arrange - create expired file WITHOUT creating the disk file
        var dto = new CreateFileDto(
            _user.Id, "nodelta", "missing.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var file = await CreateFile(dto);

        // Expire the file (no disk file created)
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
            new SqliteParameter("@token", file.Token));

        // Act & Assert - should not throw
        await InvokeCleanUpFilesAsync();
        var remaining = await _db.Files.AnyAsync(f => f.Token == file.Token);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task CleanUpFiles_HandlesEmptyDatabase()
    {
        // Arrange - no files in database

        // Act & Assert - should not throw
        var act = InvokeCleanUpFilesAsync;
        await act.Should().NotThrowAsync();
    }

    private async Task<FileModel> CreateFile(CreateFileDto dto)
    {
        var fileParams = new FileParams
        {
            Token = dto.Token ?? Guid.NewGuid().ToString(),
            OwnerId = dto.OwnerId,
            DiskFileName = dto.DiskFileName,
            OriginalFileName = dto.OriginalFileName,
            ContentType = dto.ContentType,
            Size = dto.Size,
            DownloadsLeft = dto.MaxDownloads,
            ExpiresAt = DateTime.UtcNow.AddHours(dto.Ttl),
            IsDirectory = dto.IsDirectory,
            AuthHash = dto.AuthHash,
            Salt = dto.Salt,
            IV = dto.IV
        };
        var file = FileModel.Create(fileParams);
        _db.Files.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
        if (Directory.Exists(_uploadDir))
        {
            Directory.Delete(_uploadDir, recursive: true);
        }
    }
}