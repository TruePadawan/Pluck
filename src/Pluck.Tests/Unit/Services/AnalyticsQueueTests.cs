using Pluck.Api.Services.Analytics;
using Pluck.Tests.TestInfrastructure.Factories;

namespace Pluck.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="AnalyticsQueue"/> channel-based queue behavior.
/// </summary>
public class AnalyticsQueueTests
{
    [Fact]
    public async Task QueueAsync_DequeueAsync_ReturnsMessage()
    {
        var queue = new AnalyticsQueue();
        var message = DownloadAnalyticsMessageFactory.Create();

        await queue.QueueAsync(message);
        var result = await queue.DequeueAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(message);
    }

    [Fact]
    public async Task QueueAsync_NullMessage_ThrowsArgumentNullException()
    {
        var queue = new AnalyticsQueue();

        var act = async () => await queue.QueueAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueueAsync_DropOldest_RemovesOldestMessageWhenFull()
    {
        int capacity = 5;
        var queue = new AnalyticsQueue(capacity);
        var oldest = DownloadAnalyticsMessageFactory.Create();

        await queue.QueueAsync(oldest);

        var newMessages = new List<DownloadAnalyticsMessage>();
        for (int i = 0; i < capacity; i++)
        {
            var message = DownloadAnalyticsMessageFactory.Create();
            newMessages.Add(message);
            await queue.QueueAsync(message);
        }

        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        dequeued.Should().NotBe(oldest);
        newMessages.Should().Contain(dequeued);
    }

    [Fact]
    public async Task QueueAsync_MultipleMessages_PreservesOrder()
    {
        var queue = new AnalyticsQueue();
        var messages = new[]
        {
            DownloadAnalyticsMessageFactory.Create(),
            DownloadAnalyticsMessageFactory.Create(),
            DownloadAnalyticsMessageFactory.Create()
        };

        foreach (var message in messages)
            await queue.QueueAsync(message);

        var dequeued1 = await queue.DequeueAsync(CancellationToken.None);
        var dequeued2 = await queue.DequeueAsync(CancellationToken.None);
        var dequeued3 = await queue.DequeueAsync(CancellationToken.None);

        dequeued1.Should().Be(messages[0]);
        dequeued2.Should().Be(messages[1]);
        dequeued3.Should().Be(messages[2]);
    }

    [Fact]
    public async Task DequeueAsync_CancellationToken_Cancelled_ThrowsOperationCanceledException()
    {
        var queue = new AnalyticsQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await queue.DequeueAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task QueueAsync_ConcurrentWrites_AllMessagesQueued()
    {
        var queue = new AnalyticsQueue(100);
        int messageCount = 20;
        var tasks = new List<ValueTask>();

        for (int i = 0; i < messageCount; i++)
        {
            tasks.Add(queue.QueueAsync(DownloadAnalyticsMessageFactory.Create()));
        }

        foreach (var task in tasks)
            await task;

        var dequeuedCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (dequeuedCount < messageCount)
        {
            await queue.DequeueAsync(cts.Token);
            dequeuedCount++;
        }

        dequeuedCount.Should().Be(messageCount);
    }
}
