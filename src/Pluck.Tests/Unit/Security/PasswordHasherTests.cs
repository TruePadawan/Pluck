using Pluck.Api.Security;

namespace Pluck.Tests.Unit.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsBcryptFormat()
    {
        var hash = PasswordHasher.Hash("password123");

        hash.Should().NotBeNull();
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void Hash_DifferentPasswords_ReturnDifferentHashes()
    {
        var hash1 = PasswordHasher.Hash("password1");
        var hash2 = PasswordHasher.Hash("password2");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_SamePassword_ReturnsDifferentHashes()
    {
        var hash1 = PasswordHasher.Hash("password");
        var hash2 = PasswordHasher.Hash("password");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var password = "mysecretpassword";
        var hash = PasswordHasher.Hash(password);

        var result = PasswordHasher.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_IncorrectPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("correct");

        var result = PasswordHasher.Verify("incorrect", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("password");

        var result = PasswordHasher.Verify("", hash);

        result.Should().BeFalse();
    }
}
