using Pluck.Api.Security;

namespace Pluck.Tests.Unit.Security;

public class KeyHasherTests
{
    [Fact]
    public void ComputeHash_ReturnsHexadecimalString()
    {
        var hash = KeyHasher.ComputeHash("test-key");

        hash.Should().NotBeNull();
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void ComputeHash_Returns64Characters()
    {
        var hash = KeyHasher.ComputeHash("test-key");

        hash.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeHash_SameInput_ReturnsSameHash()
    {
        var hash1 = KeyHasher.ComputeHash("my-api-key");
        var hash2 = KeyHasher.ComputeHash("my-api-key");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_ReturnsDifferentHash()
    {
        var hash1 = KeyHasher.ComputeHash("key1");
        var hash2 = KeyHasher.ComputeHash("key2");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_EmptyString_ReturnsValidHash()
    {
        var hash = KeyHasher.ComputeHash("");

        hash.Should().NotBeNull();
        hash.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeHash_CaseSensitive()
    {
        var hash1 = KeyHasher.ComputeHash("ApiKey");
        var hash2 = KeyHasher.ComputeHash("apikey");

        hash1.Should().NotBe(hash2);
    }
}
