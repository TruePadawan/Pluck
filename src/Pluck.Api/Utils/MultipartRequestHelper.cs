using System.Net.Http.Headers;

namespace Pluck.Api.Utils;

public static class MultipartRequestHelper
{
    public static string GetBoundary(MediaTypeHeaderValue mediaType)
    {
        var boundary = mediaType.Parameters
            .FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrEmpty(boundary)
            ? throw new InvalidDataException("Missing multipart boundary descriptor")
            : boundary;
    }

    public static bool IsMultipartRequest(string? contentType)
    {
        return !string.IsNullOrEmpty(contentType) &&
               contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);
    }
}