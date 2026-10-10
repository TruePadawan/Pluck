namespace Pluck.Shared.Params;

/// <summary>
/// Parameter object for creating or updating a <see cref="Models.File"/> entity.
/// </summary>
public class FileParams
{
    public required string Token { get; init; }
    public required Guid OwnerId { get; init; }
    public required string DiskFileName { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required long Size { get; init; }
    public int? DownloadsLeft { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public bool IsDirectory { get; init; }
    public string? PasswordHash { get; init; }
}
