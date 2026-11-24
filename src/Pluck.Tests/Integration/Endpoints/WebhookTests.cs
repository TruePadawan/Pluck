using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Pluck.Api.Persistence;
using WebhookDelivery = Pluck.Shared.Models.WebhookDelivery;
using Pluck.Api.Security;
using Pluck.Api.Workers;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Endpoints;

/// <summary>Exercises webhook registration, event creation, and delivery recovery.</summary>
public class WebhookTests
{
    /// <summary>Hosts the API with a test receiver allowlist and manually driven workers.</summary>
    private sealed class Factory : TestApplicationFactory
    {
        /// <summary>Disables background workers and permits the test receiver.</summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var worker in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                             s.ImplementationType?.Namespace == typeof(WebhookWorker).Namespace).ToArray())
                    services.Remove(worker);
                services.Configure<PluckApiOptions>(o => o.WebhookHosts = ["receiver.example"]);
            });
        }
    }

    /// <summary>Records event IDs across a simulated outage and recovery.</summary>
    private sealed class Receiver : HttpMessageHandler
    {
        public List<string> EventIds { get; } = [];
        /// <summary>Checks the notification and returns success after the first transient failure.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            request.RequestUri!.AbsoluteUri.Should().Be("https://receiver.example/topic");
            request.Headers.GetValues("X-Pluck-Event").Single().Should().Be("file.downloaded");
            (await request.Content!.ReadAsStringAsync(token)).Should().Contain("recorded a download");
            EventIds.Add(request.Headers.GetValues("X-Pluck-Event-Id").Single());
            return new HttpResponseMessage(EventIds.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        }
    }

    /// <summary>Verifies upload validation, resumed download notification, retry identity, and expiry cleanup.</summary>
    [Fact]
    public async Task Upload_RangeDownload_RetryDelivery_ThenExpire()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        client.SetUserApiKey();
        client.DefaultRequestHeaders.Add("X-Pluck-Salt", "salt");
        client.DefaultRequestHeaders.Add("X-Pluck-IV", "iv");
        client.DefaultRequestHeaders.Add("X-Pluck-AuthKey", "secret");
        client.DefaultRequestHeaders.Add("X-Pluck-Token", "whook");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<PluckApiOptions>>();

        /// <summary>Uploads a test file with the supplied webhook destination.</summary>
        async Task<HttpResponseMessage> Upload(string destination)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(new byte[16]), "file", "example.dat");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/upload") { Content = form };
            request.Headers.Add("X-Pluck-Webhook", destination);
            return await client.SendAsync(request);
        }

        using var rejected = await Upload("https://receiver.example.invalid/topic");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Directory.GetFiles(options.CurrentValue.UploadDirectory).Should().BeEmpty();
        (await db.Files.CountAsync()).Should().Be(0);
        using var accepted = await Upload("https://receiver.example/topic");
        accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        (await accepted.Content.ReadAsStringAsync()).Should().NotContain("receiver.example");

        client.DefaultRequestHeaders.Range = new(0, 3);
        using var partial = await client.GetAsync("/f/whook");
        partial.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await db.WebhookDeliveries.CountAsync()).Should().Be(0);
        client.DefaultRequestHeaders.Range = new(4, null);
        using var resumed = await client.GetAsync("/f/whook");
        resumed.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        var delivery = await db.WebhookDeliveries.AsNoTracking().SingleAsync();
        var receiver = new Receiver();
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient("webhooks")).Returns(() => new HttpClient(receiver, false));
        var clock = new TestClock();
        var worker = new WebhookWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            clients.Object, options, clock, NullLogger<WebhookWorker>.Instance);
        await worker.DeliverNextAsync(db, default);
        (await worker.DeliverNextAsync(db, default)).Should().BeFalse();
        clock.Advance();
        await worker.DeliverNextAsync(db, default);
        receiver.EventIds.Should().Equal(delivery.Id.ToString(), delivery.Id.ToString());
        (await db.WebhookDeliveries.AsNoTracking().SingleAsync()).Status.Should().Be("delivered");

        await db.Files.ExecuteUpdateAsync(s => s.SetProperty(f => f.ExpiresAt, DateTime.UtcNow.AddHours(-1)));
        var cleanup = ActivatorUtilities.CreateInstance<FileCleanupBackgroundService>(scope.ServiceProvider);
        await cleanup.CleanUpFiles(default);
        await cleanup.CleanUpFiles(default);
        (await db.Files.CountAsync()).Should().Be(0);
        (await db.WebhookDeliveries.CountAsync(d => d.EventType == "file.expired")).Should().Be(1);
    }

    /// <summary>Verifies that unsupported webhook origins and URL components are rejected.</summary>
    [Theory]
    [InlineData("http://receiver.example/topic")]
    [InlineData("https://receiver.example:8000/topic")]
    [InlineData("https://user:pass@receiver.example/topic")]
    [InlineData("https://receiver.example/topic#fragment")]
    [InlineData("https://sub.receiver.example/topic")]
    public void DestinationRequiresExactApprovedHttpsOrigin(string url)
    {
        new PluckApiOptions { WebhookHosts = ["receiver.example"] }.AllowsWebhook(url).Should().BeFalse();
    }

    /// <summary>Checks that the worker waits for a lease to expire and records the outcome of the next attempt.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Redirect, 0, "failed")]
    [InlineData(HttpStatusCode.BadRequest, 0, "failed")]
    [InlineData(HttpStatusCode.TooManyRequests, 4, "failed")]
    [InlineData(HttpStatusCode.OK, 1, "delivered")]
    public async Task Worker_RecoversLeaseAndClassifiesResponse(HttpStatusCode code, int previousAttempts,
        string expectedStatus)
    {
        using var factory = new Factory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = new TestClock();
        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            FileId = Guid.NewGuid(), Destination = "https://receiver.example/topic", Payload = "expired",
            EventType = "file.expired", OccurredAt = clock.GetUtcNow().UtcDateTime, Attempts = previousAttempts,
            NextAttemptAt = clock.GetUtcNow().UtcDateTime.AddMinutes(1)
        });
        await db.SaveChangesAsync();
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient("webhooks")).Returns(() => new HttpClient(new FixedResponse(code)));
        var worker = new WebhookWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), clients.Object,
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<PluckApiOptions>>(), clock,
            NullLogger<WebhookWorker>.Instance);
        (await worker.DeliverNextAsync(db, default)).Should().BeFalse();
        clock.Advance();
        (await worker.DeliverNextAsync(db, default)).Should().BeTrue();
        var saved = await db.WebhookDeliveries.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(expectedStatus);
        saved.Attempts.Should().Be(previousAttempts + 1);
    }

    /// <summary>Simulates a receiver that always returns the configured HTTP status.</summary>
    private sealed class FixedResponse(HttpStatusCode code) : HttpMessageHandler
    {
        /// <summary>Returns the configured status for each delivery attempt.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(code));
    }

    /// <summary>Provides a controllable UTC clock for retry and lease tests.</summary>
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        /// <summary>Returns the current simulated UTC time.</summary>
        public override DateTimeOffset GetUtcNow() => _now;
        /// <summary>Advances simulated time by two minutes.</summary>
        public void Advance() => _now = _now.AddMinutes(2);
    }
}
