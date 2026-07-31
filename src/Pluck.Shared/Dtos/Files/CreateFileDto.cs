namespace Pluck.Shared.Dtos.Files;

public record CreateFileDto(
    Guid OwnerId,
    string DiskFileName,
    string OriginalFileName,
    string ContentType,
    double Ttl,
    int? MaxDownloads
);