using EshopGuard.Core.Classify;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Reads a downloaded page once (<see cref="ParsedPage"/>) and takes from that one document the text blocks, the type of
/// the page, the structure of its template and the stored profile that fits it. The HTML is kept in
/// <see cref="IPageContentStore"/> for profiles written later. A page whose extraction takes longer than
/// <c>crawl.extract_timeout_seconds</c> is not read (<see cref="ExtractionStatus.NotProcessed"/>).
/// </summary>
internal sealed class ExtractStep(
    IPageExtractor extractor,
    PageClassifier classifier,
    IPageContentStore contents,
    IOptions<EshopGuardOptions> options,
    ILogger<ExtractStep> logger)
{
    /// <summary>Reason of a page that was not read in time.</summary>
    public const string TimeoutReason = "extract_timeout";

    /// <summary>One page right after its download; with <paramref name="persist"/> the extraction is stored too.</summary>
    public async Task<ExtractedPageRecord> ExtractPageAsync(
        SiteScope site, Uri finalUrl, string html, bool isHome, IReadOnlyList<PageProfile> storedProfiles, CancellationToken ct, bool persist = false)
    {
        var key = new PageContentKey(site.SiteKey, finalUrl.AbsoluteUri);
        await contents.PutHtmlAsync(key, PageContent.Compress(html), ct);
        var timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.Crawl.ExtractTimeoutSeconds));
        var work = Task.Run(() => Extract(finalUrl, html, isHome, storedProfiles, site.Version?.VersionLanguage), ct);
        try
        {
            var page = await work.WaitAsync(timeout, ct);
            if (persist)
            {
                await contents.PutExtractAsync(key, PipelineJson.Serialize(page), ct);
            }

            return page;
        }
        catch (TimeoutException)
        {
            // SmartReader cannot be interrupted; its work finishes in the background and is thrown away.
            _ = work.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            logger.LogWarning("Page {Url}: extraction took longer than {Seconds} s, the page is not checked", finalUrl, timeout.TotalSeconds);
            return NotProcessed(finalUrl, isHome, TimeoutReason);
        }
    }

    /// <summary>Pages of a batch downloaded without extraction; their HTML is read from the content store.</summary>
    public async Task<ExtractResult> ExtractBatchAsync(ExtractInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var pages = new List<ExtractedPageRecord>();
        foreach (var fetched in input.Pages)
        {
            if (fetched.Outcome != FetchOutcome.Ok)
            {
                continue;
            }

            if (fetched.Extract is { } inline)
            {
                pages.Add(inline);
                continue;
            }

            var key = new PageContentKey(input.Site.SiteKey, fetched.FinalUrl.AbsoluteUri);
            var html = await contents.GetHtmlAsync(key, ct)
                ?? throw new InvalidOperationException($"HTML of {fetched.FinalUrl} is not in the content store.");
            pages.Add(await ExtractPageAsync(input.Site, fetched.FinalUrl, PageContent.Decompress(html), fetched.IsHome, input.StoredProfiles, ct, persist: true));
        }

        return new ExtractResult(pages);
    }

    private ExtractedPageRecord Extract(Uri finalUrl, string html, bool isHome, IReadOnlyList<PageProfile> storedProfiles, string? versionLanguage)
    {
        var settings = options.Value;
        var parsed = ParsedPage.Parse(finalUrl, html);
        var content = extractor.Extract(finalUrl, parsed);
        var type = classifier.Classify(finalUrl, content, isHome);
        var info = new PageInfo
        {
            Url = finalUrl.AbsoluteUri,
            Type = type,
            Title = content.Title,
            MetaDescription = content.MetaDescription,
            JsonLdDescription = content.JsonLdDescription,
            Extraction = content.Method,
            Images = content.Images,
            MainText = string.Join('\n', content.MainBlocks.Select(b => b.Text)),
            Category = content.Category,
            VisibleTextChars = content.Render.VisibleChars,
            RestText = string.Join('\n', content.RestBlocks.Select(b => b.Text)),
            NavigationTextChars = content.NavigationChars,
            ListingTextChars = content.ListingChars,
            CheckedTextChars = content.MainBlocks.Concat(content.ChromeRegions.SelectMany(r => r)).Concat(content.RestBlocks).Sum(b => b.Text.Length),
            ScriptApp = content.Render.ScriptApp,
            TextNotLoaded = content.Render.VisibleChars < settings.Crawl.MinPageTextChars,
            Language = versionLanguage is { } version && version != "und" ? version : content.HtmlLang,
            HreflangGroup = Languages.HreflangGroups.Key(content.Alternates.Select(a => a.Url)),
            ProductIds = content.ProductIds,
        };
        if (versionLanguage is not null && content.HtmlLang is { } declared && !Markets.LanguageTags.SamePrimary(declared, versionLanguage))
        {
            logger.LogWarning("Page {Url} of the version {Version} declares html lang {Lang}", finalUrl, versionLanguage, declared);
        }

        // Legal pages and pages without loaded text never use a profile: missing information on a legal page is a finding.
        var body = parsed.Document.Body;
        var eligible = settings.Profiles.Enabled && type != PageType.Legal && !info.TextNotLoaded && body is not null;
        var page = new ExtractedPageRecord
        {
            Info = info,
            Content = content,
            IsHome = isHome,
            TextHash = TextHash(content),
            ProfileEligible = eligible,
            StructureTokens = eligible ? ProfileMatcher.StructureTokens(body!) : [],
        };

        logger.LogDebug("Page {Url}: {Type}, {Method}, {Blocks} main blocks", finalUrl, type, content.Method, content.MainBlocks.Count);
        if (info.TextNotLoaded)
        {
            logger.LogWarning("Page {Url}: only {Chars} characters of readable text, JavaScript sign: {App}", finalUrl, info.VisibleTextChars, info.ScriptApp ?? "none");
        }

        if (eligible && ProfileFitting.Fit(page, parsed.Document, storedProfiles, settings.Profiles.MaxUnknownShare) is { } blocked)
        {
            foreach (var region in blocked)
            {
                logger.LogInformation("Page {Url}, profile {Id}: skip region not used, {Reason}", info.Url, page.Fit!.ProfileId, region);
            }
        }

        return page;
    }

    /// <summary>A page that was downloaded but not read; it is reported as not checked.</summary>
    private static ExtractedPageRecord NotProcessed(Uri url, bool isHome, string reason) => new()
    {
        Info = new PageInfo { Url = url.AbsoluteUri, Type = isHome ? PageType.Home : PageType.Content, IncludedInAnalysis = false },
        Content = new ExtractedPage(),
        IsHome = isHome,
        Status = ExtractionStatus.NotProcessed,
        NotProcessedReason = reason,
    };

    /// <summary>SHA-256 of the readable text of the page before a profile: a new version is written only when it changes.</summary>
    internal static string TextHash(ExtractedPage content) =>
        TextTools.Sha256(string.Join('\n',
            new[] { content.Title, content.MetaDescription, content.JsonLdDescription }.Select(t => t ?? "")
                .Concat(content.MainBlocks.Select(b => b.Text))
                .Concat(content.ChromeRegions.SelectMany(r => r).Select(b => b.Text))
                .Concat(content.RestBlocks.Select(b => b.Text))));
}
