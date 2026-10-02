using System.Buffers.Text;
using System.Security.Cryptography;

namespace EshopGuard.Application.Security;

/// <summary>
/// One-time tokens of sign-in links, password resets and invitations (AD 2): 32 random bytes, base64url (43 characters) in the
/// e-mail only; the database keeps <c>SHA-256</c> of the bytes.
/// </summary>
public static class OneTimeTokens
{
    public const int Bytes = 32;

    /// <summary>Length of the token in base64url without padding.</summary>
    public const int Length = 43;

    /// <summary>A new token and its hash.</summary>
    public static (string Token, byte[] Hash) Create()
    {
        Span<byte> bytes = stackalloc byte[Bytes];
        RandomNumberGenerator.Fill(bytes);
        var token = Base64Url.EncodeToString(bytes);
        var hash = SHA256.HashData(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        return (token, hash);
    }

    /// <summary>Hash of a token from a request, or <c>null</c> when it is not a token of this format (then it is invalid).</summary>
    public static byte[]? TryHash(string? token)
    {
        if (token is null || token.Length != Length)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[Bytes + 2];
        if (Base64Url.DecodeFromChars(token, bytes, out var consumed, out var written) != System.Buffers.OperationStatus.Done
            || consumed != Length || written != Bytes)
        {
            return null;
        }

        var hash = SHA256.HashData(bytes[..Bytes]);
        CryptographicOperations.ZeroMemory(bytes);
        return hash;
    }
}
