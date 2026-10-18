using Microsoft.AspNetCore.Http;
using Moq;
using Pluck.Api.Utils;
using Pluck.Shared.Params;
using File = Pluck.Shared.Models.File;

namespace Pluck.Tests.Unit.Utils;

/// <summary>
/// Unit tests for <see cref="Utilities"/> static utility methods.
/// </summary>
public class UtilitiesTests
{
    [Fact]
    public void GenerateFileResponse_ReturnsCorrectDto()
    {
        var file = File.Create(new FileParams
        {
            Token = "testtoken",
            OwnerId = Guid.NewGuid(),
            DiskFileName = "disk.txt",
            OriginalFileName = "original.txt",
            ContentType = "text/plain",
            Size = 2048,
            DownloadsLeft = 5,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            IsDirectory = false,
            AuthHash = "hash",
            Salt = "salt",
            IV = "iv"
        });

        var request = Mock.Of<HttpRequest>(r => r.Scheme == "https" && r.Host == new HostString("example.com", 8080));

        var result = Utilities.GenerateFileResponse(file, request!);

        result.Should().NotBeNull();
        result.Token.Should().Be("testtoken");
        result.OriginalFileName.Should().Be("original.txt");
        result.DownloadsLeft.Should().Be(5);
        result.Size.Should().Be(2048);
        result.IsDirectory.Should().BeFalse();
        result.DownloadCount.Should().Be(0);
    }

    [Fact]
    public void GenerateFileResponse_GeneratesCorrectDownloadUrl()
    {
        var file = File.Create(new FileParams
        {
            Token = "mytoken",
            OwnerId = Guid.NewGuid(),
            DiskFileName = "disk.txt",
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            Size = 1024,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var request = Mock.Of<HttpRequest>(r => r.Scheme == "http" && r.Host == new HostString("localhost"));

        var result = Utilities.GenerateFileResponse(file, request!);

        result.DownloadUrl.Should().Be("http://localhost/f/mytoken");
    }

    [Fact]
    public void GenerateFileResponse_WithPort_IncludesPortInUrl()
    {
        var file = File.Create(new FileParams
        {
            Token = "porttoken",
            OwnerId = Guid.NewGuid(),
            DiskFileName = "disk.txt",
            OriginalFileName = "file.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var request = Mock.Of<HttpRequest>(r => r.Scheme == "https" && r.Host == new HostString("example.com", 3000));

        var result = Utilities.GenerateFileResponse(file, request!);

        result.DownloadUrl.Should().Be("https://example.com:3000/f/porttoken");
    }

    [Fact]
    public void GenerateFileResponse_NullDownloadsLeft_PassesNull()
    {
        var file = File.Create(new FileParams
        {
            Token = "unlimited",
            OwnerId = Guid.NewGuid(),
            DiskFileName = "disk.txt",
            OriginalFileName = "file.txt",
            ContentType = "text/plain",
            Size = 500,
            DownloadsLeft = null,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var request = Mock.Of<HttpRequest>(r => r.Scheme == "http" && r.Host == new HostString("localhost"));

        var result = Utilities.GenerateFileResponse(file, request!);

        result.DownloadsLeft.Should().BeNull();
    }

    [Fact]
    public void GenerateFileResponse_DirectoryFile_SetsIsDirectory()
    {
        var file = File.Create(new FileParams
        {
            Token = "dirtoken",
            OwnerId = Guid.NewGuid(),
            DiskFileName = "disk_dir",
            OriginalFileName = "myfolder",
            ContentType = "application/x-directory",
            Size = 0,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            IsDirectory = true,
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var request = Mock.Of<HttpRequest>(r => r.Scheme == "http" && r.Host == new HostString("localhost"));

        var result = Utilities.GenerateFileResponse(file, request!);

        result.IsDirectory.Should().BeTrue();
    }
}
