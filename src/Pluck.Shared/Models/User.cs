namespace Pluck.Shared.Models;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ApiKeyHash { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedAt { get; set; }
}