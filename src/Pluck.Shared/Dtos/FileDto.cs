namespace Pluck.Shared.Dtos;

public record FileDto(
    int OwnerId,
    string DiskFileName,
    string OriginalFileName,
    double Ttl,
    int? MaxDownloads
);