using Pluck.Api.Services.Analytics;
using Pluck.Shared.Models.Events;

namespace Pluck.Tests.TestInfrastructure.Factories;

/// <summary>
/// Factory helper for creating <see cref="DownloadAnalyticsMessage"/> test instances.
/// </summary>
public static class DownloadAnalyticsMessageFactory
{
    /// <summary>Creates a message with the given overrides; defaults to a random file ID.</summary>
    public static DownloadAnalyticsMessage Create(
        Guid? fileId = null,
        string? ipAddress = "192.168.1.1",
        string? userAgent = "PluckCli/1.0",
        ClientType clientType = ClientType.PluckCli)
    {
        return new DownloadAnalyticsMessage(
            fileId ?? Guid.NewGuid(),
            ipAddress,
            userAgent,
            clientType);
    }
}