using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Profiles of the page templates of one scan.
/// <list type="number">
/// <item><see cref="PrepareAsync"/>, no model: every page is compared with the stored profiles of the site and uses the
/// one that leaves the least of its text outside the known regions, if that is at most <c>max_unknown_share</c>. Pages that
/// fit no profile are grouped by structure; groups of at least <c>min_template_pages</c> pages are planned for a new
/// profile and priced.</item>
/// <item><see cref="CreateAsync"/>, after the host confirmed the price: the largest group gets a profile from outlines of
/// its sample pages, the profile is checked on them and stored, the pages that fit it use it, and the remaining pages are
/// grouped again, up to <c>max_new_profiles_per_scan</c>.</item>
/// </list>
/// Legal pages and pages whose text was not loaded never use a profile: missing information on a legal page is a
/// finding of its own. A page without a profile is checked whole, as before profiles existed.
/// </summary>
internal sealed class PageProfiler(
    IPageProfileStore store,
    IProfileModel model,
    IOptions<EshopGuardOptions> options,
    ILogger<PageProfiler> logger)
{
    private ProfileOptions Settings => options.Value.Profiles;

    /// <summary>Host without "www." and the port when it is not the default: profiles of www.shop.sk and shop.sk are shared.</summary>
    public static string SiteKey(Uri url)
    {
        var host = url.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? url.Host[4..] : url.Host;
        host = host.ToLowerInvariant();
        return url.IsDefaultPort ? host : $"{host}:{url.Port}";
    }

    public async Task<ProfilingRun> PrepareAsync(Uri siteUrl, IReadOnlyList<CrawledPage> pages, CancellationToken ct)
    {
        var run = new ProfilingRun(SiteKey(siteUrl), pages.Select(p => new PageEntry(p)).ToList());
        if (!Settings.Enabled)
        {
            run.Disabled = true;
            return run;
        }

        run.Stored.AddRange(await store.GetAsync(run.Site, ct));
        foreach (var entry in run.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var info = entry.Page.Info;
            if (info.Type == PageType.Legal || info.TextNotLoaded || entry.Page.Html is null)
            {
                continue;
            }

            var body = Parse(entry.Page.Html).Body;
            if (body is null)
            {
                continue;
            }

            entry.Eligible = true;
            entry.Tokens = ProfileMatcher.StructureTokens(body);
            Fit(run, entry, body, run.Stored);
        }

        run.Planned.AddRange(Plan(run));
        run.UnavailableReason = run.Planned.Count > 0 ? model.UnavailableReason : null;
        logger.LogInformation("Profiles of {Site}: {Stored} stored, {Fit} of {Eligible} pages fit, {Planned} new planned (about {Cost} USD){Reason}",
            run.Site, run.Stored.Count, run.Entries.Count(e => e.Profile is not null), run.Entries.Count(e => e.Eligible),
            run.Planned.Count, run.EstimatedUsd, run.UnavailableReason is null ? "" : $", not created: {run.UnavailableReason}");
        return run;
    }

    public async Task CreateAsync(ProfilingRun run, CancellationToken ct)
    {
        if (!run.WillCreate)
        {
            return;
        }

        var failedLeaders = new HashSet<PageEntry>();
        while (run.Created.Count < Settings.MaxNewProfilesPerScan)
        {
            var clusters = Cluster(Unfitting(run));
            var cluster = clusters
                .Where(c => c.Count >= Settings.MinTemplatePages && !failedLeaders.Contains(c[0]))
                .OrderByDescending(c => c.Count)
                .FirstOrDefault();
            if (cluster is null)
            {
                break;
            }

            var samples = ChooseSamples(run, cluster, clusters);
            var documents = samples.Select(s => (Entry: s, Document: Parse(s.Page.Html!))).ToList();
            var outlines = documents.Select(d => (d.Entry.Page.Info.Url, PageOutline.Build(d.Document, Settings.MaxOutlineChars))).ToList();
            ProfileAnswer answer;
            try
            {
                answer = await model.AskAsync(outlines, ct);
            }
            catch (RewriteApiException ex) when (ex.IsFatal)
            {
                run.Warnings.Add($"Profily šablon se nevytvořily: {ex.Message} Stránky bez profilu se kontrolovaly celé.");
                logger.LogWarning("Profile model failed fatally: {Message}", ex.Message);
                break;
            }
            catch (Exception ex) when (ex is RewriteApiException or JsonException)
            {
                failedLeaders.Add(cluster[0]);
                run.Warnings.Add($"Profil šablony stránky {cluster[0].Page.Info.Url} se nepodařilo vytvořit ({ex.Message}); jejích {cluster.Count} stránek se kontrolovalo celých.");
                logger.LogWarning(ex, "Profile for the template of {Url} failed", cluster[0].Page.Info.Url);
                continue;
            }

            run.Calls++;
            run.InputTokens += answer.InputTokens;
            run.OutputTokens += answer.OutputTokens;
            run.CostUsd += answer.CostUsd;

            var profile = new PageProfile
            {
                Id = NextId(run),
                Site = run.Site,
                CreatedAt = DateTimeOffset.UtcNow,
                Model = answer.Model,
                PromptVersion = ProfilePrompt.Version,
                SampleUrls = samples.Select(s => s.Page.Info.Url).ToList(),
                Regions = Validate(answer.Regions, documents),
            };

            var fitting = documents.Count(d => d.Document.Body is { } body
                && ProfileMatcher.UnknownShare(body, profile, ProfileMatcher.KnownByStructure(body, new Uri(d.Entry.Page.Info.Url))) <= Settings.MaxUnknownShare);
            if (fitting == 0)
            {
                failedLeaders.Add(cluster[0]);
                run.Warnings.Add($"Profil šablony stránky {cluster[0].Page.Info.Url} nesedí ani na vzorové stránky, nepoužije se; jejích {cluster.Count} stránek se kontrolovalo celých.");
                logger.LogWarning("Profile for the template of {Url} fits none of its samples", cluster[0].Page.Info.Url);
                continue;
            }

            await store.AddAsync(profile, ct);
            run.Created.Add(profile);
            foreach (var entry in Unfitting(run))
            {
                if (Parse(entry.Page.Html!).Body is { } body)
                {
                    Fit(run, entry, body, [profile]);
                }
            }

            logger.LogInformation("Profile {Id}: {Regions} regions ({Rejected} rejected), {Pages} pages use it, {Cost} USD",
                profile.Id, profile.Regions.Count, profile.Regions.Count(r => r.RejectedBecause is not null),
                run.Uses.GetValueOrDefault(profile.Id)?.Pages ?? 0, answer.CostUsd);
        }
    }

    /// <summary>The best of the given profiles for the page; when it fits and is better than the page's current fit, it is applied.</summary>
    private void Fit(ProfilingRun run, PageEntry entry, IElement body, IReadOnlyList<PageProfile> profiles)
    {
        if (profiles.Count == 0 || entry.Profile is not null)
        {
            return;
        }

        var known = ProfileMatcher.KnownByStructure(body, new Uri(entry.Page.Info.Url));
        var (best, share) = profiles
            .Select(p => (Profile: p, Share: ProfileMatcher.UnknownShare(body, p, known)))
            .MinBy(x => x.Share);
        entry.Page.Info.ProfileUnknownShare = Math.Min(share, entry.Page.Info.ProfileUnknownShare ?? 1);
        if (share > Settings.MaxUnknownShare)
        {
            return;
        }

        var application = ProfileMatcher.Apply(Parse(entry.Page.Html!), entry.Page.Content, best);
        var content = application.Content;
        var info = entry.Page.Info;
        info.ProfileId = best.Id;
        info.ProfileSkippedTextChars = content.ProfileSkippedBlocks.Sum(b => b.Text.Length);
        info.ProfileSkippedText = string.Join('\n', content.ProfileSkippedBlocks.Select(b => b.Text));
        info.RestText = string.Join('\n', content.RestBlocks.Select(b => b.Text));
        info.CheckedTextChars = content.MainBlocks.Concat(content.ChromeRegions.SelectMany(r => r)).Concat(content.RestBlocks).Sum(b => b.Text.Length);
        entry.Profile = best;
        entry.Page = entry.Page with { Content = content };

        var use = run.Uses.TryGetValue(best.Id, out var existing)
            ? existing
            : run.Uses[best.Id] = new ProfileUse { Profile = best, CreatedInThisScan = run.Created.Contains(best) };
        use.Pages++;
        foreach (var (role, chars) in application.SkippedCharsByRole)
        {
            use.SkippedCharsByRole[role] = use.SkippedCharsByRole.GetValueOrDefault(role) + chars;
        }

        foreach (var blocked in application.Blocked)
        {
            logger.LogInformation("Page {Url}, profile {Id}: skip region not used, {Reason}", info.Url, best.Id, blocked);
        }
    }

    /// <summary>
    /// Regions as the model returned them, checked on the sample pages: an invalid selector is rejected; a skip region becomes
    /// a check region when its role may not be skipped, or when on a sample page it contains a check region or a longer block
    /// of the main text. The main text counts only on pages read by SmartReader: the fallback heuristic takes everything
    /// outside the frame as main text, cookie bars included. A check region still counts as a known part of the template.
    /// </summary>
    private static List<ProfileRegion> Validate(IReadOnlyList<ProfileRegion> regions, List<(PageEntry Entry, IDocument Document)> samples)
    {
        var bodies = samples.Where(s => s.Document.Body is not null).Select(s => (s.Entry, Body: s.Document.Body!)).ToList();
        var validated = regions.Select(region =>
        {
            if (string.IsNullOrWhiteSpace(region.Selector) || bodies.Any(b => !ProfileMatcher.IsValidSelector(b.Body, region.Selector)))
            {
                return region.Rejected("neplatný nebo nepodporovaný selektor");
            }

            if (region.Action != ProfileRegion.Skip || ProfileRegion.SkippableRoles.Contains(region.Role))
            {
                return region.Action is ProfileRegion.Check or ProfileRegion.Skip ? region : Checked(region, $"neznámá akce „{region.Action}“");
            }

            return Checked(region, $"roli „{region.Role}“ nelze přeskočit");
        }).ToList();

        var checks = validated.Where(r => r.RejectedBecause is null && r.Action == ProfileRegion.Check).ToList();
        return validated.Select(region =>
        {
            if (region.RejectedBecause is not null || region.Action != ProfileRegion.Skip)
            {
                return region;
            }

            var probe = new PageProfile { Id = "", Site = "", Regions = [.. checks, region] };
            foreach (var (entry, _) in bodies)
            {
                var application = ProfileMatcher.Apply(Parse(entry.Page.Html!), entry.Page.Content, probe);
                var blocked = application.Blocked.FirstOrDefault(b =>
                    b.Reason == ProfileMatcher.BlockReason.ContainsCheckedRegion || entry.Page.Info.Extraction == ExtractionMethod.Readability);
                if (blocked is not null)
                {
                    return Checked(region, $"na vzorové stránce {entry.Page.Info.Url} {blocked}");
                }
            }

            return region;
        }).ToList();
    }

    private static ProfileRegion Checked(ProfileRegion region, string why) => new()
    {
        Role = region.Role,
        Action = ProfileRegion.Check,
        Selector = region.Selector,
        Example = region.Example,
        Reason = $"[kontroluje se: {why}] {region.Reason}",
    };

    /// <summary>New profiles the run would write, priced from the outlines of their sample pages; no model is called.</summary>
    private IEnumerable<PlannedProfile> Plan(ProfilingRun run)
    {
        var clusters = Cluster(Unfitting(run));
        return clusters
            .Where(c => c.Count >= Settings.MinTemplatePages)
            .OrderByDescending(c => c.Count)
            .Take(Settings.MaxNewProfilesPerScan)
            .Select((cluster, i) =>
            {
                var samples = ChooseSamples(run, cluster, clusters, first: i == 0 && run.Stored.Count == 0);
                var outlines = samples.Select(s => PageOutline.Build(Parse(s.Page.Html!), Settings.MaxOutlineChars)).ToList();
                return new PlannedProfile(cluster[0].Page.Info.Url, cluster.Count, model.EstimateUsd(outlines));
            })
            .ToList();
    }

    private static string NextId(ProfilingRun run)
    {
        var taken = run.Stored.Concat(run.Created).Select(p => p.Id).ToHashSet();
        var number = taken.Count + 1;
        while (taken.Contains($"{run.Site}#{number}"))
        {
            number++;
        }

        return $"{run.Site}#{number}";
    }

    private static List<PageEntry> Unfitting(ProfilingRun run) => run.Entries.Where(e => e.Eligible && e.Profile is null).ToList();

    /// <summary>Groups pages of one template: a page joins the first group whose first page is similar enough, otherwise starts a new one.</summary>
    private List<List<PageEntry>> Cluster(List<PageEntry> entries)
    {
        var clusters = new List<List<PageEntry>>();
        foreach (var entry in entries)
        {
            var cluster = clusters.FirstOrDefault(c => ProfileMatcher.Similarity(c[0].Tokens, entry.Tokens) >= Settings.TemplateSimilarity);
            if (cluster is null)
            {
                clusters.Add([entry]);
            }
            else
            {
                cluster.Add(entry);
            }
        }

        return clusters;
    }

    /// <summary>
    /// The first page of the group and pages spread over the rest of it. The first profile of a site also gets the home
    /// page and the first page of the next group, so that it learns the frame shared by all templates (the probe of
    /// 1. 10. 2026 used home, product and category pages).
    /// </summary>
    private List<PageEntry> ChooseSamples(ProfilingRun run, List<PageEntry> cluster, List<List<PageEntry>> clusters, bool? first = null)
    {
        var count = Math.Max(1, Settings.SamplePages);
        var samples = new List<PageEntry> { cluster[0] };
        if (first ?? (run.Stored.Count == 0 && run.Created.Count == 0))
        {
            var others = clusters.Where(c => c != cluster).OrderByDescending(c => c.Count).Select(c => c[0]);
            var home = run.Entries.FirstOrDefault(e => e.Eligible && e.Profile is null && e.Page.Info.Type == PageType.Home && !cluster.Contains(e));
            foreach (var extra in new[] { home }.Concat(others).OfType<PageEntry>().Distinct().Take(count - 1))
            {
                samples.Add(extra);
            }
        }

        // The rest is spread over the group, so that the model sees how pages of the template differ.
        var need = count - samples.Count;
        var rest = cluster.Skip(1).Where(e => !samples.Contains(e)).ToList();
        for (var j = 0; j < need && j < rest.Count; j++)
        {
            var index = need == 1 || rest.Count == 1 ? 0 : (int)Math.Round((double)j * (rest.Count - 1) / (need - 1));
            if (!samples.Contains(rest[index]))
            {
                samples.Add(rest[index]);
            }
        }

        return samples;
    }

    private static IDocument Parse(string html) => new HtmlParser().ParseDocument(html);
}

/// <summary>
/// Profiles of one scan: the pages with the profile they use, stored and new profiles, the plan and its price.
/// </summary>
internal sealed class ProfilingRun(string site, List<PageEntry> entries)
{
    public string Site { get; } = site;

    public List<PageEntry> Entries { get; } = entries;

    public bool Disabled { get; set; }

    public List<PageProfile> Stored { get; } = [];

    public List<PageProfile> Created { get; } = [];

    public Dictionary<string, ProfileUse> Uses { get; } = [];

    public List<PlannedProfile> Planned { get; } = [];

    /// <summary>Why planned profiles will not be written (mock run, missing key), or null.</summary>
    public string? UnavailableReason { get; set; }

    public bool WillCreate => Planned.Count > 0 && UnavailableReason is null;

    public decimal EstimatedUsd => Planned.Sum(p => p.EstimatedUsd);

    public int Calls { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public decimal CostUsd { get; set; }

    public List<string> Warnings { get; } = [];

    public List<CrawledPage> Pages => Entries.Select(e => e.Page).ToList();
}

/// <summary>A page in the profiling of one scan.</summary>
internal sealed class PageEntry(CrawledPage page)
{
    public CrawledPage Page { get; set; } = page;

    /// <summary>False for legal pages, pages without loaded text and pages without HTML: they never use a profile.</summary>
    public bool Eligible { get; set; }

    public HashSet<string> Tokens { get; set; } = [];

    public PageProfile? Profile { get; set; }
}

/// <summary>A new profile the scan would write: the first page of its group, the size of the group and the estimated price.</summary>
internal sealed record PlannedProfile(string FirstUrl, int Pages, decimal EstimatedUsd);
