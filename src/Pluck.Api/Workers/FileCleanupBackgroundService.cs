using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Persistence;
using Pluck.Api.Security;

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
                logger.LogError(ex, "File cleanup loop iteration failed. Continuing");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    /// <summary>
    /// Cleans up files that have expired
    /// </summary>
    /// <param name="stoppingToken"></param>
    private async Task CleanUpFiles(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var apiOptions = scope.ServiceProvider.GetRequiredService<IOptions<PluckApiOptions>>();
        var config = apiOptions.Value;

        var expiredFiles = await db.Files
            .Where(file => file.DownloadsLeft <= 0 || file.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(stoppingToken);
        // Remove the files from the disk
        expiredFiles.ForEach(file =>
        {
            var filePath = Path.Combine(config.UploadDirectory, file.DiskFileName);
            File.Delete(filePath);
        });
        // Remove the files from the database
        db.Files.RemoveRange(expiredFiles);
        await db.SaveChangesAsync(stoppingToken);
    }
}