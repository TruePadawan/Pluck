using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Lib;
using Pluck.Shared.Models;
using Pluck.Shared.Params;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Repositories;

public class FileRepository(AppDbContext db)
{
    /// <summary>Persists a new share with its expiration, download allowance, and optional webhook destination.</summary>
    public async Task<File> CreateFile(CreateFileDto fileDto)
    {
        var token = SharedUtilities.GenerateId(6);
        var fileExpiryDate = DateTime.UtcNow.AddHours(fileDto.Ttl);
        var fileEntry = File.Create(new FileParams
        {
            WebhookUrl = fileDto.WebhookUrl,
            Token = fileDto.Token ?? token,
            OwnerId = fileDto.OwnerId,
            DiskFileName = fileDto.DiskFileName,
            OriginalFileName = fileDto.OriginalFileName,
            ContentType = fileDto.ContentType,
            Size = fileDto.Size,
            DownloadsLeft = fileDto.MaxDownloads,
            ExpiresAt = fileExpiryDate,
            IsDirectory = fileDto.IsDirectory,
            AuthHash = fileDto.AuthHash,
            Salt = fileDto.Salt,
            IV = fileDto.IV
        });
        db.Files.Add(fileEntry);
        await db.SaveChangesAsync();
        return fileEntry;
    }

    /// <summary>
    /// Returns the file with the specified token if it exists and is not expired
    /// </summary>
    public async Task<File?> GetFileByToken(string token)
    {
        return await db.Files.SingleOrDefaultAsync(f => f.Token == token &&
                                                        (f.DownloadsLeft == null || f.DownloadsLeft > 0)
                                                        && f.ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>Records a counted download, atomically consuming any remaining allowance and queuing its configured notification.</summary>
    public async Task DecrementDownloadsLeft(File file)
    {
        if (file.DownloadsLeft is null && file.WebhookUrl is null) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var active = db.Files.Where(f => f.Id == file.Id);
        if (file.DownloadsLeft.HasValue)
        {
            var changed = await active.Where(f => f.DownloadsLeft > 0).ExecuteUpdateAsync(s => s
                .SetProperty(f => f.DownloadsLeft, f => f.DownloadsLeft - 1)
                .SetProperty(f => f.LastModifiedAt, DateTime.UtcNow));
            if (changed == 0) return;
            await db.Entry(file).ReloadAsync();
        }
        else if (!await active.AnyAsync()) return;

        if (file.WebhookUrl != null)
        {
            db.WebhookDeliveries.Add(WebhookDelivery.Downloaded(file));
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Returns all unexpired files that belong to the specified user
    /// </summary>
    public async Task<List<File>> GetFilesByName(string name)
    {
        return await db.Files.Where(f => f.Owner.Name == name &&
                                         (f.DownloadsLeft == null || f.DownloadsLeft > 0)
                                         && f.ExpiresAt > DateTime.UtcNow).ToListAsync();
    }

    /// <summary>
    /// Returns all unexpired files
    /// </summary>
    /// <returns></returns>
    public async Task<List<File>> GetAllFiles()
    {
        return await db.Files.Where(f => (f.DownloadsLeft == null || f.DownloadsLeft > 0)
                                         && f.ExpiresAt > DateTime.UtcNow).ToListAsync();
    }

    /// <summary>
    /// Deletes a file specified by the passed token
    /// </summary>
    /// <param name="token"></param>
    public async Task DeleteFileByToken(string token)
    {
        var file = await db.Files.SingleOrDefaultAsync(f => f.Token == token);
        if (file is null) return;
        db.Files.Remove(file);
        await db.SaveChangesAsync();
    }
}