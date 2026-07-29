using System.Security.Cryptography;

namespace Pluck.Api.Utils;

public static class Utilities
{
    public static string GenerateId(int length)
    {
        // Defines allowed alphanumeric characters
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNPQRSTUVWXYZ23456789";
        return string.Create(length, chars, (buffer, alphabet) =>
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }
}