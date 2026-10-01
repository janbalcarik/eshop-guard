using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EshopGuard.Core.Segmentation;

/// <summary>
/// 64-bit fingerprint of a sentence or paragraph: the first 8 bytes of SHA-256 of the text normalized for hashing
/// (whitespace collapsed, lower case), big-endian. The same sentence in another context or with other spacing or case has
/// the same fingerprint, so it is found across the pages of a shop and its language versions. For about 300 000 sentences
/// of one shop a collision is negligible (birthday bound about 2.4·10⁻⁹).
/// </summary>
internal static class SentenceFingerprint
{
    public static long Of(string text)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(TextTools.NormalizeForHash(text)), hash);
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }
}
