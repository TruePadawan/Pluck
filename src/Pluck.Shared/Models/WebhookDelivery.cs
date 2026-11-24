namespace Pluck.Shared.Models;

/// <summary>Stores a webhook notification and its delivery state independently of the shared file.</summary>
public sealed class WebhookDelivery
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FileId { get; init; }
    public required string Destination { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime OccurredAt { get; init; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending";

    /// <summary>Creates a notification for a counted download of a file with a configured webhook.</summary>
    public static WebhookDelivery Downloaded(File file) => new()
    {
        FileId = file.Id, Destination = file.WebhookUrl!, EventType = "file.downloaded",
        OccurredAt = DateTime.UtcNow, Payload = $"Pluck recorded a download for share {file.Id}."
    };

    /// <summary>Creates an expiry notification using the file’s scheduled expiration time.</summary>
    public static WebhookDelivery Expired(File file) => new()
    {
        FileId = file.Id, Destination = file.WebhookUrl!, EventType = "file.expired",
        OccurredAt = file.ExpiresAt, Payload = $"Pluck share {file.Id} expired."
    };
}