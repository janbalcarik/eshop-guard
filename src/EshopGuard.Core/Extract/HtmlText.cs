using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace EshopGuard.Core.Extract;

/// <summary>
/// Turns a DOM subtree into text blocks.
/// <list type="bullet">
/// <item>Block elements (paragraph, heading, list item, table cell, div…) and <c>br</c> end a block.</item>
/// <item>Text formatting inside a word stays joined: <c>&lt;b&gt;Eko&lt;/b&gt;logický</c> gives "Ekologický".</item>
/// <item>Other inline elements (span, a, label, img, unknown tags) are separated by a space, so badges such as
/// <c>&lt;span&gt;Novinka&lt;/span&gt;&lt;span&gt;EKO&lt;/span&gt;</c> give "Novinka EKO" instead of "NovinkaEKO".</item>
/// </list>
/// Content hidden by the <c>hidden</c> attribute or CSS is kept: collapsed tabs and accordions are still shown to customers.
/// </summary>
internal static partial class HtmlText
{
    private static readonly HashSet<string> SkippedElements =
    [
        "script", "style", "noscript", "template", "svg", "iframe", "select", "option", "textarea", "input",
        "button", "datalist", "canvas", "object", "embed", "video", "audio", "map", "head",
    ];

    /// <summary>Elements whose text is never read (scripts, form controls, media); <see cref="RenderCheck"/> leaves them out too.</summary>
    internal static bool IsSkipped(string localName) => SkippedElements.Contains(localName);

    private static readonly HashSet<string> BlockElements =
    [
        "p", "div", "section", "article", "main", "li", "ul", "ol", "table", "thead", "tbody", "tfoot", "tr", "td", "th",
        "dl", "dt", "dd", "blockquote", "figure", "figcaption", "caption", "pre", "address", "header", "footer",
        "aside", "nav", "form", "fieldset", "legend", "details", "summary", "hr", "body", "center",
    ];

    /// <summary>Inline elements used to format text, possibly inside a single word; they add no separator.</summary>
    private static readonly HashSet<string> FormattingElements =
    [
        "b", "strong", "i", "em", "u", "s", "strike", "del", "ins", "mark", "small", "big", "sub", "sup", "abbr",
        "acronym", "cite", "code", "kbd", "samp", "var", "dfn", "q", "font", "tt", "bdi", "bdo", "wbr", "ruby", "rt", "rp",
    ];

    /// <param name="root">Subtree to read.</param>
    /// <param name="skip">Elements to leave out with their whole subtree, e.g. navigation in the page frame.</param>
    public static List<TextBlock> ExtractBlocks(INode? root, Func<IElement, bool>? skip = null)
    {
        var blocks = new List<TextBlock>();
        if (root is null)
        {
            return blocks;
        }

        var buffer = new StringBuilder();
        Walk(root, buffer, blocks, skip);
        Flush(buffer, blocks);
        return blocks;
    }

    private static void Walk(INode node, StringBuilder buffer, List<TextBlock> blocks, Func<IElement, bool>? skip)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    buffer.Append(text.Data);
                    break;
                case IElement element:
                    VisitElement(element, buffer, blocks, skip);
                    break;
            }
        }
    }

    private static void VisitElement(IElement element, StringBuilder buffer, List<TextBlock> blocks, Func<IElement, bool>? skip)
    {
        var tag = element.LocalName;
        if (SkippedElements.Contains(tag) || (skip?.Invoke(element) ?? false))
        {
            return;
        }

        if (tag == "br")
        {
            Flush(buffer, blocks);
            return;
        }

        var headingLevel = tag is ['h', >= '1' and <= '6'] ? tag[1] - '0' : 0;
        if (headingLevel > 0)
        {
            Flush(buffer, blocks);
            var heading = new StringBuilder();
            AppendInline(element, heading);
            var text = Tidy(heading.ToString());
            if (text.Length > 0)
            {
                blocks.Add(new TextBlock(text, headingLevel));
            }

            return;
        }

        if (BlockElements.Contains(tag) || IsBadge(element))
        {
            Flush(buffer, blocks);
            Walk(element, buffer, blocks, skip);
            Flush(buffer, blocks);
        }
        else if (FormattingElements.Contains(tag))
        {
            Walk(element, buffer, blocks, skip);
        }
        else
        {
            buffer.Append(' ');
            Walk(element, buffer, blocks, skip);
            buffer.Append(' ');
        }
    }

    /// <summary>
    /// A product badge (for example "Eco", "Vegan", "Doprava zdarma") is a label of its own, not a word of a sentence:
    /// e-shop platforms mark them with classes such as flag, badge, label, sticker, ribbon or tag.
    /// </summary>
    private static bool IsBadge(IElement element) =>
        element.ClassList.Any(c => BadgeClasses.Contains(c));

    /// <summary>Classes that mark a badge; readers that remove classes must keep these.</summary>
    internal static IEnumerable<string> BadgeClassNames => BadgeClasses;

    private static readonly HashSet<string> BadgeClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "flag", "badge", "label", "sticker", "ribbon", "tag", "product-flag", "product-badge", "product-label",
    };

    /// <summary>Text of a heading: everything inside becomes one line with the same separator rules.</summary>
    private static void AppendInline(INode node, StringBuilder buffer)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    buffer.Append(text.Data);
                    break;
                case IElement element when SkippedElements.Contains(element.LocalName):
                    break;
                case IElement element when FormattingElements.Contains(element.LocalName):
                    AppendInline(element, buffer);
                    break;
                case IElement element:
                    buffer.Append(' ');
                    AppendInline(element, buffer);
                    buffer.Append(' ');
                    break;
            }
        }
    }

    private static void Flush(StringBuilder buffer, List<TextBlock> blocks)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        var text = Tidy(buffer.ToString());
        buffer.Clear();
        if (text.Length > 0)
        {
            blocks.Add(new TextBlock(text));
        }
    }

    /// <summary>Cleans whitespace and removes spaces that separators left before punctuation: "odkaz ." becomes "odkaz.".</summary>
    private static string Tidy(string text) =>
        SpaceAfterOpening().Replace(SpaceBeforePunctuation().Replace(TextTools.Clean(text), "$1"), "$1");

    [GeneratedRegex(@" ([.,;:!?)\]])")]
    private static partial Regex SpaceBeforePunctuation();

    [GeneratedRegex(@"([(\[]) ")]
    private static partial Regex SpaceAfterOpening();
}
