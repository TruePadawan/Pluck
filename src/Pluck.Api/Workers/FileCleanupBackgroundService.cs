using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Persistence;
using Pluck.Api.Security;
using Pluck.Shared.Models;
using File = System.IO.File;

namespace Pluck.Api.Workers;

/// <summary>
/// Background service that cleans up expired files every 10 mins
/// </summary>
public class FileCleanupBackgroundService(
    ILogger<FileCleanupBackgroundService> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanUpFiles(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[LOG] File cleanup loop iteration failed. Continuing");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    /// <summary>
    /// Removes expired and exhausted shares and queues expiry notifications for time-expired shares with remaining downloads.
    /// </summary>
    /// <param name="stoppingToken"></param>
    public async Task CleanUpFiles(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var apiOptions = scope.ServiceProvider.GetRequiredService<IOptions<PluckApiOptions>>();
        var config = apiOptions.Value;

        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
        var expiredFiles = await db.Files
            .Where(file => file.DownloadsLeft <= 0 || file.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(stoppingToken);
        // Remove the files from the disk
        expiredFiles.ForEach(file =>
        {
            var filePath = Path.Combine(config.UploadDirectory, file.DiskFileName);
            File.Delete(filePath);
            if (file.WebhookUrl != null && file.DownloadsLeft is not <= 0)
                db.WebhookDeliveries.Add(WebhookDelivery.Expired(file));
        });
        // Remove the files from the database
        db.Files.RemoveRange(expiredFiles);
        await db.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);
    }
}