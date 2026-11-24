using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Persistence;
using Pluck.Api.Security;

namespace Pluck.Api.Workers;

/// <summary>Delivers persisted webhook notifications and retries transient failures.</summary>
public sealed class WebhookWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory clients,
    IOptionsMonitor<PluckApiOptions> options,
    TimeProvider clock,
    ILogger<WebhookWorker> logger) : BackgroundService
{
    private static readonly int[] RetrySeconds = [30, 120, 600, 3600];
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Processes due deliveries and periodically removes old terminal records.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextPrune = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (Now >= nextPrune)
                {
                    var cutoff = Now.AddDays(-7);
                    await db.WebhookDeliveries.Where(d => d.Status != "pending" && d.NextAttemptAt < cutoff)
                        .ExecuteDeleteAsync(stoppingToken);
                    nextPrune = Now.AddHours(1);
                }

                if (await DeliverNextAsync(db, stoppingToken)) continue;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Webhook worker iteration failed ({ErrorType})", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken);
        }
    }

    /// <summary>Attempts to claim and process the next due delivery. Returns false when no delivery is due.</summary>
    public async Task<bool> DeliverNextAsync(AppDbContext db, CancellationToken token)
    {
        var now = Now;
        var due = db.WebhookDeliveries.Where(d => d.Status == "pending" && d.NextAttemptAt <= now);
        var delivery = await due.AsNoTracking().OrderBy(d => d.NextAttemptAt).FirstOrDefaultAsync(token);
        if (delivery == null) return false;

        var attempt = delivery.Attempts + 1;
        var claimed = await due.Where(d => d.Id == delivery.Id && d.Attempts == delivery.Attempts)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Attempts, attempt)
                .SetProperty(d => d.NextAttemptAt, now.AddMinutes(1)), token);
        if (claimed == 0) return true;

        var status = "failed";
        var next = now;
        int? code = null;
        TimeSpan? retryAfter = null;
        if (attempt <= RetrySeconds.Length + 1 && options.CurrentValue.AllowsWebhook(delivery.Destination))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Destination);
            request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "text/plain");
            request.Headers.Add("X-Pluck-Event", delivery.EventType);
            request.Headers.Add("X-Pluck-Event-Id", delivery.Id.ToString());
            request.Headers.Add("X-Pluck-Occurred-At",
                DateTime.SpecifyKind(delivery.OccurredAt, DateTimeKind.Utc).ToString("O"));
            try
            {
                using var client = clients.CreateClient("webhooks");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                code = (int)response.StatusCode;
                if (response.IsSuccessStatusCode) status = "delivered";
                var hint = response.Headers.RetryAfter;
                retryAfter = hint?.Delta ?? (hint?.Date - clock.GetUtcNow());
            }
            catch (HttpRequestException)
            {
                /* Retry transport failures. */
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                /* Request timeout. */
            }

            if (attempt <= RetrySeconds.Length && code is null or 408 or 429 or >= 500)
            {
                status = "pending";
                next = Now.AddSeconds(Math.Clamp(retryAfter?.TotalSeconds ?? 0, RetrySeconds[attempt - 1], 3600));
            }
        }

        await db.WebhookDeliveries.Where(d => d.Id == delivery.Id && d.Attempts == attempt)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, status).SetProperty(d => d.NextAttemptAt, next),
                token);
        logger.LogInformation("Webhook {Id} attempt {Attempt}: {Status}, HTTP {Code}", delivery.Id, attempt, status,
            code);
        return true;
    }
}