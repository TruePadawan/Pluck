namespace Pluck.Shared.Models;

public record User(
    int Id,
    string Name,
    string ApiKeyHash,
    string Role,
    DateTime CreatedAt
);