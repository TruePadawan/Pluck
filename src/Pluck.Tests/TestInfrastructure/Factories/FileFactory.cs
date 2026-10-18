using Pluck.Shared.Lib;
using Pluck.Shared.Params;
using File = Pluck.Shared.Models.File;

namespace Pluck.Tests.TestInfrastructure.Factories;

/// <summary>
/// Factory helpers for creating <see cref="File"/> test entities with sensible defaults.
/// </summary>
public static class FileFactory
{
    /// <summary>Creates a file with the given overrides; defaults to a far-future expiry date.</summary>
    public static File Create(
        string? token = null,
        Guid? ownerId = null,
        string? diskFileName = null,
        string? originalFileName = "test.txt",
        string? contentType = "text/plain",
        long size = 1024,
        int? downloadsLeft = null,
        DateTime? expiresAt = null,
        bool isDirectory = false,
        string? authHash = null,
        string? salt = null,
        string? iv = null)
    {
        return File.Create(new FileParams
        {
            Token = token ?? SharedUtilities.GenerateId(6),
            OwnerId = ownerId ?? Guid.NewGuid(),
            DiskFileName = diskFileName ?? Path.GetRandomFileName(),
            OriginalFileName = originalFileName ?? "test.txt",
            ContentType = contentType ?? "text/plain",
            Size = size,
            DownloadsLeft = downloadsLeft,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddYears(10),
            IsDirectory = isDirectory,
            AuthHash = authHash ?? "default_hash",
            Salt = salt ?? "default_salt",
            IV = iv ?? "default_iv"
        });
    }

    /// <summary>Creates a file whose <c>ExpiresAt</c> is in the past.</summary>
    public static File CreateExpiredFile(Guid? ownerId = null)
    {
        return Create(
            ownerId: ownerId,
            expiresAt: DateTime.UtcNow.AddSeconds(-1));
    }

    /// <summary>Creates a file with <c>DownloadsLeft</c> set to 0.</summary>
    public static File CreateFileWithZeroDownloads(Guid? ownerId = null)
    {
        return Create(
            ownerId: ownerId,
            downloadsLeft: 0);
    }
}
