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

    private User(UserParams p)
    {
        Name = p.Name;
        ApiKeyHash = p.ApiKeyHash;
        Role = p.Role;
        Files = [];
    }

    public static User Create(UserParams p)
    {
        ValidateInputs(p);
        return new User(p with { Name = p.Name.ToLowerInvariant() });
    }

    public void Update(UserParams p)
    {
        ValidateInputs(p);
        Name = p.Name;
        ApiKeyHash = p.ApiKeyHash;
        Role = p.Role;

        UpdateLastModified();
    }

    private static void ValidateInputs(UserParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
            throw new ArgumentException("Name cannot be null or empty", nameof(p.Name));
        if (string.IsNullOrWhiteSpace(p.ApiKeyHash))
            throw new ArgumentException("API key hash cannot be null or empty", nameof(p.ApiKeyHash));
        if (string.IsNullOrWhiteSpace(p.Role))
            throw new ArgumentException("Role cannot be null or empty", nameof(p.Role));
    }
}