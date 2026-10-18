using Pluck.Api.Security;
using Pluck.Shared.Models;
using Pluck.Shared.Params;

namespace Pluck.Tests.Unit.Models;

public class UserTests
{
    [Fact]
    public void Create_ValidParams_ReturnsUser()
    {
        var result = User.Create(new UserParams
        {
            Name = "TestUser",
            ApiKeyHash = "hash123",
            Role = "User"
        });

        result.Should().NotBeNull();
        result.Name.Should().Be("testuser");
        result.ApiKeyHash.Should().Be("hash123");
        result.Role.Should().Be("User");
    }

    [Fact]
    public void Create_NormalizesNameToLowercase()
    {
        var result = User.Create(new UserParams
        {
            Name = "ADMIN",
            ApiKeyHash = "hash123",
            Role = "Admin"
        });

        result.Name.Should().Be("admin");
    }

    [Fact]
    public void Create_NullName_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = null!,
            ApiKeyHash = "hash123",
            Role = "User"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Name");
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = "   ",
            ApiKeyHash = "hash123",
            Role = "User"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Name");
    }

    [Fact]
    public void Create_NullApiKeyHash_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = null!,
            Role = "User"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("ApiKeyHash");
    }

    [Fact]
    public void Create_EmptyApiKeyHash_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = "   ",
            Role = "User"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("ApiKeyHash");
    }

    [Fact]
    public void Create_NullRole_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = "hash123",
            Role = null!
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Role");
    }

    [Fact]
    public void Create_EmptyRole_ThrowsArgumentException()
    {
        var act = () => User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = "hash123",
            Role = "   "
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Role");
    }

    [Fact]
    public void Create_InitializesEmptyFilesCollection()
    {
        var result = User.Create(new UserParams
        {
            Name = "testuser",
            ApiKeyHash = "hash123",
            Role = "User"
        });

        result.Files.Should().NotBeNull();
        result.Files.Should().BeEmpty();
    }

    [Fact]
    public void Update_ValidParams_UpdatesProperties()
    {
        var user = User.Create(new UserParams
        {
            Name = "oldname",
            ApiKeyHash = "oldhash",
            Role = "User"
        });

        user.Update(new UserParams
        {
            Name = "newname",
            ApiKeyHash = "newhash",
            Role = "Admin"
        });

        user.Name.Should().Be("newname");
        user.ApiKeyHash.Should().Be("newhash");
        user.Role.Should().Be("Admin");
    }
}
