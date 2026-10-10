namespace Pluck.Api.Services.Analytics;

public interface IAnalyticsQueue
{
    ValueTask QueueAsync(DownloadAnalyticsMessage message);
    ValueTask<DownloadAnalyticsMessage> DequeueAsync(CancellationToken cancellationToken);
}
