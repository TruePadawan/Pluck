namespace Pluck.Shared.Models.Events;

public class FileDownloadEvents : EntityBase
{
    public Guid FileId { get; private set; }
    public File File { get; private set; }
    public string HashedIp { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }
    public ClientType ClientType { get; set; }

    private FileDownloadEvents()
    {
        FileId = Guid.Empty;
        HashedIp = string.Empty;
    }

    public static FileDownloadEvents Create(Guid fileId, string hashedIp, string? city, string? country, ClientType clientType)
    {
        return new FileDownloadEvents
        {
            FileId = fileId,
            HashedIp = hashedIp,
            City = city,
            Country = country,
            ClientType = clientType
        };
    }
}

public enum ClientType
{
    Unknown,
    Browser,
    PluckCli,
    Script
}