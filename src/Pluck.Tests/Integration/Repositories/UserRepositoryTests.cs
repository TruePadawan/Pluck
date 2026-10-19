using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Persistence;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Dtos.Users;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Repositories;

[Collection("RepositoryTests")]
public class UserRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserRepository _repo;

    public UserRepositoryTests()
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

        _repo = new UserRepository(_db);
    }

    [Fact]
    public async Task GetByApiKeyHash_ExistingUser_ReturnsUser()
    {
        // Arrange
        var dto = new CreateUserDto("findme", KeyHasher.ComputeHash("find-key"), "User");
        await _repo.CreateUser(dto);

        // Act
        var user = await _repo.GetByApiKeyHash(KeyHasher.ComputeHash("find-key"));

        // Assert
        user.Should().NotBeNull();
        user!.Name.Should().Be("findme");
    }

    [Fact]
    public async Task GetByApiKeyHash_NonExistent_ReturnsNull()
    {
        // Act
        var user = await _repo.GetByApiKeyHash(KeyHasher.ComputeHash("unknown-key"));

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_SetsAllProperties()
    {
        // Arrange
        var dto = new CreateUserDto("newuser", KeyHasher.ComputeHash("new-key"), "User");

        // Act
        var user = await _repo.CreateUser(dto);

        // Assert
        user.Name.Should().Be("newuser");
        user.ApiKeyHash.Should().Be(KeyHasher.ComputeHash("new-key"));
        user.Role.Should().Be("User");
    }

    [Fact]
    public async Task CreateUser_NormalizesNameToLowercase()
    {
        // Arrange
        var dto = new CreateUserDto("NewUser", KeyHasher.ComputeHash("key"), "User");

        // Act
        var user = await _repo.CreateUser(dto);

        // Assert
        user.Name.Should().Be("newuser");
    }

    [Fact]
    public async Task NameExists_ExistingName_ReturnsTrue()
    {
        // Arrange
        var dto = new CreateUserDto("exists", KeyHasher.ComputeHash("key"), "User");
        await _repo.CreateUser(dto);

        // Act
        var exists = await _repo.NameExists("exists");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task NameExists_NonExistent_ReturnsFalse()
    {
        // Act
        var exists = await _repo.NameExists("noone");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task NameExists_CaseInsensitive()
    {
        // Arrange
        var dto = new CreateUserDto("testcase", KeyHasher.ComputeHash("key"), "User");
        await _repo.CreateUser(dto);

        // Act
        var exists = await _repo.NameExists("TestCase");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserByName_RemovesUser()
    {
        // Arrange
        var dto = new CreateUserDto("todelete", KeyHasher.ComputeHash("key"), "User");
        await _repo.CreateUser(dto);

        // Act
        await _repo.DeleteUserByName("todelete");

        // Assert
        var exists = await _repo.NameExists("todelete");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserByName_NonExistent_NoError()
    {
        // Act & Assert - should not throw
        await _repo.DeleteUserByName("noone");
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsers()
    {
        // Arrange
        var dto1 = new CreateUserDto("user1", KeyHasher.ComputeHash("key1"), "User");
        var dto2 = new CreateUserDto("user2", KeyHasher.ComputeHash("key2"), "User");
        await _repo.CreateUser(dto1);
        await _repo.CreateUser(dto2);

        // Act
        var users = (await _repo.GetAllUsers()).ToList();

        // Assert — includes the auto-seeded admin user
        users.Should().HaveCount(3);
        users.Select(u => u.Name).Should().BeEquivalentTo("admin", "user1", "user2");
    }

    public void Dispose()
    {
        _db?.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
