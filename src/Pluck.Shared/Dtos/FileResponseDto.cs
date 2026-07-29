namespace Pluck.Shared.Dtos;

public record FileResponseDto(
    string Token,
    string OriginalFileName,
    int? DownloadsLeft,
    DateTimeOffset ExpiresAt
);