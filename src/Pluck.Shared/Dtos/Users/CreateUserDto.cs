namespace Pluck.Shared.Dtos.Users;

public record CreateUserDto(string Name, string ApiKeyHash, string Role);