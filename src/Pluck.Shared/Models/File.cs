namespace Pluck.Shared.Models;

public sealed class File : EntityBase
{
    public string Token { get; private set; }
    public Guid OwnerId { get; private set; }
    public User Owner { get; private set; } = null!;
    public string DiskFileName { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public int? DownloadsLeft { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsDirectory { get; private set; }
    public string? PasswordHash { get; private set; }

    public bool IsPasswordProtected => PasswordHash is not null;

    // For ORM frameworks
    private File()
    {
        Token = string.Empty;
        OwnerId = Guid.Empty;
        DiskFileName = string.Empty;
        OriginalFileName = string.Empty;
        ContentType = string.Empty;
        DownloadsLeft = null;
        ExpiresAt = DateTime.UtcNow;
        IsDirectory = false;
        PasswordHash = null;
    }

    private File(FileParams p)
    {
        Token = p.Token;
        OwnerId = p.OwnerId;
        DiskFileName = p.DiskFileName;
        OriginalFileName = p.OriginalFileName;
        ContentType = p.ContentType;
        DownloadsLeft = p.DownloadsLeft;
        ExpiresAt = p.ExpiresAt;
        IsDirectory = p.IsDirectory;
        PasswordHash = p.PasswordHash;
    }

    public static File Create(FileParams p)
    {
        ValidateInputs(p);
        return new File(p);
    }

    public void Update(FileParams p)
    {
        ValidateInputs(p);
        Token = p.Token;
        OwnerId = p.OwnerId;
        DiskFileName = p.DiskFileName;
        OriginalFileName = p.OriginalFileName;
        ContentType = p.ContentType;
        DownloadsLeft = p.DownloadsLeft;
        ExpiresAt = p.ExpiresAt;
        IsDirectory = p.IsDirectory;
        PasswordHash = p.PasswordHash;

        UpdateLastModified();
    }

    public void DecrementDownloadsLeft()
    {
        if (DownloadsLeft > 0)
        {
            DownloadsLeft--;
            UpdateLastModified();
        }
    }

    public bool IsDownloadable(string uploadDirectory)
    {
        if (DownloadsLeft <= 0 || ExpiresAt < DateTime.UtcNow) return false;
        // Check that the actual file exists on disk
        var filePath = Path.Combine(uploadDirectory, DiskFileName);
        return System.IO.File.Exists(filePath);
    }

    private static void ValidateInputs(FileParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Token))
            throw new ArgumentException("Token cannot be null or empty", nameof(p.Token));
        if (p.OwnerId == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty", nameof(p.OwnerId));
        if (string.IsNullOrWhiteSpace(p.DiskFileName))
            throw new ArgumentException("Disk file name cannot be null or empty", nameof(p.DiskFileName));
        if (string.IsNullOrWhiteSpace(p.OriginalFileName))
            throw new ArgumentException("Original name cannot be null or empty", nameof(p.OriginalFileName));
        if (string.IsNullOrWhiteSpace(p.ContentType))
            throw new ArgumentException("Content type cannot be null or empty", nameof(p.ContentType));
        if (p.DownloadsLeft < 0)
            throw new ArgumentException("The number of downloads left cannot be negative", nameof(p.DownloadsLeft));
        if (p.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("File expiration date cannot be in the past");
    }
};