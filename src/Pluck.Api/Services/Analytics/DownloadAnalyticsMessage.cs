using Pluck.Shared.Models.Events;

namespace Pluck.Api.Services.Analytics;

public record DownloadAnalyticsMessage(
    Guid FileId,
    string? IpAddress,
    string? UserAgent,
    ClientType ClientType
);
