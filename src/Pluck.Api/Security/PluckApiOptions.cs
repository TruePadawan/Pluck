using System.ComponentModel.DataAnnotations;

namespace Pluck.Api.Security;

public class PluckApiOptions
{
    public const string SectionName = "PluckApi";

    [Required] public string AdminKey { get; set; } = string.Empty;
    [Required] public string UploadDirectory { get; set; } = string.Empty;
    public string[] WebhookHosts { get; set; } = [];

    /// <summary>Checks that the URL uses HTTPS on port 443, matches an approved host, and contains no credentials or fragment.</summary>
    public bool AllowsWebhook(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri is { Port: 443, UserInfo.Length: 0, Fragment.Length: 0 } &&
        WebhookHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);

    public string GeoDbPath => "Data/GeoLite2-City.mmdb";
}