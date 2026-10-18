using System.Net.Http.Headers;
using Pluck.Api.Utils;

namespace Pluck.Tests.Unit.Utils;

/// <summary>
/// Unit tests for <see cref="MultipartRequestHelper"/> static utility methods.
/// </summary>
public class MultipartRequestHelperTests
{
    [Fact]
    public void IsMultipartRequest_MultipartFormData_ReturnsTrue()
    {
        var result = MultipartRequestHelper.IsMultipartRequest("multipart/form-data; boundary=----WebKitFormBoundary");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsMultipartRequest_MultipartMixed_ReturnsTrue()
    {
        var result = MultipartRequestHelper.IsMultipartRequest("multipart/mixed; boundary=abc123");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsMultipartRequest_ApplicationJson_ReturnsFalse()
    {
        var result = MultipartRequestHelper.IsMultipartRequest("application/json");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsMultipartRequest_NullContentType_ReturnsFalse()
    {
        var result = MultipartRequestHelper.IsMultipartRequest(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsMultipartRequest_EmptyContentType_ReturnsFalse()
    {
        var result = MultipartRequestHelper.IsMultipartRequest("");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetBoundary_ValidMediaType_ReturnsBoundary()
    {
        var mediaType = new MediaTypeHeaderValue("multipart/form-data");
        mediaType.Parameters.Add(new NameValueHeaderValue("boundary", "----WebKitFormBoundary7MA4YWxk"));

        var result = MultipartRequestHelper.GetBoundary(mediaType);

        result.Should().Be("----WebKitFormBoundary7MA4YWxk");
    }

    [Fact]
    public void GetBoundary_MissingBoundary_ThrowsInvalidDataException()
    {
        var mediaType = new MediaTypeHeaderValue("multipart/form-data");

        var act = () => MultipartRequestHelper.GetBoundary(mediaType);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Missing multipart boundary descriptor");
    }
}
