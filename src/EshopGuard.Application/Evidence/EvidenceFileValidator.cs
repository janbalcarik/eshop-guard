using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EshopGuard.Application.Evidence;

/// <summary>A type of file of evidence: its content type and extension.</summary>
public sealed record EvidenceFileType(string ContentType, string Extension);

/// <summary>
/// The file of a piece of evidence (change 11, AD 10): its type by its content (the magic numbers of PDF, JPEG and PNG, never
/// the extension or the declared type) and a safe name to store it under (no path, no diacritics, only letters, digits,
/// dot, dash and underscore, with the extension of the detected type).
/// </summary>
public static partial class EvidenceFileValidator
{
    public static readonly EvidenceFileType Pdf = new("application/pdf", ".pdf");
    public static readonly EvidenceFileType Jpeg = new("image/jpeg", ".jpg");
    public static readonly EvidenceFileType Png = new("image/png", ".png");

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Bytes needed from the start of the file.</summary>
    public const int HeaderLength = 8;

    public static EvidenceFileType? Detect(ReadOnlySpan<byte> header) =>
        header.StartsWith("%PDF-"u8) ? Pdf
        : header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF ? Jpeg
        : header.StartsWith(PngSignature) ? Png
        : null;

    public static EvidenceFileType? ByExtension(string? fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".pdf" => Pdf,
        ".jpg" or ".jpeg" => Jpeg,
        ".png" => Png,
        _ => null,
    };

    public static string SafeName(string? fileName, EvidenceFileType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = (fileName ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        var plain = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(c);
            }
        }

        var safe = Dashes().Replace(Unsafe().Replace(plain.ToString(), "-"), "-").Trim('-', '.');
        if (safe.Length > 80)
        {
            safe = safe[..80].TrimEnd('-', '.');
        }

        return (safe.Length == 0 ? "doklad" : safe) + type.Extension;
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]")]
    private static partial Regex Unsafe();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex Dashes();
}
