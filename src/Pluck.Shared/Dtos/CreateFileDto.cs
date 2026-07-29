namespace Pluck.Shared.Dtos;

public record CreateFileDto(
    Guid OwnerId,
    string DiskFileName,
    string OriginalFileName,
    double Ttl,
    int? MaxDownloads
);