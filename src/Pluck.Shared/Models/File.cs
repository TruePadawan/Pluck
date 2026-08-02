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
    }

    private File(string token, Guid ownerId, string diskFileName, string originalFileName, string contentType,
        int? downloadsLeft,
        DateTime expiresAt)
    {
        Token = token;
        OwnerId = ownerId;
        DiskFileName = diskFileName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        DownloadsLeft = downloadsLeft;
        ExpiresAt = expiresAt;
    }

    public static File Create(string token, Guid ownerId, string diskFileName, string originalFileName,
        string contentType,
        int? downloadsLeft, DateTime expiresAt)
    {
        ValidateInputs(token, ownerId, diskFileName, originalFileName, contentType, downloadsLeft, expiresAt);
        return new File(token, ownerId, diskFileName, originalFileName, contentType, downloadsLeft, expiresAt);
    }

    public void Update(string token, Guid ownerId, string diskFileName, string originalFileName, string contentType,
        int? downloadsLeft,
        DateTime expiresAt)
    {
        ValidateInputs(token, ownerId, diskFileName, originalFileName, contentType, downloadsLeft, expiresAt);
        Token = token;
        OwnerId = ownerId;
        DiskFileName = diskFileName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        DownloadsLeft = downloadsLeft;
        ExpiresAt = expiresAt;

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

    private static void ValidateInputs(string token, Guid ownerId, string diskFileName, string originalFileName,
        string contentType,
        int? downloadsLeft, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty", nameof(token));
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(diskFileName))
            throw new ArgumentException("Disk file name cannot be null or empty", nameof(diskFileName));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original name cannot be null or empty", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be null or empty", nameof(contentType));
        if (downloadsLeft < 0)
            throw new ArgumentException("The number of downloads left cannot be negative", nameof(downloadsLeft));
        if (expiresAt < DateTime.UtcNow)
            throw new ArgumentException("File expiration date cannot be in the past");
    }
};