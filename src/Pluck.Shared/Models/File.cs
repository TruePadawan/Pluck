namespace Pluck.Shared.Models;

public sealed class File : EntityBase
{
    public string Token { get; private set; }
    public Guid OwnerId { get; private set; }
    public User? Owner { get; private set; }
    public string DiskFileName { get; private set; }
    public string OriginalFileName { get; private set; }
    public int? DownloadsLeft { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    // For ORM frameworks
    private File()
    {
        Token = string.Empty;
        OwnerId = Guid.Empty;
        DiskFileName = string.Empty;
        OriginalFileName = string.Empty;
        DownloadsLeft = null;
        ExpiresAt = DateTimeOffset.Now;
    }

    private File(string token, Guid ownerId, string diskFileName, string originalFileName, int? downloadsLeft,
        DateTimeOffset expiresAt)
    {
        Token = token;
        OwnerId = ownerId;
        DiskFileName = diskFileName;
        OriginalFileName = originalFileName;
        DownloadsLeft = downloadsLeft;
        ExpiresAt = expiresAt;
    }

    public static File Create(string token, Guid ownerId, string diskFileName, string originalFileName,
        int? downloadsLeft, DateTimeOffset expiresAt)
    {
        ValidateInputs(token, ownerId, diskFileName, originalFileName, downloadsLeft, expiresAt);
        return new File(token, ownerId, diskFileName, originalFileName, downloadsLeft, expiresAt);
    }

    public void Update(string token, Guid ownerId, string diskFileName, string originalFileName, int? downloadsLeft,
        DateTimeOffset expiresAt)
    {
        ValidateInputs(token, ownerId, diskFileName, originalFileName, downloadsLeft, expiresAt);
        Token = token;
        OwnerId = ownerId;
        DiskFileName = diskFileName;
        OriginalFileName = originalFileName;
        DownloadsLeft = downloadsLeft;
        ExpiresAt = expiresAt;

        UpdateLastModified();
    }

    private static void ValidateInputs(string token, Guid ownerId, string diskFileName, string originalFileName,
        int? downloadsLeft, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty", nameof(token));
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(diskFileName))
            throw new ArgumentException("Disk file name cannot be null or empty", nameof(diskFileName));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original name cannot be null or empty", nameof(originalFileName));
        if (downloadsLeft < 0)
            throw new ArgumentException("The number of downloads left cannot be negative", nameof(downloadsLeft));
        if (expiresAt < DateTimeOffset.Now)
            throw new ArgumentException("File expiration date cannot be in the past");
    }
};