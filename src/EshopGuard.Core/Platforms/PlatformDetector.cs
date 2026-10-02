using AngleSharp.Html.Parser;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Core.Platforms;

/// <summary>
/// Recognizes the platform of an e-shop from technical signatures only (change 10, AD 2): meta tags, hosts and paths of the
/// files the page loads (<c>script</c>, <c>link</c>, <c>img</c>, <c>source</c>), headers and cookie names of the response.
/// Links in the text (<c>a href</c>) and the text of the page are never read, so an article about Shopify changes nothing.
/// Signatures of two kinds give <see cref="PlatformConfidence.Certain"/>, one <see cref="PlatformConfidence.Likely"/>;
/// signatures of two platforms give <c>unknown</c> (fail-closed) with every signal listed.
/// </summary>
public static class PlatformDetector
{
    private static readonly string[] ResourceSelectors = ["script[src]", "link[href]", "img[src]", "source[src]", "iframe[src]"];

    /// <summary>The platform of the page by <paramref name="signatures"/>.</summary>
    public static PlatformDetection Detect(PlatformSignatures signatures, PlatformPage page)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(page);
        var facts = Read(page);
        var matched = new List<(string Platform, PlatformSignature Signature)>();
        foreach (var (platform, list) in signatures.ByPlatform)
        {
            matched.AddRange(list.Where(s => Matches(s, facts)).Select(s => (platform, s)));
        }

        var signals = matched.Select(m => m.Signature.Code).ToList();
        var platforms = matched.Select(m => m.Platform).Distinct(StringComparer.Ordinal).ToList();
        if (platforms.Count != 1)
        {
            return new PlatformDetection(PlatformDetection.UnknownPlatform, PlatformConfidence.Unknown, signals);
        }

        var kinds = matched.Select(m => m.Signature.Kind).Distinct(StringComparer.Ordinal).Count();
        return new PlatformDetection(platforms[0], kinds >= 2 ? PlatformConfidence.Certain : PlatformConfidence.Likely, signals);
    }

    private static bool Matches(PlatformSignature signature, PageFacts facts) => signature.Kind switch
    {
        SignatureKinds.Meta => facts.Metas.Any(m => m.Name == signature.Value && Contains(m.Content, signature.Contains)),
        SignatureKinds.Host => facts.Resources.Any(r => r.Host == signature.Value || r.Host.EndsWith("." + signature.Value, StringComparison.Ordinal)),
        SignatureKinds.Path => facts.Resources.Any(r => r.SameSite && r.Path.Contains(signature.Value, StringComparison.Ordinal)),
        SignatureKinds.Header => facts.Headers.Any(h => h.Key == signature.Value && Contains(h.Value, signature.Contains)),
        SignatureKinds.Cookie => facts.Cookies.Any(c => c.StartsWith(signature.Value, StringComparison.Ordinal)),
        _ => false,
    };

    private static bool Contains(string value, string? part) => part is null || value.Contains(part, StringComparison.Ordinal);

    private static PageFacts Read(PlatformPage page)
    {
        var html = HtmlDecoding.Decode(page.Body, page.Charset);
        var document = new HtmlParser().ParseDocument(html);
        var metas = document.QuerySelectorAll("meta[name]")
            .Select(m => (Name: m.GetAttribute("name")!.Trim().ToLowerInvariant(), Content: (m.GetAttribute("content") ?? "").ToLowerInvariant()))
            .ToList();
        var resources = new List<Resource>();
        foreach (var element in document.QuerySelectorAll(string.Join(", ", ResourceSelectors)))
        {
            var value = element.GetAttribute(element.HasAttribute("src") ? "src" : "href");
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(page.FinalUrl, value.Trim(), out var url) || url.Scheme is not ("http" or "https"))
            {
                continue;
            }

            resources.Add(new Resource(url.IdnHost.ToLowerInvariant(), url.AbsolutePath.ToLowerInvariant(), SameSite(url, page.FinalUrl)));
        }

        var headers = page.Headers.Select(h => (Key: h.Key.Trim().ToLowerInvariant(), Value: h.Value.ToLowerInvariant()))
            .Select(h => new KeyValuePair<string, string>(h.Key, h.Value)).ToList();
        var cookies = page.SetCookies.Select(c => c.Split('=', 2)[0].Trim()).Where(c => c.Length > 0).ToList();
        return new PageFacts(metas, resources, headers, cookies);
    }

    /// <summary>A file of the e-shop itself (its host with or without <c>www.</c>): only such paths count as paths of the platform.</summary>
    private static bool SameSite(Uri url, Uri page) =>
        string.Equals(Bare(url.IdnHost), Bare(page.IdnHost), StringComparison.OrdinalIgnoreCase);

    private static string Bare(string host) => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private sealed record Resource(string Host, string Path, bool SameSite);

    private sealed record PageFacts(
        List<(string Name, string Content)> Metas, List<Resource> Resources, List<KeyValuePair<string, string>> Headers, List<string> Cookies);
}
