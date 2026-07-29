using Pluck.Api.Persistence;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Repositories;

public class FileRepository(AppDbContext db)
{
    public async Task<File> CreateFileEntry(CreateFileDto fileDto)
    {
        var token = Utilities.GenerateId(6);
        var fileExpiryDate = DateTimeOffset.UtcNow.AddHours(fileDto.Ttl);
        var fileEntry = File.Create(token, fileDto.OwnerId, fileDto.DiskFileName, fileDto.OriginalFileName,
            fileDto.MaxDownloads, fileExpiryDate);
        db.Files.Add(fileEntry);
        await db.SaveChangesAsync();
        return fileEntry;
    }
}