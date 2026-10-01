using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// A compact outline of a page for the profile model: every element with its id and classes, links as paths, texts cut to
/// <see cref="TextChars"/> characters, and long runs of similar siblings (menu items, product tiles) cut to
/// <see cref="KeepRepeats"/>. Scripts, styles, media and the head are left out. The vegis.sk and naturfyt.sk outlines of
/// 1. 10. 2026 had 20–54 thousand characters per page.
/// </summary>
internal static partial class PageOutline
{
    private const int KeepRepeats = 3;
    private const int TextChars = 70;
    private const int HrefChars = 50;
    private const int MaxIndent = 30;
    internal const string CutMarker = "<!-- outline cut -->";
    private const string RepeatMarker = "<!-- more similar siblings follow -->";

    private static readonly HashSet<string> Dropped =
    [
        "script", "style", "svg", "noscript", "template", "iframe", "link", "meta", "head", "picture", "source", "video",
        "audio", "canvas",
    ];

    public static string Build(IDocument document, int maxChars)
    {
        var body = document.Body;
        if (body is null)
        {
            return "";
        }

        var outline = new StringBuilder();
        Render(body, 0, outline, maxChars);
        return outline.ToString();
    }

    /// <summary>Element name and classes with numbers replaced, so that <c>menu-item-123</c> and <c>menu-item-456</c> are alike.</summary>
    internal static string Signature(IElement element) =>
        element.LocalName + "." + string.Join(".", element.ClassList.Select(c => Digits().Replace(c, "#")).Order(StringComparer.Ordinal));

    /// <returns>False once the outline is full.</returns>
    private static bool Render(INode node, int depth, StringBuilder outline, int maxChars)
    {
        if (outline.Length >= maxChars)
        {
            outline.AppendLine(CutMarker);
            return false;
        }

        var indent = new string(' ', Math.Min(depth, MaxIndent));
        if (node is IText text)
        {
            var clean = TextTools.Clean(text.Data);
            if (clean.Length > 0)
            {
                outline.Append(indent).AppendLine(clean.Length > TextChars ? clean[..TextChars] + "…" : clean);
            }

            return true;
        }

        if (node is not IElement element || Dropped.Contains(element.LocalName))
        {
            return true;
        }

        outline.Append(indent).Append('<').Append(element.LocalName);
        if (element.Id is { Length: > 0 } id)
        {
            outline.Append(" id=\"").Append(id).Append('"');
        }

        if (element.ClassList.Length > 0)
        {
            outline.Append(" class=\"").Append(string.Join(' ', element.ClassList)).Append('"');
        }

        if (element.LocalName == "a" && element.GetAttribute("href") is { } href)
        {
            var path = Host().Replace(href, "");
            outline.Append(" href=\"").Append(path.Length > HrefChars ? path[..HrefChars] : path).Append('"');
        }

        outline.AppendLine(">");

        string? runSignature = null;
        var runLength = 0;
        foreach (var child in element.ChildNodes)
        {
            // Comments and blank text would break a run of similar siblings.
            if (child is not (IText or IElement) || (child is IText { Data: var data } && string.IsNullOrWhiteSpace(data)))
            {
                continue;
            }

            var signature = child is IElement childElement && !Dropped.Contains(childElement.LocalName) ? Signature(childElement) : null;
            if (signature is not null && signature == runSignature)
            {
                runLength++;
            }
            else
            {
                (runSignature, runLength) = (signature, 1);
            }

            if (signature is not null && runLength > KeepRepeats)
            {
                if (runLength == KeepRepeats + 1)
                {
                    outline.Append(indent).Append(' ').AppendLine(RepeatMarker);
                }

                continue;
            }

            if (!Render(child, depth + 1, outline, maxChars))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^https?://[^/]+", RegexOptions.IgnoreCase)]
    private static partial Regex Host();
}
