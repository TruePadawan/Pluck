using Pluck.Shared.Lib;

namespace Pluck.Tests.Unit.Lib;

public class TokenSanitizerTests
{
    [Fact]
    public void Sanitize_ValidToken_ReturnsValidResult()
    {
        var result = TokenSanitizer.Sanitize("my-file");

        result.IsValid.Should().BeTrue();
        result.SanitizedToken.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Sanitize_NullToken_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize(null!);

        result.IsValid.Should().BeFalse();
        result.SanitizedToken.Should().BeNull();
        result.ErrorMessage.Should().Be("Token cannot be empty");
    }

    [Fact]
    public void Sanitize_EmptyToken_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize("");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Token cannot be empty");
    }

    [Fact]
    public void Sanitize_WhitespaceToken_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize("   ");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Sanitize_TokenTooShort_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize("ab");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("3 and 60");
    }

    [Fact]
    public void Sanitize_ReservedWord_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize("admin");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Token cannot be a reserved word");
    }

    [Fact]
    public void Sanitize_ReservedWord_CaseInsensitive_ReturnsInvalidResult()
    {
        var result = TokenSanitizer.Sanitize("ADMIN");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Token cannot be a reserved word");
    }

    [Fact]
    public void Sanitize_TokenWithSpecialChars_SanitizesCorrectly()
    {
        var result = TokenSanitizer.Sanitize("My File!@#");

        result.IsValid.Should().BeTrue();
        result.SanitizedToken.Should().NotContain(" ");
        result.SanitizedToken.Should().NotContain("!");
        result.SanitizedToken.Should().NotContain("@");
        result.SanitizedToken.Should().NotContain("#");
    }
}
