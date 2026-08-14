namespace Pluck.Shared.Dtos.Files;

public record FileResponseDto(
    string Token,
    string OriginalFileName,
    int? DownloadsLeft,
    DateTime ExpiresAt,
    string DownloadUrl,
    bool IsDirectory
);