using System.Security.Cryptography;
using System.Text;

namespace Pluck.Cli.Utils;

public static class SecurityUtilities
{
    public static byte[] GenerateSalt(int length = 16)
    {
        var salt = new byte[length];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    public static (byte[] EncryptionKey, byte[] AuthKey) DeriveKeys(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        const int iterations = 600000;
        var keyMaterial = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, 64);
        var encryptionKey = keyMaterial[..32];
        var authKey = keyMaterial[32..];
        return (encryptionKey, authKey);
    }

    public static (byte[] initializationVector, ICryptoTransform encryptor) SetupEncryption(byte[] encryptionKey)
    {
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        var iv = aes.IV;
        return (iv, aes.CreateEncryptor());
    }
}