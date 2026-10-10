using System.Threading.Channels;

namespace Pluck.Api.Services.Analytics;

public sealed class AnalyticsQueue : IAnalyticsQueue
{
    private readonly Channel<DownloadAnalyticsMessage> _queue;

    public AnalyticsQueue(int capacity = 1000)
    {
        BoundedChannelOptions options = new(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _queue = Channel.CreateBounded<DownloadAnalyticsMessage>(options);
    }

    public async ValueTask QueueAsync(DownloadAnalyticsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _queue.Writer.WriteAsync(message);
    }

    public async ValueTask<DownloadAnalyticsMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
