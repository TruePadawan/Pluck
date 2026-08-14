namespace Pluck.Shared.Models;

/// <summary>
/// Parameter object for creating or updating a <see cref="User"/> entity.
/// </summary>
public record UserParams
{
    public required string Name { get; init; }
    public required string ApiKeyHash { get; init; }
    public required string Role { get; init; }
}
