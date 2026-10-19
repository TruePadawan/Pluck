using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Middlewares;
using Pluck.Api.Persistence;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Shared.Models;
using Pluck.Shared.Params;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Middleware;

/// <summary>
/// Tests for <see cref="ApiKeyAuthMiddleware"/>.
/// Uses in-memory SQLite with a real UserRepository to verify end-to-end auth flow.
/// </summary>
public class ApiKeyAuthMiddlewareTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserRepository _repo;
    private readonly User _user;

    public ApiKeyAuthMiddlewareTests()
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

        _repo = new UserRepository(_db);
    }

    private static ApiKeyAuthMiddleware CreateMiddleware()
    {
        return new ApiKeyAuthMiddleware(Next);
        Task Next(HttpContext _) => Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_WithoutHeader_Returns401()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Invoke_WithInvalidKey_Returns401()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-PLUCK-API-KEY"] = "invalid-key";
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Invoke_WithValidKey_SetsUserOnContext()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-PLUCK-API-KEY"] = SharedData.UserApiKey;
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        context.Items["User"].Should().NotBeNull();
        context.Items["User"].Should().Be(_user);
    }

    [Fact]
    public async Task Invoke_WithValidKey_CallsNextDelegate()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-PLUCK-API-KEY"] = SharedData.UserApiKey;

        var nextCalled = false;

        var middleware = new ApiKeyAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_ErrorMessage_MissingKey()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        var body = await GetBodyAsync(context);
        body.Should().Contain("API Key missing");
    }

    [Fact]
    public async Task Invoke_ErrorMessage_InvalidKey()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-PLUCK-API-KEY"] = "invalid-key";
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _repo);

        // Assert
        var body = await GetBodyAsync(context);
        body.Should().Contain("Invalid API Key");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        return context;
    }

    private static async Task<string> GetBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}