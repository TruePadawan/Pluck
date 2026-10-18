using Pluck.Shared.Lib;
using Pluck.Shared.Models;
using Pluck.Shared.Params;
using File = Pluck.Shared.Models.File;

namespace Pluck.Tests.Unit.Models;

public class FileTests
{
    private readonly Guid _ownerId;

    public FileTests()
    {
        _ownerId = Guid.NewGuid();
    }

    [Fact]
    public void Create_ValidParams_ReturnsFile()
    {
        var token = SharedUtilities.GenerateId(6);
        var result = File.Create(new FileParams
        {
            Token = token,
            OwnerId = _ownerId,
            DiskFileName = Path.GetRandomFileName(),
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 1024,
            DownloadsLeft = null,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            IsDirectory = false,
            AuthHash = "hash",
            Salt = "salt",
            IV = "iv"
        });

        result.Should().NotBeNull();
        result.Token.Should().Be(token);
        result.OwnerId.Should().Be(_ownerId);
        result.DownloadCount.Should().Be(0);
    }

    [Fact]
    public void Create_NullToken_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = null!,
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Token");
    }

    [Fact]
    public void Create_EmptyOwnerId_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = Guid.Empty,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("OwnerId");
    }

    [Fact]
    public void Create_NullDiskFileName_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = null!,
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("DiskFileName");
    }

    [Fact]
    public void Create_NullOriginalFileName_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = null!,
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("OriginalFileName");
    }

    [Fact]
    public void Create_NullContentType_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = null!,
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("ContentType");
    }

    [Fact]
    public void Create_NegativeSize_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = -1,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Size");
    }

    [Fact]
    public void Create_NegativeDownloadsLeft_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = -1,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("DownloadsLeft");
    }

    [Fact]
    public void Create_PastExpiresAt_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullAuthHash_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = null!,
            Salt = "s",
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("AuthHash");
    }

    [Fact]
    public void Create_NullSalt_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = null!,
            IV = "i"
        });

        act.Should().Throw<ArgumentException>().WithParameterName("Salt");
    }

    [Fact]
    public void Create_NullIV_ThrowsArgumentException()
    {
        var act = () => File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "orig.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = null!
        });

        act.Should().Throw<ArgumentException>().WithParameterName("IV");
    }

    [Fact]
    public void IsDownloadable_ValidFileWithDiskFile_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        var diskFileName = Path.GetRandomFileName();
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = diskFileName,
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        System.IO.File.WriteAllText(Path.Combine(tempDir, diskFileName), "content");

        var result = file.IsDownloadable(tempDir);

        result.Should().BeTrue();
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void IsDownloadable_NullDownloadsLeftWithDiskFile_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        var diskFileName = Path.GetRandomFileName();
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = diskFileName,
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = null,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        System.IO.File.WriteAllText(Path.Combine(tempDir, diskFileName), "content");

        var result = file.IsDownloadable(tempDir);

        result.Should().BeTrue();
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void IsDownloadable_ZeroDownloadsLeft_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        var diskFileName = Path.GetRandomFileName();
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = diskFileName,
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = 0,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        System.IO.File.WriteAllText(Path.Combine(tempDir, diskFileName), "content");

        var result = file.IsDownloadable(tempDir);

        result.Should().BeFalse();
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void IsDownloadable_FileNotOnDisk_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "missing.txt",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var result = file.IsDownloadable(tempDir);

        result.Should().BeFalse();
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void DecrementDownloadsLeft_DecrementsCounter()
    {
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = 5,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        file.DecrementDownloadsLeft();

        file.DownloadsLeft.Should().Be(4);
    }

    [Fact]
    public void DecrementDownloadsLeft_NullDownloadsLeft_DoesNothing()
    {
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            DownloadsLeft = null,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        file.DecrementDownloadsLeft();

        file.DownloadsLeft.Should().BeNull();
    }

    [Fact]
    public void IncrementDownloadCount_IncrementsCounter()
    {
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        file.IncrementDownloadCount();
        file.IncrementDownloadCount();

        file.DownloadCount.Should().Be(2);
    }

    [Fact]
    public void Update_ValidParams_UpdatesProperties()
    {
        var file = File.Create(new FileParams
        {
            Token = "abc123",
            OwnerId = _ownerId,
            DiskFileName = "disk.txt",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            Size = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        var newOwner = Guid.NewGuid();
        file.Update(new FileParams
        {
            Token = "abc123",
            OwnerId = newOwner,
            DiskFileName = "disk.txt",
            OriginalFileName = "updated.txt",
            ContentType = "text/plain",
            Size = 200,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            AuthHash = "h",
            Salt = "s",
            IV = "i"
        });

        file.OwnerId.Should().Be(newOwner);
        file.OriginalFileName.Should().Be("updated.txt");
        file.Size.Should().Be(200);
    }
}
