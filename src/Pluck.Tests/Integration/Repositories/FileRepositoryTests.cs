using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Persistence;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Repositories;

[Collection("RepositoryTests")]
public class FileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FileRepository _repo;
    private readonly User _user;

    public FileRepositoryTests()
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

        _repo = new FileRepository(_db);
    }

    [Fact]
    public async Task CreateFile_GeneratesToken()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, null, "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");

        // Act
        var file = await _repo.CreateFile(dto);

        // Assert
        file.Token.Should().HaveLength(6);
    }

    [Fact]
    public async Task CreateFile_WithCustomToken_UsesCustomToken()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "custom", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");

        // Act
        var file = await _repo.CreateFile(dto);

        // Assert
        file.Token.Should().Be("custom");
    }

    [Fact]
    public async Task CreateFile_SetsCorrectExpiration()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, null, "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");

        // Act
        var file = await _repo.CreateFile(dto);

        // Assert
        var expected = DateTime.UtcNow.AddHours(24);
        file.ExpiresAt.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetFileByToken_ExistingFile_ReturnsFile()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "exist", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto);

        // Act
        var file = await _repo.GetFileByToken("exist");

        // Assert
        file.Should().NotBeNull();
        file!.Token.Should().Be("exist");
    }

    [Fact]
    public async Task GetFileByToken_NonExistent_ReturnsNull()
    {
        // Act
        var file = await _repo.GetFileByToken("nonexist");

        // Assert
        file.Should().BeNull();
    }

    [Fact]
    public async Task GetFileByToken_ExpiredFile_ReturnsNull()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "expired", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto);

        // Bypass model validation to set expired date
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
            new SqliteParameter("@token", "expired"));

        // Act
        var file = await _repo.GetFileByToken("expired");

        // Assert
        file.Should().BeNull();
    }

    [Fact]
    public async Task GetFileByToken_ExhaustedDownloads_ReturnsNull()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "exhaust", "disk.txt", "original.txt", "text/plain",
            1024, 24, 0, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto);

        // Act
        var file = await _repo.GetFileByToken("exhaust");

        // Assert
        file.Should().BeNull();
    }

    [Fact]
    public async Task GetFileByToken_NullDownloadsLeft_ReturnsFile()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "unlimit", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto);

        // Act
        var file = await _repo.GetFileByToken("unlimit");

        // Assert
        file.Should().NotBeNull();
        file!.DownloadsLeft.Should().BeNull();
    }

    [Fact]
    public async Task DecrementDownloadsLeft_ReducesCount()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "decr", "disk.txt", "original.txt", "text/plain",
            1024, 24, 5, false, "hash", "salt", "iv");
        var file = await _repo.CreateFile(dto);

        // Act
        await _repo.DecrementDownloadsLeft(file);

        // Assert
        file.DownloadsLeft.Should().Be(4);
    }

    [Fact]
    public async Task GetFilesByName_ReturnsMatchingFiles()
    {
        // Arrange
        var dto1 = new CreateFileDto(
            _user.Id, "file1", "disk1.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var dto2 = new CreateFileDto(
            _user.Id, "file2", "disk2.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto1);
        await _repo.CreateFile(dto2);

        // Act
        var files = await _repo.GetFilesByName("test_user");

        // Assert
        files.Should().HaveCount(2);
        files.Select(f => f.Token).Should().BeEquivalentTo("file1", "file2");
    }

    [Fact]
    public async Task GetFilesByName_ExcludesExpired()
    {
        // Arrange
        var dto1 = new CreateFileDto(
            _user.Id, "valid", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var dto2 = new CreateFileDto(
            _user.Id, "old", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto1);
        await _repo.CreateFile(dto2);

        // Expire the second file
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
            new SqliteParameter("@token", "old"));

        // Act
        var files = await _repo.GetFilesByName("test_user");

        // Assert
        files.Should().HaveCount(1);
        files[0].Token.Should().Be("valid");
    }

    [Fact]
    public async Task GetAllFiles_ReturnsAllUnexpired()
    {
        // Arrange
        var otherUser = User.Create(new UserParams
        {
            Name = "other_user",
            ApiKeyHash = KeyHasher.ComputeHash("other-key"),
            Role = "User"
        });
        _db.Users.Add(otherUser);
        await _db.SaveChangesAsync();

        var dto1 = new CreateFileDto(
            _user.Id, "file1", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        var dto2 = new CreateFileDto(
            otherUser.Id, "file2", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto1);
        await _repo.CreateFile(dto2);

        // Act
        var files = await _repo.GetAllFiles();

        // Assert
        files.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteFileByToken_RemovesFile()
    {
        // Arrange
        var dto = new CreateFileDto(
            _user.Id, "delete", "disk.txt", "original.txt", "text/plain",
            1024, 24, null, false, "hash", "salt", "iv");
        await _repo.CreateFile(dto);

        // Act
        await _repo.DeleteFileByToken("delete");

        // Assert
        var file = await _repo.GetFileByToken("delete");
        file.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFileByToken_NonExistent_NoError()
    {
        // Act & Assert - should not throw
        await _repo.DeleteFileByToken("nonexist");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}