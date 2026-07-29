namespace Pluck.Shared.Models;

public abstract class EntityBase
{
    public Guid Id { get; private init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; private init; } = DateTimeOffset.Now;
    public DateTimeOffset LastModifiedAt { get; private set; } = DateTimeOffset.Now;
    public void UpdateLastModified() => LastModifiedAt = DateTimeOffset.Now;
}