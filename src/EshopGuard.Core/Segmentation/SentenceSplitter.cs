using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Segmentation;

/// <summary>
/// Splits Czech and Slovak text into sentences. Knows common abbreviations, dates such as "27. 9. 2026",
/// initials, decimal numbers and quotes.
/// </summary>
internal sealed class SentenceSplitter
{
    private readonly HashSet<string> _nonTerminal;
    private readonly HashSet<string> _terminal;

    public SentenceSplitter(IOptions<EshopGuardOptions> options)
        : this(options.Value.Segmentation)
    {
    }

    public SentenceSplitter(SegmentationOptions options)
    {
        _nonTerminal = options.NonTerminalAbbreviations.Select(NormalizeAbbreviation).ToHashSet(StringComparer.Ordinal);
        _terminal = options.TerminalAbbreviations.Select(NormalizeAbbreviation).ToHashSet(StringComparer.Ordinal);
    }

    public List<string> Split(string text)
    {
        var sentences = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is not ('.' or '!' or '?' or '…'))
            {
                i++;
                continue;
            }

            var end = i + 1;
            while (end < text.Length && text[end] is '.' or '!' or '?' or '…')
            {
                end++;
            }

            while (end < text.Length && IsClosing(text[end]))
            {
                end++;
            }

            // A boundary needs whitespace after the punctuation: "3.5", "www.shop.cz" and "s.r.o" stay intact.
            if (end >= text.Length || !char.IsWhiteSpace(text[end]))
            {
                i = end;
                continue;
            }

            var next = end;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            if (next < text.Length && IsBoundary(text, start, i, end, text[next]))
            {
                Add(sentences, text[start..end]);
                start = next;
            }

            i = next;
        }

        if (start < text.Length)
        {
            Add(sentences, text[start..]);
        }

        return sentences;
    }

    /// <summary>Splits a too long sentence at a comma, semicolon, dash or space.</summary>
    public static List<string> SplitLong(string sentence, int maxLength)
    {
        var parts = new List<string>();
        var rest = sentence.Trim();
        while (rest.Length > maxLength)
        {
            var cut = FindCut(rest, maxLength);
            parts.Add(rest[..cut].Trim());
            rest = rest[cut..].Trim();
        }

        if (rest.Length > 0)
        {
            parts.Add(rest);
        }

        return parts;
    }

    private bool IsBoundary(string text, int sentenceStart, int punctuationIndex, int punctuationEnd, char next)
    {
        var nextIsLower = char.IsLower(next);
        var nextIsUpperOrOpening = char.IsUpper(next) || IsOpening(next);
        var punctuation = text[punctuationIndex];
        var isEllipsis = punctuation == '…' || punctuationEnd - punctuationIndex > 1 && text[punctuationIndex + 1] == '.';
        if (punctuation != '.' || isEllipsis)
        {
            return !nextIsLower;
        }

        var token = TokenBefore(text, sentenceStart, punctuationIndex);
        if (token.Length == 0)
        {
            return !nextIsLower;
        }

        var normalized = NormalizeAbbreviation(token);
        if (_nonTerminal.Contains(normalized))
        {
            return false;
        }

        // Initials and one-letter abbreviations: "J. Novák", "č. 5", "t. j.", "a. s.".
        if (token.Length == 1 && char.IsLetter(token[0]))
        {
            return false;
        }

        // Numbered headings and list items: "3. Odstoupení od smlouvy" stays together.
        if (token.All(char.IsDigit) && punctuationIndex - token.Length == sentenceStart)
        {
            return false;
        }

        // Dates and ordinals: "27. 9. 2026", "1. ledna" stay together; "roku 2010. Od té doby" splits.
        if (token.All(char.IsDigit))
        {
            return nextIsUpperOrOpening;
        }

        if (_terminal.Contains(normalized))
        {
            return nextIsUpperOrOpening;
        }

        return !nextIsLower;
    }

    private static string TokenBefore(string text, int sentenceStart, int punctuationIndex)
    {
        var begin = punctuationIndex;
        while (begin > sentenceStart && !char.IsWhiteSpace(text[begin - 1]))
        {
            begin--;
        }

        return text[begin..punctuationIndex].TrimStart('(', '[', '„', '"', '“', '‚', '\'', '«', '»');
    }

    private static int FindCut(string text, int maxLength)
    {
        var window = text[..maxLength];
        var minimum = maxLength / 2;
        foreach (var separator in new[] { "; ", ", ", " – ", " - " })
        {
            var index = window.LastIndexOf(separator, StringComparison.Ordinal);
            if (index >= minimum)
            {
                return index + separator.TrimEnd().Length;
            }
        }

        var space = window.LastIndexOf(' ');
        return space >= minimum ? space : maxLength;
    }

    private static void Add(List<string> sentences, string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Any(char.IsLetterOrDigit))
        {
            sentences.Add(trimmed);
        }
    }

    private static string NormalizeAbbreviation(string value) => value.Trim().TrimEnd('.').ToLowerInvariant();

    private static bool IsClosing(char c) => c is ')' or ']' or '"' or '\'' or '“' or '”' or '’' or '»' or '«';

    private static bool IsOpening(char c) => c is '(' or '[' or '„' or '"' or '“' or '‚' or '«' or '»';
}
