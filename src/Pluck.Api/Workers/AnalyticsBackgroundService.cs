using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Api.Services.Analytics;
using Pluck.Shared.Models.Events;

namespace Pluck.Api.Workers;

public class AnalyticsBackgroundService : BackgroundService
{
    private readonly IAnalyticsQueue _analyticsQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsBackgroundService> _logger;

    public AnalyticsBackgroundService(
        IAnalyticsQueue analyticsQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsBackgroundService> logger)
    {
        _analyticsQueue = analyticsQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnalyticsBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _analyticsQueue.DequeueAsync(stoppingToken);
                await ProcessAnalyticsMessageAsync(message, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing analytics message.");
            }
        }
    }

    private async Task ProcessAnalyticsMessageAsync(DownloadAnalyticsMessage message, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // We use a simple hash of the IP for privacy
        var hashedIp = string.IsNullOrEmpty(message.IpAddress)
            ? "Unknown"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.IpAddress)));

        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == message.FileId, stoppingToken);
        if (file is null) return; // File deleted before analytics processed

        file.IncrementDownloadCount();

        // TODO: Implement Geolocation
        var downloadEvent = FileDownloadEvents.Create(
            fileId: file.Id,
            hashedIp: hashedIp,
            city: null,
            country: null,
            clientType: message.ClientType
        );

        db.FileDownloadEvents.Add(downloadEvent);
        await db.SaveChangesAsync(stoppingToken);
    }
}