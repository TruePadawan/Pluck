using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using Pluck.Shared.Models.Events;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;
using Pluck.Tests.TestInfrastructure.Factories;
using File = Pluck.Shared.Models.File;

namespace Pluck.Tests.Integration.Endpoints;

public class FileEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestApplicationFactory _factory;

    public FileEndpointsTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    //  Get Files Tests 

    [Fact]
    public async Task GetFiles_WithoutApiKey_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFiles_AsUser_ReturnsOwnFiles()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("getfile1");

        // Act
        var response = await _client.GetAsync("/api/files");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetFiles_AsAdmin_ReturnsAllFiles()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("adminfile");

        // Act
        _client.SetAdminApiKey();
        var response = await _client.GetAsync("/api/files");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Should().Contain(d => d.Token == "adminfile");
    }

    [Fact]
    public async Task GetFiles_WithNameFilter_ReturnsFiltered()
    {
        // Arrange
        _client.SetAdminApiKey();
        await CreateFileForCurrentUser("filterfile");

        // Act
        var response = await _client.GetAsync("/api/files?name=test_user");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Should().Contain(d => d.Token == "filterfile");
    }

    [Fact]
    public async Task GetFiles_ExcludesExpiredFiles()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("notexpired");

        // Create an expired file directly in DB (bypass validation)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Name == "test_user");
            var expiredFile = File.Create(new FileParams
            {
                Token = "expired1",
                OwnerId = user.Id,
                DiskFileName = Path.GetRandomFileName(),
                OriginalFileName = "test.txt",
                ContentType = "text/plain",
                Size = 100,
                DownloadsLeft = null,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsDirectory = false,
                AuthHash = PasswordHasher.Hash("pass"),
                Salt = "salt",
                IV = "iv"
            });
            db.Files.Add(expiredFile);
            await db.SaveChangesAsync();
            // Now set it to expired directly via EF raw update
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
                new Microsoft.Data.Sqlite.SqliteParameter("@token", "expired1"));
        }

        // Act
        var response = await _client.GetAsync("/api/files");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Should().NotContain(d => d.Token == "expired1");
        dtos.Should().Contain(d => d.Token == "notexpired");
    }

    [Fact]
    public async Task GetFiles_ExcludesExhaustedFiles()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("hasdownloads");

        // Create a file with 0 downloads left
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Name == "test_user");
            var exhaustedFile = FileFactory.Create(token: "exhausted", ownerId: user.Id, downloadsLeft: 0);
            db.Files.Add(exhaustedFile);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync("/api/files");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Should().NotContain(d => d.Token == "exhausted");
        dtos.Should().Contain(d => d.Token == "hasdownloads");
    }

    [Fact]
    public async Task GetFiles_EmptyList_WhenNoFiles()
    {
        // Arrange - create a fresh user with no files
        var newKey = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newUser = User.Create(new UserParams
            {
                Name = "emptyuser",
                ApiKeyHash = KeyHasher.ComputeHash(newKey),
                Role = "User"
            });
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }

        // Act
        _client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        _client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", newKey);
        var response = await _client.GetAsync("/api/files");
        var dtos = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dtos.Should().NotBeNull();
        dtos.Should().BeEmpty();
    }

    //  Get File Tests 

    [Fact]
    public async Task GetFile_WithValidToken_Returns200()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("mytoken1");

        // Act
        var response = await _client.GetAsync("/api/files/mytoken1");
        var dto = await response.Content.ReadFromJsonAsync<FileResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto.Token.Should().Be("mytoken1");
    }

    [Fact]
    public async Task GetFile_WithNonExistentToken_Returns404()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.GetAsync("/api/files/nonexist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFile_AsNonOwner_Returns401()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("notmine2");

        // Create a second non-admin user
        var newKey = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newUser = User.Create(new UserParams
            {
                Name = "secondusr",
                ApiKeyHash = KeyHasher.ComputeHash(newKey),
                Role = "User"
            });
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }

        // Act - second user tries to get test_user's file
        _client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        _client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", newKey);
        var response = await _client.GetAsync("/api/files/notmine2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFile_AsAdmin_ForOtherUser_Returns200()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("admintok");

        // Act - get as admin
        _client.SetAdminApiKey();
        var response = await _client.GetAsync("/api/files/admintok");
        var dto = await response.Content.ReadFromJsonAsync<FileResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto.Token.Should().Be("admintok");
    }

    //  Get File Analytics Tests 

    [Fact]
    public async Task GetFileAnalytics_WithValidToken_Returns200()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("analyt1");

        // Act
        var response = await _client.GetAsync("/api/files/analyt1/analytics");
        var dto = await response.Content.ReadFromJsonAsync<FileAnalyticsDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFileAnalytics_WithNoDownloads_ReturnsEmptyDicts()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("emptyan");

        // Act
        var response = await _client.GetAsync("/api/files/emptyan/analytics");
        var dto = await response.Content.ReadFromJsonAsync<FileAnalyticsDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto.TotalDownloads.Should().Be(0);
        dto.ClientTypes.Should().BeEmpty();
        dto.Countries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFileAnalytics_AsNonOwner_Returns401()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("anonym1");

        // Create a second user
        _client.SetAdminApiKey();
        var newKey = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newUser = User.Create(new UserParams
            {
                Name = "anothersu",
                ApiKeyHash = KeyHasher.ComputeHash(newKey),
                Role = "User"
            });
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }

        // Act - try as another user
        _client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        _client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", newKey);
        var response = await _client.GetAsync("/api/files/anonym1/analytics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFileAnalytics_AsAdmin_Returns200()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("adminan");

        // Act - get analytics as admin
        _client.SetAdminApiKey();
        var response = await _client.GetAsync("/api/files/adminan/analytics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFileAnalytics_ReturnsClientTypeBreakdown()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("clienttb");

        // Add download events directly
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var file = await db.Files.FirstAsync(f => f.Token == "clienttb");
            db.FileDownloadEvents.Add(FileDownloadEvents.Create(file.Id, "hash1", null, "US", ClientType.PluckCli));
            db.FileDownloadEvents.Add(FileDownloadEvents.Create(file.Id, "hash2", null, "UK", ClientType.Browser));
            file.IncrementDownloadCount();
            file.IncrementDownloadCount();
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync("/api/files/clienttb/analytics");
        var dto = await response.Content.ReadFromJsonAsync<FileAnalyticsDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto.ClientTypes.Should().ContainKey("PluckCli");
        dto.ClientTypes.Should().ContainKey("Browser");
    }

    [Fact]
    public async Task GetFileAnalytics_ReturnsCountryBreakdown()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("countryb");

        // Add download events directly
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var file = await db.Files.FirstAsync(f => f.Token == "countryb");
            db.FileDownloadEvents.Add(FileDownloadEvents.Create(file.Id, "hash1", null, "US", ClientType.Script));
            db.FileDownloadEvents.Add(FileDownloadEvents.Create(file.Id, "hash2", null, "US", ClientType.Script));
            db.FileDownloadEvents.Add(FileDownloadEvents.Create(file.Id, "hash3", null, "DE", ClientType.Script));
            file.IncrementDownloadCount();
            file.IncrementDownloadCount();
            file.IncrementDownloadCount();
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync("/api/files/countryb/analytics");
        var dto = await response.Content.ReadFromJsonAsync<FileAnalyticsDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto.Countries.Should().ContainKey("US");
        dto.Countries["US"].Should().Be(2);
        dto.Countries.Should().ContainKey("DE");
        dto.Countries["DE"].Should().Be(1);
    }

    //  Delete File Tests 

    [Fact]
    public async Task DeleteFile_WithValidToken_Returns204()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("delete1");

        // Act
        var response = await _client.DeleteAsync("/api/files/delete1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteFile_AsAdmin_Returns204()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("admindel");

        // Act - delete as admin
        _client.SetAdminApiKey();
        var response = await _client.DeleteAsync("/api/files/admindel");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteFile_AsNonOwner_Returns204()
    {
        // Arrange - create a file for test_user
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("nonowner");

        // Create a second user
        _client.SetAdminApiKey();
        var newKey = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newUser = User.Create(new UserParams
            {
                Name = "deluser2",
                ApiKeyHash = KeyHasher.ComputeHash(newKey),
                Role = "User"
            });
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }

        // Act - non-owner tries to delete (returns 204 but does nothing)
        _client.DefaultRequestHeaders.Remove("X-PLUCK-API-KEY");
        _client.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", newKey);
        var response = await _client.DeleteAsync("/api/files/nonowner");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // File should still exist
        _client.SetUserApiKey();
        var getResponse = await _client.GetAsync("/api/files/nonowner");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteFile_WithNonExistentToken_Returns404()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.DeleteAsync("/api/files/nonexist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFile_RemovesDatabaseEntry()
    {
        // Arrange
        _client.SetUserApiKey();
        await CreateFileForCurrentUser("removedb");

        // Act
        var response = await _client.DeleteAsync("/api/files/removedb");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var file = await db.Files.FirstOrDefaultAsync(f => f.Token == "removedb");
        file.Should().BeNull();
    }

    //  Helper Methods 

    private async Task CreateFileForCurrentUser(string token)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Name == "test_user");

        const string password = "testpass";
        var file = File.Create(new FileParams
        {
            Token = token,
            OwnerId = user.Id,
            DiskFileName = Path.GetRandomFileName(),
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = null,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsDirectory = false,
            AuthHash = PasswordHasher.Hash(password),
            Salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            IV = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
        });
        db.Files.Add(file);
        await db.SaveChangesAsync();
    }
}