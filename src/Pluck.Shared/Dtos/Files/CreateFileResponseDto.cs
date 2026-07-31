namespace Pluck.Shared.Dtos.Files;

public record CreateFileResponseDto(
    string Token,
    string OriginalFileName,
    int? DownloadsLeft,
    DateTime ExpiresAt
);