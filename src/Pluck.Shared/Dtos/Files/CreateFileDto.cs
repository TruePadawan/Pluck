namespace Pluck.Shared.Dtos.Files;

/// <summary>Describes an uploaded file, its sharing limits, and its optional notification destination.</summary>
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
    string AuthHash,
    string Salt,
    string IV,
    string? WebhookUrl = null
);