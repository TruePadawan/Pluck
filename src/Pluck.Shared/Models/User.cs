namespace Pluck.Shared.Models;

public sealed class User : EntityBase
{
    public string Name { get; private set; }
    public string ApiKeyHash { get; private set; }
    public string Role { get; private set; }
    public ICollection<File> Files { get; private set; }

    // For ORM frameworks
    private User()
    {
        Name = string.Empty;
        ApiKeyHash = string.Empty;
        Role = string.Empty;
        Files = [];
    }

    private User(string name, string apiKeyHash, string role)
    {
        Name = name;
        ApiKeyHash = apiKeyHash;
        Role = role;
        Files = [];
    }

    public static User Create(string name, string apiKeyHash, string role)
    {
        ValidateInputs(name, apiKeyHash, role);
        return new User(name.ToLowerInvariant(), apiKeyHash, role);
    }

    public void Update(string name, string apiKeyHash, string role)
    {
        ValidateInputs(name, apiKeyHash, role);
        Name = name;
        ApiKeyHash = apiKeyHash;
        Role = role;

        UpdateLastModified();
    }

    private static void ValidateInputs(string name, string apiKeyHash, string role)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        if (string.IsNullOrWhiteSpace(apiKeyHash))
            throw new ArgumentException("API key hash cannot be null or empty", nameof(apiKeyHash));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty", nameof(role));
    }
}