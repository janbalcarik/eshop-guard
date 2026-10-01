using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Profiles of the page templates of one site. Stored profiles are applied already by <see cref="ExtractStep"/>.
/// <list type="number">
/// <item><see cref="PlanAsync"/>, no model: pages that fit no profile are grouped by structure; groups of at least
/// <c>min_template_pages</c> pages are planned for a new profile and priced from outlines of their sample pages.</item>
/// <item><see cref="CreateAsync"/>, after the host confirmed the price: the largest group gets a profile from outlines of
/// its sample pages, the profile is checked on them and stored, the pages that fit it use it (<see cref="RefitAsync"/>),
/// and the remaining pages are grouped again, up to <c>max_new_profiles_per_scan</c>.</item>
/// </list>
/// Legal pages and pages whose text was not loaded never use a profile: missing information on a legal page is a finding
/// of its own. A page without a profile is checked whole. HTML of the pages is read from <see cref="IPageContentStore"/>.
/// </summary>
internal sealed class ProfileStep(
    IPageProfileStore store,
    IProfileModel model,
    IPageContentStore contents,
    IOptions<EshopGuardOptions> options,
    ILogger<ProfileStep> logger)
{
    private ProfileOptions Settings => options.Value.Profiles;

    /// <summary>Host without "www." and the port when it is not the default: profiles of www.shop.sk and shop.sk are shared.</summary>
    public static string SiteKey(Uri url)
    {
        var host = url.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? url.Host[4..] : url.Host;
        host = host.ToLowerInvariant();
        return url.IsDefaultPort ? host : $"{host}:{url.Port}";
    }

    /// <summary>Stored profiles of the site; none when profiles are off.</summary>
    public async Task<IReadOnlyList<PageProfile>> LoadStoredAsync(SiteScope site, CancellationToken ct) =>
        Settings.Enabled ? await store.GetAsync(site.SiteKey, ct) : [];

    /// <summary>The candidates of the planning from the extracted pages.</summary>
    public static ProfilePlanInput PlanInput(SiteScope site, IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<PageProfile> stored) =>
        new(site.SiteKey, pages.Select(p => new ProfileCandidate(p.Info.Url, p.Info.Type, p.ProfileEligible, p.Fit?.ProfileId, p.StructureTokens)).ToList())
        {
            StoredProfileIds = stored.Select(p => p.Id).ToList(),
        };

    /// <summary>New profiles the run would write, priced from the outlines of their sample pages; no model is called.</summary>
    public async Task<ProfilePlan> PlanAsync(ProfilePlanInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Settings.Enabled)
        {
            return ProfilePlan.Off(input.SiteKey);
        }

        var unfitting = input.Candidates.Where(c => c.Eligible && c.ProfileId is null).ToList();
        var clusters = Cluster(unfitting, c => c.Tokens);
        var planned = new List<PlannedProfile>();
        var index = 0;
        foreach (var cluster in clusters.Where(c => c.Count >= Settings.MinTemplatePages).OrderByDescending(c => c.Count).Take(Settings.MaxNewProfilesPerScan))
        {
            ct.ThrowIfCancellationRequested();
            var samples = ChooseSamples(input.Candidates, cluster, clusters, c => c.Eligible && c.ProfileId is null, c => c.Type, first: index++ == 0 && input.StoredProfileIds.Count == 0);
            var outlines = new List<string>();
            foreach (var sample in samples)
            {
                outlines.Add(PageOutline.Build(await ParseAsync(input.SiteKey, sample.Url, ct), Settings.MaxOutlineChars));
            }

            planned.Add(new PlannedProfile(cluster[0].Url, cluster.Count, model.EstimateUsd(outlines)) { SampleUrls = samples.Select(s => s.Url).ToList() });
        }

        var plan = new ProfilePlan(input.SiteKey, false, planned, planned.Count > 0 ? model.UnavailableReason : null);
        logger.LogInformation("Profiles of {Site}: {Stored} stored, {Fit} of {Eligible} pages fit, {Planned} new planned (about {Cost} USD){Reason}",
            input.SiteKey, input.StoredProfileIds.Count, input.Candidates.Count(c => c.ProfileId is not null), input.Candidates.Count(c => c.Eligible),
            plan.Planned.Count, plan.EstimatedUsd, plan.UnavailableReason is null ? "" : $", not created: {plan.UnavailableReason}");
        return plan;
    }

    /// <summary>
    /// Writes the planned profiles (the plan was confirmed): one group at a time, the largest first, and fits every page
    /// without a profile to each new one. The pages are changed in place.
    /// </summary>
    public async Task<ProfileCreateResult> CreateAsync(
        ProfilePlan plan, IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<PageProfile> stored, CancellationToken ct)
    {
        if (!plan.WillCreate)
        {
            return ProfileCreateResult.None;
        }

        var created = new List<PageProfile>();
        var warnings = new List<ScanWarning>();
        var failedLeaders = new HashSet<string>(StringComparer.Ordinal);
        var calls = 0;
        long inputTokens = 0;
        long outputTokens = 0;
        decimal cost = 0;
        while (created.Count < Settings.MaxNewProfilesPerScan)
        {
            var unfitting = Unfitting(pages);
            var clusters = Cluster(unfitting, p => p.StructureTokens);
            var cluster = clusters
                .Where(c => c.Count >= Settings.MinTemplatePages && !failedLeaders.Contains(c[0].Info.Url))
                .OrderByDescending(c => c.Count)
                .FirstOrDefault();
            if (cluster is null)
            {
                break;
            }

            var leader = cluster[0].Info.Url;
            var samples = ChooseSamples(pages, cluster, clusters, p => p.ProfileEligible && p.Fit is null, p => p.Info.Type, first: stored.Count == 0 && created.Count == 0);
            var documents = new List<(ExtractedPageRecord Page, IDocument Document)>();
            foreach (var sample in samples)
            {
                documents.Add((sample, await ParseAsync(plan.SiteKey, sample.Info.Url, ct)));
            }

            var outlines = documents.Select(d => (d.Page.Info.Url, PageOutline.Build(d.Document, Settings.MaxOutlineChars))).ToList();
            ProfileAnswer answer;
            try
            {
                answer = await model.AskAsync(outlines, ct);
            }
            catch (RewriteApiException ex) when (ex.IsFatal)
            {
                warnings.Add(new ScanWarning(EngineCodes.ProfileFailedFatal, NoteParams.Of(("reason", ex.Message))));
                logger.LogWarning("Profile model failed fatally: {Message}", ex.Message);
                break;
            }
            catch (Exception ex) when (ex is RewriteApiException or JsonException)
            {
                failedLeaders.Add(leader);
                warnings.Add(new ScanWarning(EngineCodes.ProfileFailed, NoteParams.Of(("url", leader), ("reason", ex.Message), ("pages", cluster.Count))));
                logger.LogWarning(ex, "Profile for the template of {Url} failed", leader);
                continue;
            }

            calls++;
            inputTokens += answer.InputTokens;
            outputTokens += answer.OutputTokens;
            cost += answer.CostUsd;

            var profile = new PageProfile
            {
                Id = NextId(plan.SiteKey, stored, created),
                Site = plan.SiteKey,
                CreatedAt = DateTimeOffset.UtcNow,
                Model = answer.Model,
                PromptVersion = ProfilePrompt.Version,
                SampleUrls = samples.Select(s => s.Info.Url).ToList(),
                Regions = Validate(answer.Regions, documents),
            };

            var fitting = documents.Count(d => d.Document.Body is { } body
                && ProfileMatcher.UnknownShare(body, profile, ProfileMatcher.KnownByStructure(body, new Uri(d.Page.Info.Url))) <= Settings.MaxUnknownShare);
            if (fitting == 0)
            {
                failedLeaders.Add(leader);
                warnings.Add(new ScanWarning(EngineCodes.ProfileNoFit, NoteParams.Of(("url", leader), ("pages", cluster.Count))));
                logger.LogWarning("Profile for the template of {Url} fits none of its samples", leader);
                continue;
            }

            await store.AddAsync(profile, ct);
            created.Add(profile);
            var used = await RefitAsync(plan.SiteKey, pages, profile, ct);
            logger.LogInformation("Profile {Id}: {Regions} regions ({Rejected} rejected), {Pages} pages use it, {Cost} USD",
                profile.Id, profile.Regions.Count, profile.Regions.Count(r => r.RejectedBecause is not null), used, answer.CostUsd);
        }

        return new ProfileCreateResult(created, calls, inputTokens, outputTokens, cost, warnings);
    }

    /// <summary>Fits every eligible page without a profile to a new profile; returns how many pages use it.</summary>
    public async Task<int> RefitAsync(string siteKey, IReadOnlyList<ExtractedPageRecord> pages, PageProfile profile, CancellationToken ct)
    {
        var used = 0;
        foreach (var page in Unfitting(pages))
        {
            var document = await ParseAsync(siteKey, page.Info.Url, ct);
            if (ProfileFitting.Fit(page, document, [profile], Settings.MaxUnknownShare) is { } blocked)
            {
                used++;
                foreach (var region in blocked)
                {
                    logger.LogInformation("Page {Url}, profile {Id}: skip region not used, {Reason}", page.Info.Url, profile.Id, region);
                }
            }
        }

        return used;
    }

    /// <summary>
    /// Regions as the model returned them, checked on the sample pages: an invalid selector is rejected; a skip region becomes
    /// a check region when its role may not be skipped, or when on a sample page it contains a check region or a longer block
    /// of the main text. The main text counts only on pages read by SmartReader: the fallback heuristic takes everything
    /// outside the frame as main text, cookie bars included. A check region still counts as a known part of the template.
    /// </summary>
    private static List<ProfileRegion> Validate(IReadOnlyList<ProfileRegion> regions, List<(ExtractedPageRecord Page, IDocument Document)> samples)
    {
        var bodies = samples.Where(s => s.Document.Body is not null).Select(s => (s.Page, s.Document, Body: s.Document.Body!)).ToList();
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
            foreach (var (page, document, _) in bodies)
            {
                var application = ProfileMatcher.ApplyNonDestructive(document, page.Content, probe);
                var blocked = application.Blocked.FirstOrDefault(b =>
                    b.Reason == ProfileMatcher.BlockReason.ContainsCheckedRegion || page.Info.Extraction == ExtractionMethod.Readability);
                if (blocked is not null)
                {
                    return Checked(region, $"na vzorové stránce {page.Info.Url} {blocked}");
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

    private static string NextId(string site, IReadOnlyList<PageProfile> stored, IReadOnlyList<PageProfile> created)
    {
        var taken = stored.Concat(created).Select(p => p.Id).ToHashSet();
        var number = taken.Count + 1;
        while (taken.Contains($"{site}#{number}"))
        {
            number++;
        }

        return $"{site}#{number}";
    }

    private static List<ExtractedPageRecord> Unfitting(IReadOnlyList<ExtractedPageRecord> pages) =>
        pages.Where(p => p.ProfileEligible && p.Fit is null).ToList();

    /// <summary>Groups pages of one template: a page joins the first group whose first page is similar enough, otherwise starts a new one.</summary>
    private List<List<T>> Cluster<T>(List<T> pages, Func<T, HashSet<string>> tokens)
    {
        var clusters = new List<List<T>>();
        foreach (var page in pages)
        {
            var cluster = clusters.FirstOrDefault(c => ProfileMatcher.Similarity(tokens(c[0]), tokens(page)) >= Settings.TemplateSimilarity);
            if (cluster is null)
            {
                clusters.Add([page]);
            }
            else
            {
                cluster.Add(page);
            }
        }

        return clusters;
    }

    /// <summary>
    /// The first page of the group and pages spread over the rest of it. The first profile of a site also gets the home
    /// page and the first page of the next group, so that it learns the frame shared by all templates (the probe of
    /// 1. 10. 2026 used home, product and category pages).
    /// </summary>
    private List<T> ChooseSamples<T>(
        IReadOnlyList<T> all, List<T> cluster, List<List<T>> clusters, Func<T, bool> unfitting, Func<T, PageType> type, bool first)
        where T : class
    {
        var count = Math.Max(1, Settings.SamplePages);
        var samples = new List<T> { cluster[0] };
        if (first)
        {
            var others = clusters.Where(c => c != cluster).OrderByDescending(c => c.Count).Select(c => c[0]);
            var home = all.FirstOrDefault(e => unfitting(e) && type(e) == PageType.Home && !cluster.Contains(e));
            foreach (var extra in new[] { home }.Concat(others).OfType<T>().Distinct().Take(count - 1))
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

    private async Task<IDocument> ParseAsync(string siteKey, string url, CancellationToken ct)
    {
        var html = await contents.GetHtmlAsync(new PageContentKey(siteKey, url), ct)
            ?? throw new InvalidOperationException($"HTML of {url} is not in the content store.");
        return new HtmlParser().ParseDocument(PageContent.Decompress(html));
    }
}
