using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace EshopGuard.Core.Extract;

/// <summary>
/// One parse of a downloaded page, shared by every reader of the page (extraction, render check, profile tokens and the
/// profile of its template). Readers never change the document; they leave nodes out instead of removing them. SmartReader
/// changes the document it reads, so it gets a deep copy (cheaper than a second parse, the same results on 430 pages).
/// </summary>
internal sealed class ParsedPage
{
    private ParsedPage(Uri url, string html, IHtmlDocument document)
    {
        Url = url;
        Html = html;
        Document = document;
    }

    /// <summary>Final URL of the page.</summary>
    public Uri Url { get; }

    /// <summary>The HTML text (for SmartReader and storage).</summary>
    public string Html { get; }

    /// <summary>The document; must not be changed.</summary>
    public IHtmlDocument Document { get; }

    /// <summary>Parses the page once.</summary>
    public static ParsedPage Parse(Uri url, string html)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(html);
        HtmlParserProbe.Count();
        return new ParsedPage(url, html, new HtmlParser().ParseDocument(html));
    }
}

/// <summary>Counts parses of whole pages in the current async flow (tests check one parse per page).</summary>
internal static class HtmlParserProbe
{
    private static readonly AsyncLocal<Counter?> Current = new();

    /// <summary>Starts counting in this async flow; read <see cref="Counter.Value"/> afterwards.</summary>
    public static Counter Start() => Current.Value = new Counter();

    internal static void Count()
    {
        if (Current.Value is { } counter)
        {
            Interlocked.Increment(ref counter.Parses);
        }
    }

    /// <summary>Number of parses.</summary>
    public sealed class Counter
    {
        internal int Parses;

        public int Value => Volatile.Read(ref Parses);
    }
}
