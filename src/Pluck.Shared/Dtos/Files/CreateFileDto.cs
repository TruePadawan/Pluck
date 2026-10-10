namespace Pluck.Shared.Dtos.Files;

public record CreateFileDto(
    Guid OwnerId,
    string? Token,
    string DiskFileName,
    string OriginalFileName,
    string ContentType,
    long Size,
    double Ttl,
    int? MaxDownloads,
    bool IsDirectory,
    string? PasswordHash
);