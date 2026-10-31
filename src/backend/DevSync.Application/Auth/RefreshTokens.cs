using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace DevSync.Application.Auth;

/// <summary>
/// Refresh tokens are 256-bit random values. Only their SHA-256 hash is stored: a fast hash is
/// appropriate for high-entropy secrets (slow hashing is for low-entropy passwords).
/// </summary>
public static class RefreshTokens
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
