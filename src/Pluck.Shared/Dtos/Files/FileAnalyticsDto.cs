namespace Pluck.Shared.Dtos.Files;

public record FileAnalyticsDto(
    int TotalDownloads,
    Dictionary<string, int> ClientTypes,
    Dictionary<string, int> Countries
);
