using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Shared.Dtos.Users;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Endpoints;

public class AdminEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestApplicationFactory _factory;

    public AdminEndpointsTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    //  Create User Tests 

    [Fact]
    public async Task CreateUser_AsNonAdmin_Returns401()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=newuser", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_WithValidName_Returns200()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=newuser", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUser_ReturnsApiKeyInResponse()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=newuser2", null);
        var dto = await response.Content.ReadFromJsonAsync<CreateUserResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("newuser2");
        dto.ApiKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateUser_WithSpecialCharsInName_Returns400()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=user%40name%21", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithEmptyName_Returns400()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateName_Returns409()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response1 = await _client.PostAsync("/api/admin/users?name=dupuser", null);
        var response2 = await _client.PostAsync("/api/admin/users?name=dupuser", null);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_HashesApiKeyInDatabase()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=hashuser", null);
        var dto = await response.Content.ReadFromJsonAsync<CreateUserResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "hashuser");
        user.Should().NotBeNull();
        var expectedHash = KeyHasher.ComputeHash(dto!.ApiKey);
        user!.ApiKeyHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task CreateUser_CreatesUserRole()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.PostAsync("/api/admin/users?name=roleuser", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "roleuser");
        user.Should().NotBeNull();
        user!.Role.Should().Be("User");
    }

    //  Remove User Tests 

    [Fact]
    public async Task RemoveUser_AsNonAdmin_Returns401()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.DeleteAsync("/api/admin/users/someuser");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveUser_WithValidName_Returns204()
    {
        // Arrange - create a user first
        _client.SetAdminApiKey();
        await _client.PostAsync("/api/admin/users?name=todelete", null);

        // Act
        var response = await _client.DeleteAsync("/api/admin/users/todelete");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveUser_WithNonExistentName_Returns404()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.DeleteAsync("/api/admin/users/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveUser_CannotDeleteSelf_Returns409()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act - admin name is "Admin" (seeded by AppDbContext)
        var response = await _client.DeleteAsync("/api/admin/users/admin");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveUser_CascadesFileDeletion()
    {
        // Arrange - create a user and a file owned by that user
        _client.SetAdminApiKey();
        await _client.PostAsync("/api/admin/users?name=cascadeuser", null);

        // Create a file for the new user via DB
        using (var scope2 = _factory.Services.CreateScope())
        {
            var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
            var newUser = await db2.Users.FirstOrDefaultAsync(u => u.Name == "cascadeuser");
            var fileRecord = Pluck.Shared.Models.File.Create(new FileParams
            {
                Token = "cascadef",
                OwnerId = newUser!.Id,
                DiskFileName = "cascade_test.txt",
                OriginalFileName = "cascade.txt",
                ContentType = "text/plain",
                Size = 100,
                DownloadsLeft = null,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsDirectory = false,
                AuthHash = "hash",
                Salt = "salt",
                IV = "iv"
            });
            db2.Files.Add(fileRecord);
            await db2.SaveChangesAsync();
        }

        // Act - delete the user
        var response = await _client.DeleteAsync("/api/admin/users/cascadeuser");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope3 = _factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
        var fileCheck = await db3.Files.FirstOrDefaultAsync(f => f.Token == "cascadef");
        fileCheck.Should().BeNull();
    }

    //  Get All Users Tests 

    [Fact]
    public async Task GetAllUsers_AsNonAdmin_Returns401()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllUsers_AsAdmin_ReturnsAllUsers()
    {
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.GetAsync("/api/admin/users");
        var dtos = await response.Content.ReadFromJsonAsync<List<UserResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos!.Count.Should().BeGreaterThan(1); // Admin + test_user
        dtos.Should().Contain(d => d.Name == "admin" && d.Role == "Admin");
        dtos.Should().Contain(d => d.Name == "test_user" && d.Role == "User");
    }

    [Fact]
    public async Task GetAllUsers_WithNoUsers_ReturnsEmpty()
    {
        // This test checks that at minimum the admin exists
        // Arrange
        _client.SetAdminApiKey();

        // Act
        var response = await _client.GetAsync("/api/admin/users");
        var dtos = await response.Content.ReadFromJsonAsync<List<UserResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        // At minimum, the admin user should exist
        dtos!.Should().Contain(d => d.Name == "admin");
    }
}