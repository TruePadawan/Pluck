using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Models;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Repositories;

public class FileRepository(AppDbContext db)
{
    public async Task<File> CreateFile(CreateFileDto fileDto)
    {
        var token = Utilities.GenerateId(6);
        var fileExpiryDate = DateTime.UtcNow.AddHours(fileDto.Ttl);
        var fileEntry = File.Create(new FileParams
        {
            Token = token,
            OwnerId = fileDto.OwnerId,
            DiskFileName = fileDto.DiskFileName,
            OriginalFileName = fileDto.OriginalFileName,
            ContentType = fileDto.ContentType,
            DownloadsLeft = fileDto.MaxDownloads,
            ExpiresAt = fileExpiryDate,
            IsDirectory = fileDto.IsDirectory,
            PasswordHash = fileDto.PasswordHash
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

    public async Task DecrementDownloadsLeft(File file)
    {
        file.DecrementDownloadsLeft();
        await db.SaveChangesAsync();
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