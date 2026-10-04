using Slugify;

namespace Pluck.Shared.Lib;

public static class TokenSanitizer
{
    private static readonly HashSet<string> ReservedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "login", "admin", "dashboard", "upload", "settings", "f", "file"
    };

    /// <summary>
    /// Sanitizes a custom token to be used as a file name.
    /// </summary>
    /// <param name="token">raw token</param>
    public static SanitizationResult Sanitize(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return new SanitizationResult(null, false, "Token cannot be empty");

        if (token.Length is < 3 or > 60)
        {
            return new SanitizationResult(null, false, "Token must be between 3 and 60 characters long");
        }

        if (ReservedTokens.Contains(token))
        {
            return new SanitizationResult(null, false, "Token cannot be a reserved word");
        }

        var config = new SlugHelperConfiguration();
        var helper = new SlugHelper(config);
        var safeToken = helper.GenerateSlug(token);
        return new SanitizationResult(safeToken, true);
    }
}

public record SanitizationResult(string? SanitizedToken, bool IsValid, string? ErrorMessage = null);