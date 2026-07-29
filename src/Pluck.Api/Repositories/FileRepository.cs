using Dapper;
using Pluck.Api.Data;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Repositories;

public class FileRepository(DbConnectionFactory dbFactory)
{
    public async Task<File> CreateFileEntry(FileDto fileDto)
    {
        var fileEntry = new File
        {
            Token = Utilities.GenerateId(6),
            OwnerId = fileDto.OwnerId,
            DiskFileName = fileDto.DiskFileName,
            OriginalFileName = fileDto.OriginalFileName,
            DownloadsLeft = fileDto.MaxDownloads,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(fileDto.Ttl)
        };
        const string createFileCommand = """
                                         INSERT INTO Files (Token, OwnerId, DiskFileName, OriginalFileName, DownloadsLeft, CreatedAt, ExpiresAt)
                                         VALUES (@Token, @OwnerId, @DiskFileName, @OriginalFileName, @DownloadsLeft, @CreatedAt, @ExpiresAt)
                                         """;
        using var dbConnection = dbFactory.CreateConnection();
        await dbConnection.ExecuteAsync(createFileCommand, fileEntry);
        return fileEntry;
    }
}