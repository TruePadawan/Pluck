namespace Pluck.Shared.Models;

public class File
{
    public required string Token { get; set; }
    public required int OwnerId { get; set; }
    public required string DiskFileName { get; set; }
    public required string OriginalFileName { get; set; }
    public int? DownloadsLeft { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
};