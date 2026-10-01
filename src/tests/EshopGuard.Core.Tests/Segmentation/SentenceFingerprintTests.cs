using EshopGuard.Core.Models;
using EshopGuard.Core.Segmentation;

namespace EshopGuard.Core.Tests;

/// <summary>The 64-bit fingerprint of a sentence (task 4.6).</summary>
public sealed class SentenceFingerprintTests
{
    [Fact]
    public void SameSentence_WithOtherCaseAndSpacing_HasTheSameFingerprint() =>
        Assert.Equal(
            SentenceFingerprint.Of("Šampón je  100 % prírodný."),
            SentenceFingerprint.Of(" šampón JE 100 %\nprírodný. "));

    [Fact]
    public void OtherText_HasAnotherFingerprint() =>
        Assert.NotEqual(SentenceFingerprint.Of("Šampón je prírodný."), SentenceFingerprint.Of("Šampón je ekologický."));

    [Fact]
    public void SameSentence_InAnotherContext_HasTheSameFingerprint()
    {
        Segment Segment(string before) => new() { Hash = "h" + before, Kind = SegmentKind.Sentence, Text = "Balenie je recyklovateľné.", ContextBefore = before };

        Assert.Equal(Segment("Prvý kontext.").Fingerprint, Segment("Úplne iný kontext.").Fingerprint);
        Assert.Equal(SentenceFingerprint.Of("Balenie je recyklovateľné."), Segment("").Fingerprint);
    }

    [Fact]
    public void Fingerprint_IsTheFirst8BytesOfSha256BigEndian()
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("abc"));

        Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash), SentenceFingerprint.Of("ABC"));
    }
}
