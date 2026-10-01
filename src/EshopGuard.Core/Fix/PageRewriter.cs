using System.Diagnostics;
using System.Text.Json;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Rewrites the problematic passages of scanned pages with a language model and checks the new text again.
/// </summary>
public interface ITextRewriter
{
    /// <summary>Estimates pages, tokens and price before anything is paid.</summary>
    /// <exception cref="InvalidOperationException">The prompt file is missing or invalid.</exception>
    Task<RewriteEstimate> EstimateAsync(RewriteInput input, CancellationToken ct = default);

    /// <summary>
    /// Rewrites every page with a finding of the groups porušení and k posouzení and checks each changed block with the
    /// rules of the tool (the block with its neighbours as context, like in a scan).
    /// </summary>
    /// <exception cref="RewriteApiException">A fatal error of the API (key, quota); other errors fail only one page.</exception>
    /// <exception cref="InvalidOperationException">The prompt file is missing or invalid.</exception>
    Task<RewriteResult> RewriteAsync(RewriteInput input, IProgress<RewriteProgress>? progress = null, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="ITextRewriter"/>: one request per page, the shared part (instructions, examples, legal texts) first
/// and the page last, answers cached by page, findings, model and prompt; the check uses <see cref="IEshopGuard"/>.
/// </summary>
internal sealed class PageRewriter(
    IRewriteClient client,
    IRewriteCache cache,
    IEshopGuard guard,
    IRuleSetProvider rules,
    IOptions<RewriteOptions> options,
    ILogger<PageRewriter> logger) : ITextRewriter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<RewriteEstimate> EstimateAsync(RewriteInput input, CancellationToken ct = default)
    {
        var settings = options.Value;
        var prompt = RewritePrompt.Load(settings.PromptFile);
        var shared = RewritePrompt.SharedPart(prompt);
        var works = BuildWork(input, await TextsAsync(input, ct), out _);
        var toSend = new List<string>();
        var cached = 0;
        foreach (var work in works)
        {
            var page = RewritePrompt.PagePart(work);
            if (await cache.GetAsync(CacheKey(prompt, shared, page), ct) is not null)
            {
                cached++;
            }
            else
            {
                toSend.Add(page);
            }
        }

        // About 3.2 characters per token for the English instructions and 3 for Slovak pages; the shared part is billed
        // in full once and then read from the OpenAI prompt cache.
        var sharedTokens = (long)Math.Ceiling(shared.Length / 3.2);
        var inputTokens = toSend.Count * sharedTokens + toSend.Sum(p => (long)Math.Ceiling(p.Length / 3.0));
        var cachedTokens = Math.Max(0, toSend.Count - 1) * sharedTokens;
        var outputTokens = (long)toSend.Count * settings.EstimatedOutputTokensPerPage;
        var cost = Cost(inputTokens, cachedTokens, outputTokens);
        var isMock = client is MockRewriteClient;
        return new RewriteEstimate
        {
            Pages = works.Count,
            Findings = works.Sum(w => w.Findings.Count),
            CachedPages = cached,
            EstimatedInputTokens = inputTokens,
            EstimatedCachedTokens = cachedTokens,
            EstimatedOutputTokens = outputTokens,
            EstimatedCostUsd = Math.Round(cost, 4),
            Model = isMock ? "mock" : settings.Model,
            IsMock = isMock,
            RequiresConfirmation = !isMock && cost > settings.MaxUsdWithoutConfirm,
        };
    }

    public async Task<RewriteResult> RewriteAsync(RewriteInput input, IProgress<RewriteProgress>? progress = null, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = options.Value;
        var prompt = RewritePrompt.Load(settings.PromptFile);
        var shared = RewritePrompt.SharedPart(prompt);
        var works = BuildWork(input, await TextsAsync(input, ct), out var warnings);
        var pages = new RewritePage[works.Count];
        var usage = new Usage();
        var done = 0;

        async Task RunAsync(int index, CancellationToken token)
        {
            pages[index] = await RewritePageAsync(works[index], prompt, shared, usage, token);
            progress?.Report(new RewriteProgress { Done = Interlocked.Increment(ref done), Total = works.Count });
        }

        if (works.Count > 0)
        {
            // The first page alone writes the shared part into the OpenAI prompt cache for the others.
            await RunAsync(0, ct);
            await Parallel.ForEachAsync(
                Enumerable.Range(1, works.Count - 1),
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, settings.Concurrency), CancellationToken = ct },
                async (index, token) => await RunAsync(index, token));
        }

        var check = await CheckAsync(works, pages, input.ResolvedJurisdictions, ct);
        var model = usage.Model ?? (client is MockRewriteClient ? "mock" : settings.Model);
        logger.LogInformation("Rewrite finished: {Pages} pages, {Cached} from cache, {Errors} errors, {Input} input tokens ({CachedTokens} cached), {Output} output tokens, model {Model}",
            pages.Length, pages.Count(p => p.FromCache), pages.Count(p => p.Error is not null), usage.Input, usage.Cached, usage.Output, model);
        return new RewriteResult
        {
            Pages = pages,
            PromptVersion = prompt.Version,
            Model = model,
            Warnings = warnings,
            Stats = new RewriteStats
            {
                Pages = pages.Length,
                FromCache = pages.Count(p => p.FromCache),
                Errors = pages.Count(p => p.Error is not null),
                InputTokens = usage.Input,
                CachedTokens = usage.Cached,
                OutputTokens = usage.Output,
                ReasoningTokens = usage.Reasoning,
                CostUsd = Math.Round(Cost(usage.Input, usage.Cached, usage.Output), 6),
                CheckCalls = check.Calls,
                CheckCostUsd = check.CostUsd,
                Duration = stopwatch.Elapsed,
            },
        };
    }

    private async Task<RewritePage> RewritePageAsync(RewriteWork work, RewritePromptFile prompt, string shared, Usage usage, CancellationToken ct)
    {
        var pagePart = RewritePrompt.PagePart(work);
        var key = CacheKey(prompt, shared, pagePart);
        string json;
        var fromCache = false;
        if (await cache.GetAsync(key, ct) is { } cached)
        {
            json = cached.Json;
            fromCache = true;
            usage.SetModel(cached.Model);
        }
        else
        {
            RewriteResponse response;
            try
            {
                response = await client.RewriteAsync(new RewriteRequest
                {
                    SharedPart = shared,
                    PagePart = pagePart,
                    PromptVersion = prompt.Version,
                    Schema = RewritePrompt.Schema,
                    Blocks = work.Blocks.Select(b => KeyValuePair.Create(b.Id, b.Text)).ToList(),
                    Findings = work.Findings,
                }, ct);
            }
            catch (RewriteApiException ex) when (!ex.IsFatal)
            {
                logger.LogWarning("Rewrite of {Url} failed: {Message}", work.Page.Url, ex.Message);
                return new RewritePage { Url = work.Page.Url, Findings = work.Findings, Blocks = Apply(work, []), Error = ex.Message };
            }

            usage.Add(response);
            json = response.Json;
        }

        Answer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<Answer>(json, Json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Rewrite of {Url} is not valid JSON", work.Page.Url);
            answer = null;
        }

        if (answer is null)
        {
            return new RewritePage { Url = work.Page.Url, Findings = work.Findings, Blocks = Apply(work, []), Error = "Odpověď modelu nemá očekávaný tvar." };
        }

        if (!fromCache)
        {
            await cache.SetAsync(key, json, usage.Model, ct);
        }

        var blocks = work.Blocks.ToDictionary(b => b.Id, b => b.Text);
        var findingIds = work.Findings.Select(f => f.Id).ToHashSet();
        var changes = new List<RewriteChange>();
        foreach (var change in answer.Changes)
        {
            var ids = change.BlockIds.Where(blocks.ContainsKey).Distinct().ToList();
            if (ids.Count == 0)
            {
                logger.LogWarning("Rewrite of {Url} names unknown blocks {Blocks}, change ignored", work.Page.Url, string.Join(", ", change.BlockIds));
                continue;
            }

            changes.Add(new RewriteChange
            {
                BlockIds = ids,
                Original = string.Join("\n", ids.Select(id => blocks[id])),
                Rewritten = change.Rewritten.Trim(),
                FindingIds = change.FindingIds.Where(findingIds.Contains).Distinct().ToList(),
                Placeholders = change.Placeholders,
                Reason = change.ReasonCs,
            });
        }

        var kept = answer.Kept
            .Where(k => findingIds.Contains(k.FindingId))
            .Select(k => new RewriteKept { FindingId = k.FindingId, Reason = k.ReasonCs })
            .ToList();
        return new RewritePage
        {
            Url = work.Page.Url,
            Findings = work.Findings,
            Blocks = Apply(work, changes),
            Changes = changes,
            Kept = kept,
            FromCache = fromCache,
        };
    }

    /// <summary>The page block by block after the changes: the first block of a change gets the new text, further ones are removed.</summary>
    private static List<RewriteBlock> Apply(RewriteWork work, IReadOnlyList<RewriteChange> changes)
    {
        var rewritten = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            for (var k = 0; k < change.BlockIds.Count; k++)
            {
                rewritten[change.BlockIds[k]] = k == 0 ? change.Rewritten : "";
            }
        }

        return work.Blocks
            .Select(b => new RewriteBlock
            {
                Id = b.Id,
                Original = b.Text,
                Rewritten = rewritten.GetValueOrDefault(b.Id, b.Text),
                Changed = rewritten.ContainsKey(b.Id),
            })
            .ToList();
    }

    /// <summary>
    /// Checks every changed block with the rules of the tool: the new block between its neighbouring blocks (the page
    /// after all its changes), in one evaluation for all pages. Sets the status of each change and finding.
    /// </summary>
    private async Task<(int Calls, decimal CostUsd)> CheckAsync(IReadOnlyList<RewriteWork> works, RewritePage[] pages, IReadOnlyList<string> jurisdictions, CancellationToken ct)
    {
        var inputs = new List<TextInput>();
        var changesByUrl = new Dictionary<string, RewriteChange>();
        for (var i = 0; i < works.Count; i++)
        {
            if (pages[i].Error is not null)
            {
                continue;
            }

            var main = pages[i].Blocks.Where(b => b.IsMainText).Select(b => (b.Id, Text: b.Rewritten)).ToList();
            foreach (var change in pages[i].Changes.Where(c => c.Rewritten.Length > 0))
            {
                var first = change.BlockIds[0];
                var window = new List<string>();
                var position = main.FindIndex(b => b.Id == first);
                if (position >= 0)
                {
                    window.AddRange(main.Take(position).Select(b => b.Text).Where(t => t.Length > 0).TakeLast(1));
                    window.Add(change.Rewritten);
                    window.AddRange(main.Skip(position + 1).Select(b => b.Text).Where(t => t.Length > 0).Take(1));
                }
                else
                {
                    window.Add(change.Rewritten);
                }

                var url = $"{pages[i].Url}#rewrite-{first}";
                changesByUrl[url] = change;
                inputs.Add(new TextInput { Url = url, Text = string.Join("\n", window), Kind = TextKind.Sentence, Category = works[i].Page.Category });
            }
        }

        var calls = 0;
        var cost = 0m;
        if (inputs.Count > 0)
        {
            var modules = works.SelectMany(w => w.Findings).Select(f => f.Finding.Module).Distinct(StringComparer.Ordinal).ToList();
            var analysis = await guard.AnalyzeTextsAsync(inputs, new AnalyzeOptions { Modules = modules, Jurisdictions = jurisdictions }, ct);
            calls = analysis.Stats.JevCalls;
            cost = analysis.Stats.EstimatedCostUsd;
            foreach (var finding in analysis.Findings.Where(f => f.Scope == "segment" && f.Text is not null))
            {
                foreach (var url in finding.Urls)
                {
                    if (!changesByUrl.TryGetValue(url, out var change) || !Normalize(change.Rewritten).Contains(Normalize(finding.Text!), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    (CheckGroup(finding) switch
                    {
                        RewriteCheckGroup.Remaining => change.RemainingFindings,
                        RewriteCheckGroup.Verify => change.VerifyFindings,
                        _ => change.UpcomingFindings,
                    }).Add(finding);
                }
            }
        }

        for (var i = 0; i < pages.Length; i++)
        {
            foreach (var change in pages[i].Changes)
            {
                // A finding in a sentence with a placeholder waits for the shop's facts; one in any other sentence is real.
                var real = change.RemainingFindings.Any(f => !f.Text!.Contains(RewritePrompt.PlaceholderMarker, StringComparison.Ordinal));
                change.Status = change.Rewritten.Length == 0 ? RewriteStatus.Resolved
                    : real ? RewriteStatus.StillFinding
                    : change.Placeholders.Count > 0 || change.Rewritten.Contains(RewritePrompt.PlaceholderMarker, StringComparison.Ordinal) ? RewriteStatus.WaitingForFacts
                    : RewriteStatus.Resolved;
            }

            var kept = pages[i].Kept.Select(k => k.FindingId).ToHashSet();
            foreach (var finding in pages[i].Findings)
            {
                var change = pages[i].Changes.FirstOrDefault(c => c.FindingIds.Contains(finding.Id));
                finding.Status = change is not null ? change.Status
                    : kept.Contains(finding.Id) ? RewriteStatus.Kept
                    : RewriteStatus.NotAddressed;
            }
        }

        return (calls, cost);
    }

    /// <summary>
    /// Where a finding of the check of a new text belongs: a verdict that applies and is decided by the text (in any
    /// jurisdiction) keeps the change open; one that waits for facts goes to the list to verify; a rule that applies only
    /// later is mentioned and changes nothing.
    /// </summary>
    internal static RewriteCheckGroup CheckGroup(Finding finding)
    {
        var current = finding.Verdicts.Where(v => v.Status == VerdictStatus.Finding).ToList();
        return current.Any(v => v.Checkability is "text" or "assess") ? RewriteCheckGroup.Remaining
            : current.Count > 0 ? RewriteCheckGroup.Verify
            : RewriteCheckGroup.Upcoming;
    }

    /// <summary>A finding of a sentence or paragraph in the groups porušení and k posouzení: its text is rewritten.</summary>
    internal static bool IsRewritable(Finding f) => f.Scope == "segment" && (f.Checkability is "text" or "assess") && !string.IsNullOrWhiteSpace(f.Text);

    /// <summary>The texts of the findings in the language of the content of the shop.</summary>
    private async Task<(RuleTextRenderer Renderer, string Locale)> TextsAsync(RewriteInput input, CancellationToken ct)
    {
        var catalog = await rules.LoadAsync(ct);
        var locale = input.ContentLanguage ?? catalog.Jurisdictions.LawLanguage(input.ResolvedJurisdictions[0]) ?? "cs";
        return (new RuleTextRenderer(catalog), locale);
    }

    /// <summary>
    /// Pages with findings of the groups porušení and k posouzení. A finding with the same text on several pages is
    /// rewritten once, on the first page of the scan that is in the input; the others are listed.
    /// </summary>
    private static List<RewriteWork> BuildWork(RewriteInput input, (RuleTextRenderer Renderer, string Locale) texts, out List<string> warnings)
    {
        warnings = [];
        var pages = input.Pages.GroupBy(p => p.Url, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var byPage = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
        var withoutPage = 0;
        foreach (var finding in input.Findings.Where(IsRewritable))
        {
            var url = finding.Urls.FirstOrDefault(pages.ContainsKey);
            if (url is null)
            {
                withoutPage++;
                continue;
            }

            if (!byPage.TryGetValue(url, out var list))
            {
                byPage[url] = list = [];
            }

            list.Add(finding);
        }

        if (withoutPage > 0)
        {
            warnings.Add($"{withoutPage} nálezů je na stránkách, jejichž text sken neuložil; ty se nepřepisují.");
        }

        var works = new List<RewriteWork>();
        foreach (var url in byPage.Keys.Order(StringComparer.Ordinal).Take(input.MaxPages ?? int.MaxValue))
        {
            works.Add(Work(pages[url], byPage[url], texts));
        }

        return works;
    }

    private static RewriteWork Work(RewritePageInput page, List<Finding> findings, (RuleTextRenderer Renderer, string Locale) texts)
    {
        var blocks = page.MainText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((text, i) => (Id: $"B{i + 1}", Text: text))
            .ToList();
        var located = new List<(Finding Finding, List<string> Blocks, int Order)>();
        foreach (var finding in findings)
        {
            var text = Normalize(finding.Text!);
            var ids = blocks.Where(b => Normalize(b.Text).Contains(text, StringComparison.Ordinal)).Select(b => b.Id).ToList();
            if (ids.Count == 0)
            {
                ids = [ExtraBlock(page, finding, blocks)];
            }

            var order = blocks.FindIndex(b => b.Id == ids[0]);
            located.Add((finding, ids, order < 0 ? int.MaxValue : order));
        }

        var rewriteFindings = located
            .OrderBy(l => l.Order)
            .Select((l, i) => new RewriteFinding
            {
                Id = $"F{i + 1}",
                Finding = l.Finding,
                Texts = VerdictOrder.Sort(l.Finding.Verdicts).Select(v =>
                {
                    var rendered = texts.Renderer.Render(l.Finding, v, texts.Locale);
                    return new RewriteFindingTexts
                    {
                        Jurisdiction = v.Jurisdiction, Checkability = v.Checkability, Title = rendered.Title,
                        Explanation = rendered.Explanation, Recommendation = rendered.Recommendation,
                    };
                }).ToList(),
                Blocks = l.Blocks,
                AlsoOn = l.Finding.Urls.Where(u => u != page.Url).ToList(),
            })
            .ToList();
        return new RewriteWork { Page = page, Blocks = blocks, Findings = rewriteFindings };
    }

    /// <summary>A block for a finding outside the main text: the title, meta description, JSON-LD or page frame.</summary>
    private static string ExtraBlock(RewritePageInput page, Finding finding, List<(string Id, string Text)> blocks)
    {
        var (label, source) = finding.Sources.Contains(SegmentSource.Title) ? ("TITLE", page.Title)
            : finding.Sources.Contains(SegmentSource.MetaDescription) ? ("META", page.MetaDescription)
            : finding.Sources.Contains(SegmentSource.JsonLd) ? ("JSONLD", page.JsonLdDescription)
            : finding.Sources.Contains(SegmentSource.Chrome) ? ("CHROME", null)
            : ("TEXT", null);
        var text = source is not null && Normalize(source).Contains(Normalize(finding.Text!), StringComparison.Ordinal) ? source.Trim() : finding.Text!;
        var existing = blocks.FirstOrDefault(b => b.Id.StartsWith(label, StringComparison.Ordinal) && Normalize(b.Text).Contains(Normalize(finding.Text!), StringComparison.Ordinal));
        if (existing.Id is not null)
        {
            return existing.Id;
        }

        var id = label;
        for (var n = 2; blocks.Any(b => b.Id == id); n++)
        {
            id = label + n;
        }

        blocks.Add((id, text));
        return id;
    }

    private static string Normalize(string text) => TextTools.Clean(text).ToLowerInvariant();

    private string CacheKey(RewritePromptFile prompt, string shared, string pagePart) =>
        TextTools.Sha256(string.Join("\n", options.Value.Model, options.Value.ReasoningEffort, prompt.Version, TextTools.Sha256(shared), pagePart));

    private decimal Cost(long input, long cached, long output)
    {
        var settings = options.Value;
        return ((input - cached) * settings.InputUsdPerMillion + cached * settings.CachedInputUsdPerMillion + output * settings.OutputUsdPerMillion) / 1_000_000m;
    }

    private sealed class Usage
    {
        private long _input;
        private long _cached;
        private long _output;
        private long _reasoning;

        public long Input => Interlocked.Read(ref _input);

        public long Cached => Interlocked.Read(ref _cached);

        public long Output => Interlocked.Read(ref _output);

        public long Reasoning => Interlocked.Read(ref _reasoning);

        public string? Model { get; private set; }

        public void Add(RewriteResponse response)
        {
            Interlocked.Add(ref _input, response.InputTokens);
            Interlocked.Add(ref _cached, response.CachedTokens);
            Interlocked.Add(ref _output, response.OutputTokens);
            Interlocked.Add(ref _reasoning, response.ReasoningTokens);
            SetModel(response.Model);
        }

        public void SetModel(string? model)
        {
            if (model is not null)
            {
                Model ??= model;
            }
        }
    }

    private sealed class Answer
    {
        public List<AnswerChange> Changes { get; set; } = [];

        public List<AnswerKept> Kept { get; set; } = [];
    }

    private sealed class AnswerChange
    {
        public List<string> BlockIds { get; set; } = [];

        public string Original { get; set; } = "";

        public string Rewritten { get; set; } = "";

        public List<string> FindingIds { get; set; } = [];

        public List<string> Placeholders { get; set; } = [];

        public string ReasonCs { get; set; } = "";
    }

    private sealed class AnswerKept
    {
        public string FindingId { get; set; } = "";

        public string ReasonCs { get; set; } = "";
    }
}

/// <summary>Where a finding of the check of a rewritten passage belongs.</summary>
internal enum RewriteCheckGroup
{
    /// <summary>A violation or a passage to assess remains in some jurisdiction.</summary>
    Remaining,

    /// <summary>Only facts outside the web decide (a named label, a claim to prove).</summary>
    Verify,

    /// <summary>The rule applies only later.</summary>
    Upcoming,
}
