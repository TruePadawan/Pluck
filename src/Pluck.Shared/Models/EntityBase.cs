namespace Pluck.Shared.Models;

public abstract class EntityBase
{
    public Guid Id { get; private init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; private set; } = DateTime.UtcNow;
    protected void UpdateLastModified() => LastModifiedAt = DateTime.UtcNow;
}