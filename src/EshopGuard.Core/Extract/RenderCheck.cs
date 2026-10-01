using AngleSharp.Dom;

namespace EshopGuard.Core.Extract;

/// <summary>
/// Recognizes pages whose text the site renders only with JavaScript. The tool reads HTML without running scripts,
/// so such a page looks empty; it must be reported as not checked, never as a page without findings.
/// Measured on the document as downloaded, before the extraction removes anything.
/// </summary>
internal static class RenderCheck
{
    /// <summary>A <c>noscript</c> element asks the visitor to turn JavaScript on.</summary>
    public const string NoscriptMessage = "noscript_message";

    /// <summary>A script stores the state of a client-side application (Redux, Apollo and similar).</summary>
    public const string AppState = "app_state";

    /// <summary>An empty container into which a client-side framework renders the page.</summary>
    public const string EmptyAppRoot = "empty_app_root";

    private static readonly string[] AppRootIds = ["root", "app", "__next", "__nuxt", "___gatsby", "q-app", "svelte"];

    private static readonly (string Marker, string App)[] StateScripts =
    [
        ("window.__NUXT__", "Nuxt"),
        ("window.__remixContext", "Remix"),
        ("window.__INITIAL_STATE__", AppState),
        ("window.__PRELOADED_STATE__", AppState),
        ("window.__APOLLO_STATE__", AppState),
    ];

    /// <param name="VisibleChars">Characters of readable text in the body (scripts, styles, templates and form controls left out).</param>
    /// <param name="ScriptApp">Framework name or one of the codes above; null when the page shows no sign of an application.</param>
    public sealed record Result(int VisibleChars, string? ScriptApp);

    public static Result Inspect(IDocument document) =>
        new(document.Body is { } body ? CountVisible(body) : 0, FindScriptApp(document));

    /// <summary>Readable characters under the node, leaving out the elements whose text is never read (<see cref="HtmlText.IsSkipped"/>).</summary>
    internal static int CountVisible(INode node)
    {
        var count = 0;
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
            {
                count += TextTools.Clean(text.Data).Length;
            }
            else if (child is IElement element && !HtmlText.IsSkipped(element.LocalName))
            {
                count += CountVisible(element);
            }
        }

        return count;
    }

    private static string? FindScriptApp(IDocument document)
    {
        if (document.GetElementById("__NEXT_DATA__") is not null)
        {
            return "Next.js";
        }

        if (document.GetElementById("___gatsby") is not null)
        {
            return "Gatsby";
        }

        if (document.GetElementById("__nuxt") is not null)
        {
            return "Nuxt";
        }

        if (document.QuerySelector("[ng-version], app-root") is not null)
        {
            return "Angular";
        }

        if (document.QuerySelector("[data-reactroot]") is not null)
        {
            return "React";
        }

        foreach (var script in document.Scripts)
        {
            var code = script.Text;
            foreach (var (marker, app) in StateScripts)
            {
                if (code.Contains(marker, StringComparison.Ordinal))
                {
                    return app;
                }
            }
        }

        if (document.QuerySelectorAll("noscript").Any(n => n.TextContent.Contains("javascript", StringComparison.OrdinalIgnoreCase)))
        {
            return NoscriptMessage;
        }

        return AppRootIds.Any(id => document.GetElementById(id) is { } root && CountVisible(root) == 0) ? EmptyAppRoot : null;
    }
}
