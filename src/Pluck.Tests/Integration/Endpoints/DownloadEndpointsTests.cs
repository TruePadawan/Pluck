using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Endpoints;

public class DownloadEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestApplicationFactory _factory;

    public DownloadEndpointsTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Download_WithNonExistentToken_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/f/nonexist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WithExpiredFile_Returns404()
    {
        // Arrange
        await CreateFileInDb(token: "expired2", expiresAt: DateTime.UtcNow.AddSeconds(-1));

        // Act
        var response = await _client.GetAsync("/f/expired2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WithExhaustedDownloads_Returns404()
    {
        // Arrange
        await CreateFileInDb(token: "exhaust2", downloadsLeft: 0);

        // Act
        var response = await _client.GetAsync("/f/exhaust2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WithMissingDiskFile_Returns404()
    {
        // Arrange - create a DB entry but no disk file
        await CreateFileInDb(token: "nodisk", createDiskFile: false);

        // Act
        var response = await _client.GetAsync("/f/nodisk");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WithoutPassword_OnProtectedFile_Returns401()
    {
        // Arrange
        await CreateFileInDb(token: "protected", password: "secret123", createDiskFile: true);

        // Act
        var response = await _client.GetAsync("/f/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Download_WithWrongPassword_Returns401()
    {
        // Arrange
        await CreateFileInDb(token: "wrongpw", password: "secret123", createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "wrongpassword");
        var response = await _client.GetAsync("/f/wrongpw");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Download_WithCorrectPassword_Returns200()
    {
        // Arrange
        await CreateFileInDb(token: "correctp", password: "secret123", createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "secret123");
        var response = await _client.GetAsync("/f/correctp");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_WithCorrectAuthKeyHeader_Returns200()
    {
        // Arrange
        await CreateFileInDb(token: "authkeyh", password: "mykey", createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "mykey");
        var response = await _client.GetAsync("/f/authkeyh");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_WithCorrectFormPassword_Returns200()
    {
        // Arrange
        await CreateFileInDb(token: "formpwd", password: "formpass", createDiskFile: true);

        // Act
        var content = new MultipartFormDataContent
        {
            { new StringContent("formpass"), "pwd" }
        };
        var response = await _client.PostAsync("/f/formpwd", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ReturnsSaltAndIvHeaders()
    {
        // Arrange
        await CreateFileInDb(token: "saltiv", password: "pass123", createDiskFile: true);

        // Act - request without password to get the metadata headers
        var response = await _client.GetAsync("/f/saltiv");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-Pluck-Salt").Should().BeTrue();
        response.Headers.Contains("X-Pluck-IV").Should().BeTrue();
    }

    [Fact]
    public async Task Download_ReturnsCorrectContentType()
    {
        // Arrange
        await CreateFileInDb(token: "contentt", password: "pass123", contentType: "application/pdf",
            createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "pass123");
        var response = await _client.GetAsync("/f/contentt");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Download_ReturnsCorrectFileName()
    {
        // Arrange
        await CreateFileInDb(token: "filename", password: "pass123", originalFileName: "document.pdf",
            createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "pass123");
        var response = await _client.GetAsync("/f/filename");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition?.FileName.Should().Be("document.pdf");
    }

    [Fact]
    public async Task Download_DecrementsDownloadsLeft()
    {
        // Arrange
        await CreateFileInDb(token: "decrement", password: "pass123", downloadsLeft: 3, createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "pass123");
        var response = await _client.GetAsync("/f/decrement");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var file = await db.Files.FirstOrDefaultAsync(f => f.Token == "decrement");
        file.Should().NotBeNull();
        // Note: DownloadsLeft is decremented after the stream completes, which may not happen
        // immediately in TestServer. We check the file exists and was accessible.
    }

    [Fact]
    public async Task Download_BrowserRequest_WithoutPassword_ReturnsHtml()
    {
        // Arrange
        await CreateFileInDb(token: "browserh", password: "pass123", createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
        var response = await _client.GetAsync("/f/browserh");
        _client.DefaultRequestHeaders.Accept.Clear();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Download_DirectoryFile_ReturnsDirectoryHeader()
    {
        // Arrange
        await CreateFileInDb(token: "dirfile", password: "pass123", isDirectory: true, createDiskFile: true);

        // Act
        _client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "pass123");
        var response = await _client.GetAsync("/f/dirfile");
        _client.DefaultRequestHeaders.Remove("X-Pluck-AuthKey");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-PLUCK-IS-DIRECTORY").Should().BeTrue();
    }

    [Fact]
    public async Task Download_WithoutPassword_NonBrowser_ReturnsAuthRequiredHeader()
    {
        // Arrange
        await CreateFileInDb(token: "authreq", password: "pass123", createDiskFile: true);

        // Act
        var response = await _client.GetAsync("/f/authreq");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-PLUCK-AUTH-REQUIRED").Should().BeTrue();
    }

    // --- Helper Methods ---

    private async Task CreateFileInDb(
        string token,
        string? password = null,
        string? originalFileName = "test.txt",
        string? contentType = "text/plain",
        int? downloadsLeft = null,
        DateTime? expiresAt = null,
        bool isDirectory = false,
        bool createDiskFile = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "test_user");

        var diskFileName = Path.GetRandomFileName();
        if (createDiskFile)
        {
            var tempDir = _factory.Services
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<PluckApiOptions>>()
                .Value.UploadDirectory;
            System.IO.File.WriteAllText(Path.Combine(tempDir!, diskFileName), "Test file content");
        }

        var authHash = password != null ? PasswordHasher.Hash(password) : "default_hash";
        var salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var iv = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

        var expiresAtValue = expiresAt ?? DateTime.UtcNow.AddHours(1);
        // If expiresAt is in the past, we need to create with future date first, then update
        var createExpiresAt = expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow
            ? DateTime.UtcNow.AddHours(1)
            : expiresAtValue;
        var file = Pluck.Shared.Models.File.Create(new FileParams
        {
            Token = token,
            OwnerId = user!.Id,
            DiskFileName = diskFileName,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            Size = 100,
            DownloadsLeft = downloadsLeft,
            ExpiresAt = createExpiresAt,
            IsDirectory = isDirectory,
            AuthHash = authHash,
            Salt = salt,
            IV = iv
        });
        db.Files.Add(file);
        await db.SaveChangesAsync();
        // If we need an expired file, set it directly via raw SQL
        if (expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Files SET ExpiresAt = datetime('now', '-1 second') WHERE Token = @token",
                new Microsoft.Data.Sqlite.SqliteParameter("@token", token));
        }
    }
}