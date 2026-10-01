using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Everything the rule engine needs. The engine does no I/O, so it is tested with fixed probabilities.
/// </summary>
internal sealed class RuleEngineInput
{
    /// <summary>Rule sets selected for the run (enabled, requested module, at least one chosen jurisdiction).</summary>
    public required IReadOnlyList<RuleSet> RuleSets { get; init; }

    public required LabelConfiguration Labels { get; init; }

    /// <summary>Segments with probabilities filled by the evaluation, keyed by <see cref="QuestionKey"/>.</summary>
    public required IReadOnlyList<Segment> Segments { get; init; }

    /// <summary>Full text of every page (including image alt texts and file names) for page-level allowlists.</summary>
    public IReadOnlyDictionary<string, string> PageTexts { get; init; } = new Dictionary<string, string>();

    /// <summary>What a customer can see on every page, split by source, for site_signal rules (scan only).</summary>
    public IReadOnlyDictionary<string, PageSignals> PageSignals { get; init; } = new Dictionary<string, PageSignals>();

    /// <summary>False for check-text: site_signal rules need the whole site and are skipped.</summary>
    public bool EvaluateSiteSignals { get; init; }

    /// <summary>Legal requirements for all products of a category (SK point 15), for <c>claim_list_match</c>.</summary>
    public LegalRequirementList LegalRequirements { get; init; } = LegalRequirementList.Empty;

    /// <summary>Product category text of every page (title, h1, breadcrumb, shop category) for <c>claim_list_match</c>.</summary>
    public IReadOnlyDictionary<string, string> PageCategories { get; init; } = new Dictionary<string, string>();

    /// <summary>Jurisdictions of the run; every rule set is evaluated for those of them it has.</summary>
    public required IReadOnlyList<string> Jurisdictions { get; init; }

    /// <summary>Date of the evaluation; a rule whose <c>effective_from</c> is later gives an upcoming verdict. Null: every rule applies.</summary>
    public DateOnly? AsOf { get; init; }

    /// <summary>False when no legal texts were supplied (check-text of a sentence); site rules are skipped.</summary>
    public bool EvaluateSitePresence { get; init; } = true;

    /// <summary>Legal documents that were not read; site findings then get a note and at most the review band.</summary>
    public IReadOnlyList<UncheckedDocument> UncheckedDocuments { get; init; } = [];

    /// <summary>
    /// Pages whose text was not in the HTML (rendered by JavaScript). Site findings that a page could contradict get a note,
    /// presence findings with such a legal page at most the review band.
    /// </summary>
    public IReadOnlyList<PageInfo> TextNotLoadedPages { get; init; } = [];

    /// <summary>Adds the built-in finding "legal pages missing".</summary>
    public bool AddMissingLegalPagesFinding { get; init; }
}

/// <summary>
/// Text, link texts with addresses and image alt texts with addresses of one page, one item per line.
/// </summary>
internal sealed record PageSignals(string Text, string Links, string Images)
{
    public string For(string? where) => where switch
    {
        "text" => Text,
        "links" => Links,
        "images" => Images,
        _ => string.Join("\n", Text, Links, Images),
    };
}

internal sealed class RuleEngineOutput
{
    public List<Finding> Findings { get; } = [];

    public List<RuleResult> RuleResults { get; } = [];

    public List<SiteObligation> SiteObligations { get; } = [];
}

/// <summary>
/// Combines question probabilities into findings according to the rule sets, for every chosen jurisdiction of every set. A
/// rule that finds the same text (or, for the whole site, the same missing information) in several jurisdictions gives one
/// finding with a verdict per jurisdiction. No legal logic is hard-coded here, except the built-in finding for a site without
/// legal pages required by the specification. Notes are codes with parameters; the sentences are composed by the host.
/// </summary>
internal static class RuleEngine
{
    public const string MissingLegalPagesRuleId = "legal_pages_missing";

    /// <summary>Most URLs listed in a note.</summary>
    private const int MaxUrlsInNote = 5;

    public static RuleEngineOutput Evaluate(RuleEngineInput input)
    {
        var output = new RuleEngineOutput();
        var partials = new List<Finding>();
        var matcher = new LabelMatcher(input.Labels);
        foreach (var set in input.RuleSets)
        {
            var jurisdictions = JurisdictionsOf(set, input);
            var kind = set.AppliesTo == RuleValidator.Sentence ? SegmentKind.Sentence : SegmentKind.LegalParagraph;
            var segments = input.Segments.Where(s => s.Kind == kind).ToList();
            foreach (var rule in set.Rules)
            {
                foreach (var jurisdiction in jurisdictions)
                {
                    var context = new RuleContext(set, rule, jurisdiction, input);
                    if (rule.Scope == RuleValidator.SegmentScope)
                    {
                        EvaluateSegmentRule(context, segments, matcher, output, partials);
                    }
                    else if (rule.Scope == RuleValidator.SiteSignalScope)
                    {
                        if (input.EvaluateSiteSignals)
                        {
                            EvaluateSiteSignal(context, output, partials);
                        }
                        else
                        {
                            output.SiteObligations.Add(context.Obligation(ObligationStatus.NotChecked, reason: EngineCodes.SiteSignalsNotEvaluated));
                        }
                    }
                    else if (input.EvaluateSitePresence)
                    {
                        EvaluateSitePresence(context, segments, output, partials);
                    }
                    else
                    {
                        output.SiteObligations.Add(context.Obligation(ObligationStatus.NotChecked, reason: EngineCodes.LegalTextsNotGiven));
                    }
                }
            }
        }

        output.Findings.AddRange(MergeJurisdictions(partials));
        MergeRepeatedTexts(output);
        MergeSameTexts(output);

        if (input.AddMissingLegalPagesFinding)
        {
            var finding = MissingLegalPages(input);
            output.Findings.Add(finding);
            foreach (var verdict in finding.Verdicts)
            {
                output.RuleResults.Add(new RuleResult
                {
                    RuleId = MissingLegalPagesRuleId, Outcome = RuleOutcome.Finding, Score = 1, Jurisdiction = verdict.Jurisdiction, RuleSet = verdict.RuleSet,
                });
            }
        }

        return output;
    }

    /// <summary>Obligations of the whole site that were not checked at all, e.g. when the host did not confirm the evaluation.</summary>
    public static List<SiteObligation> NotChecked(IReadOnlyList<RuleSet> ruleSets, IReadOnlyList<string> jurisdictions, string reason) =>
        (from set in ruleSets
         from rule in set.Rules
         where rule.Scope != RuleValidator.SegmentScope
         from jurisdiction in set.Jurisdictions.Where(jurisdictions.Contains).Order(StringComparer.Ordinal)
         select new RuleContext(set, rule, jurisdiction, null).Obligation(ObligationStatus.NotChecked, reason: reason))
        .ToList();

    private static List<string> JurisdictionsOf(RuleSet set, RuleEngineInput input) =>
        set.Jurisdictions.Where(input.Jurisdictions.Contains).Order(StringComparer.Ordinal).ToList();

    private static void EvaluateSegmentRule(
        RuleContext context, List<Segment> segments, LabelMatcher matcher, RuleEngineOutput output, List<Finding> partials)
    {
        var (set, rule, jurisdiction, input) = (context.Set, context.Rule, context.Jurisdiction, context.Input!);
        var logic = rule.Logic!;
        var questions = logic.All.Concat(logic.Any).Concat(logic.None).Select(c => c.Q).Distinct().ToList();
        foreach (var segment in segments)
        {
            if (segment.SkippedModules.Contains(set.Module))
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.SkippedBySieve));
                continue;
            }

            if (!questions.All(q => segment.Probabilities.ContainsKey(QuestionKey.Of(set, q))))
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.NotEvaluated));
                continue;
            }

            double P(RuleCondition condition) => segment.Probabilities[QuestionKey.Of(set, condition.Q)];

            var parts = new List<double>();
            if (logic.All.Count > 0)
            {
                parts.Add(logic.All.Min(P));
            }

            if (logic.Any.Count > 0)
            {
                parts.Add(logic.Any.Max(P));
            }

            parts.AddRange(logic.None.Select(c => 1 - P(c)));
            var score = parts.Min();

            var conditionsHold = logic.All.All(c => P(c) >= c.Gte)
                && (logic.Any.Count == 0 || logic.Any.Any(c => P(c) >= c.Gte))
                && logic.None.All(c => P(c) < c.Gte);
            if (!conditionsHold)
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.ConditionsNotMet, score));
                continue;
            }

            var band = SegmentBand(score, rule.BandsFor(jurisdiction));
            if (band is null)
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.BelowThreshold, score));
                continue;
            }

            var urls = segment.Urls.ToList();
            foreach (var check in rule.CodeChecks.Where(c => c.Type == RuleValidator.AllowlistAbsent))
            {
                if (check.Where == "page")
                {
                    urls = urls.Where(url => !matcher.ContainsAny(input.PageTexts.GetValueOrDefault(url, segment.Text), check.List!)).ToList();
                }
                else if (matcher.ContainsAny(segment.Text, check.List!))
                {
                    urls.Clear();
                }
            }

            if (urls.Count == 0)
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.ExcludedByAllowlist, score));
                continue;
            }

            var notes = new List<FindingNote>();
            foreach (var check in rule.CodeChecks.Where(c => c.Type == RuleValidator.ClaimListMatch))
            {
                urls = urls.Where(url => ClaimListHolds(check, segment.Text, input.PageCategories.GetValueOrDefault(url, ""), jurisdiction, input, notes)).ToList();
            }

            if (urls.Count == 0)
            {
                output.RuleResults.Add(context.Result(segment, RuleOutcome.NotInList, score));
                continue;
            }

            if (rule.CodeChecks.Any(c => c.Type == RuleValidator.LabelNotes))
            {
                notes.AddRange(matcher.NotesFor(segment.Text).Select(id => new FindingNote(EngineCodes.LabelNote, NoteParams.Of(("label_id", id)))));
            }

            var verdict = context.Verdict(band.Value, score, notes.Distinct().ToList(), questions.ToDictionary(q => q, q => segment.Probabilities[QuestionKey.Of(set, q)]));
            output.RuleResults.Add(context.Result(segment, RuleOutcome.Finding, score, verdict.Band));
            partials.Add(new Finding
            {
                RuleId = rule.Id,
                Module = set.Module,
                Scope = "segment",
                Text = segment.Text,
                ContextBefore = segment.ContextBefore,
                ContextAfter = segment.ContextAfter,
                Sources = segment.Sources,
                Urls = urls,
                Boilerplate = segment.Boilerplate,
                SegmentHash = segment.Hash,
                TextFingerprint = segment.Fingerprint,
                Verdicts = [verdict],
            });
        }
    }

    /// <summary>
    /// The sentence makes the claim of a list item whose category matches the page category (never the sentence itself);
    /// <c>outcomes</c> filters the items, <c>absent</c> turns the check around. Notes name the items, so the finding says
    /// which requirement and legal basis it rests on, or why the list did not decide.
    /// </summary>
    private static bool ClaimListHolds(CodeCheck check, string text, string category, string jurisdiction, RuleEngineInput input, List<FindingNote> notes)
    {
        var claimed = input.LegalRequirements.For(jurisdiction).Where(r => r.MatchesClaim(text)).ToList();
        var inCategory = claimed.Where(r => r.MatchesCategory(category)).ToList();
        var wanted = check.Outcomes is { Count: > 0 } outcomes ? inCategory.Where(r => outcomes.Contains(r.Outcome)).ToList() : inCategory;
        if (!check.Absent)
        {
            notes.AddRange(wanted.Select(Describe));
            return wanted.Count > 0;
        }

        if (wanted.Count > 0)
        {
            return false;
        }

        var shown = category.Length > 150 ? category[..150] + "…" : category;
        var others = claimed.Except(inCategory).Select(r => r.CategoryName).Distinct().ToList();
        notes.Add(others.Count > 0
            ? new FindingNote(EngineCodes.ClaimListOtherCategory, NoteParams.Of(("categories", string.Join("; ", others)), ("category", shown)))
            : category.Length == 0
                ? new FindingNote(EngineCodes.ClaimListNoCategory, NoteParams.None)
                : new FindingNote(EngineCodes.ClaimListNotInList, NoteParams.Of(("category", shown))));
        return true;
    }

    private static FindingNote Describe(LegalRequirement requirement) =>
        new(EngineCodes.ClaimListItem, NoteParams.Of(
            ("category", requirement.CategoryName),
            ("feature", requirement.Feature),
            ("basis", requirement.Basis),
            ("since", requirement.Since is { Length: > 0 } since ? new FindingNote(EngineCodes.ClaimListSince, NoteParams.Of(("since", since))) : null),
            ("remark", requirement.Note is { Length: > 0 } remark ? new FindingNote(EngineCodes.ClaimListRemark, NoteParams.Of(("remark", remark))) : null)));

    private static void EvaluateSitePresence(RuleContext context, List<Segment> paragraphs, RuleEngineOutput output, List<Finding> partials)
    {
        var (set, rule, input) = (context.Set, context.Rule, context.Input!);
        var question = QuestionKey.Of(set, rule.Question!);
        var evaluated = paragraphs.Where(p => p.Probabilities.ContainsKey(question)).ToList();
        var best = evaluated.MaxBy(p => p.Probabilities[question]);
        var max = best?.Probabilities[question] ?? 0;
        var present = max >= set.PresenceThreshold;
        var bands = rule.BandsFor(context.Jurisdiction);

        var legalText = string.Join("\n", paragraphs.Select(p => p.Text));
        var missingPatterns = rule.CodeChecks
            .Where(c => c.Type == RuleValidator.RegexRequired && !IsMatch(legalText, c.Pattern!))
            .Select(c => c.Pattern!)
            .ToList();

        var notes = new List<FindingNote>();
        double score;
        FindingBand band;
        if (!present)
        {
            // A missing piece of information is never ignored: below the high band it is still "to review".
            score = 1 - max;
            band = score >= bands.High ? FindingBand.High : FindingBand.Review;
            if (best is not null)
            {
                notes.Add(new FindingNote(EngineCodes.PresenceClosestParagraph, NoteParams.Of(("probability", max), ("threshold", set.PresenceThreshold))));
            }

            notes.AddRange(missingPatterns.Select(p => new FindingNote(EngineCodes.PresencePatternMissing, NoteParams.Of(("pattern", p)))));
        }
        else if (missingPatterns.Count > 0)
        {
            score = bands.Review;
            band = FindingBand.Review;
            notes.AddRange(missingPatterns.Select(p => new FindingNote(EngineCodes.PresenceFoundButPatternMissing, NoteParams.Of(("probability", max), ("pattern", p)))));
        }
        else
        {
            output.RuleResults.Add(context.SiteResult(best?.Hash, RuleOutcome.Present, max));
            output.SiteObligations.Add(context.Obligation(ObligationStatus.Met, best?.Urls ?? []));
            return;
        }

        var notEvaluated = paragraphs.Count - evaluated.Count;
        if (notEvaluated > 0)
        {
            band = FindingBand.Review;
            notes.Add(new FindingNote(EngineCodes.PresenceParagraphsNotEvaluated, NoteParams.Of(("count", notEvaluated))));
        }

        if (input.UncheckedDocuments.Count > 0)
        {
            band = FindingBand.Review;
            notes.Add(PdfNote(input.UncheckedDocuments));
        }

        var notLoadedLegal = input.TextNotLoadedPages.Where(p => p.Type == PageType.Legal).ToList();
        if (notLoadedLegal.Count > 0)
        {
            band = FindingBand.Review;
            notes.Add(UrlsNote(EngineCodes.LegalPagesNotLoaded, notLoadedLegal.Select(p => p.Url).ToList()));
        }

        var verdict = context.Verdict(band, score, notes, new Dictionary<string, double> { [rule.Question!] = max });
        output.RuleResults.Add(context.SiteResult(best?.Hash, RuleOutcome.Finding, score, verdict.Band));
        output.SiteObligations.Add(context.Obligation(verdict.Status == VerdictStatus.Upcoming ? ObligationStatus.Upcoming : ObligationStatus.Missing, best?.Urls ?? []));
        partials.Add(new Finding
        {
            RuleId = rule.Id,
            Module = set.Module,
            Scope = "site",
            Text = best?.Text,
            Sources = best?.Sources ?? [],
            Urls = best?.Urls ?? [],
            SegmentHash = best?.Hash,
            TextFingerprint = best?.Fingerprint,
            Verdicts = [verdict],
        });
    }

    /// <summary>
    /// A sign on the whole site: required (a finding when no page has it) or forbidden (a finding listing the pages with it).
    /// Pages the crawler never downloads (cart, checkout, customer account) are not seen, so a finding is always to review.
    /// </summary>
    private static void EvaluateSiteSignal(RuleContext context, RuleEngineOutput output, List<Finding> partials)
    {
        var (set, rule, input) = (context.Set, context.Rule, context.Input!);
        var required = rule.CodeChecks[0].Type == RuleValidator.SitePatternRequired;
        var matches = input.PageSignals
            .Where(page => rule.CodeChecks.Any(c => IsMatch(page.Value.For(c.Where), c.Pattern!)))
            .Select(page => page.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (required ? matches.Count > 0 : matches.Count == 0)
        {
            output.RuleResults.Add(context.SiteResult(null, RuleOutcome.Present, 1));
            output.SiteObligations.Add(context.Obligation(ObligationStatus.Met, matches));
            return;
        }

        var notes = new List<FindingNote>();
        if (required)
        {
            notes.Add(new FindingNote(EngineCodes.SignalNotFound, NoteParams.Of(("count", input.PageSignals.Count))));
            notes.Add(new FindingNote(EngineCodes.SignalCartNotDownloaded, NoteParams.None));
            if (input.TextNotLoadedPages.Count > 0)
            {
                notes.Add(UrlsNote(EngineCodes.PagesNotLoaded, input.TextNotLoadedPages.Select(p => p.Url).ToList()));
            }
        }
        else
        {
            notes.Add(new FindingNote(EngineCodes.SignalFoundOn, NoteParams.Of(("count", matches.Count), ("total", input.PageSignals.Count))));
        }

        var verdict = context.Verdict(FindingBand.Review, 1, notes, new Dictionary<string, double>());
        output.RuleResults.Add(context.SiteResult(null, RuleOutcome.Finding, 1, verdict.Band));
        output.SiteObligations.Add(context.Obligation(verdict.Status == VerdictStatus.Upcoming ? ObligationStatus.Upcoming : ObligationStatus.Missing, matches));
        partials.Add(new Finding
        {
            RuleId = rule.Id,
            Module = set.Module,
            Scope = "site",
            Urls = matches,
            Verdicts = [verdict],
        });
    }

    /// <summary>
    /// Findings of the same rule for the same segment (or, for the whole site, of the same rule) from several jurisdictions are
    /// one finding with their verdicts; the text, pages and paragraph come from the strictest verdict. With one jurisdiction
    /// nothing changes.
    /// </summary>
    private static List<Finding> MergeJurisdictions(List<Finding> partials)
    {
        var groups = new List<List<Finding>>();
        var index = new Dictionary<(string RuleId, string? SegmentHash), List<Finding>>();
        foreach (var partial in partials)
        {
            var key = (partial.RuleId, partial.Scope == "site" ? null : partial.SegmentHash);
            if (!index.TryGetValue(key, out var group))
            {
                group = [];
                index[key] = group;
                groups.Add(group);
            }

            group.Add(partial);
        }

        return groups.Select(group =>
        {
            if (group.Count == 1)
            {
                return group[0];
            }

            var strictest = group.OrderBy(f => f.Verdicts[0], Comparer<JurisdictionVerdict>.Create(VerdictOrder.Compare)).First();
            return With(strictest, VerdictOrder.Sort(group.SelectMany(f => f.Verdicts)), group.SelectMany(f => f.Urls).Distinct().ToList(), strictest.Sources);
        }).ToList();
    }

    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    /// <summary>
    /// Patterns from rule files run on whole pages. The linear-time engine is used when the pattern allows it; a pattern that
    /// cannot be evaluated in time counts as not found, which for required information means a finding to review.
    /// </summary>
    private static bool IsMatch(string input, string pattern)
    {
        var regex = Patterns.GetOrAdd(pattern, p =>
        {
            try
            {
                return new Regex(p, RegexOptions.NonBacktracking, TimeSpan.FromSeconds(5));
            }
            catch (NotSupportedException)
            {
                return new Regex(p, RegexOptions.None, TimeSpan.FromSeconds(5));
            }
        });
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static readonly SegmentSource[] SecondarySources = [SegmentSource.Title, SegmentSource.MetaDescription, SegmentSource.JsonLd];

    /// <summary>
    /// A title or meta description usually repeats the product name or the first sentence of the page. When it adds at most
    /// a short tail (such as the shop name), it is the same claim, so only the finding from the page text is kept; it must
    /// have a verdict in every jurisdiction of the repeated one.
    /// </summary>
    private static void MergeRepeatedTexts(RuleEngineOutput output)
    {
        const int maxExtraChars = 40;
        var duplicates = new List<Finding>();
        foreach (var group in output.Findings.Where(f => f.Scope == "segment" && f.Text is not null).GroupBy(f => f.RuleId))
        {
            var findings = group.ToList();
            foreach (var candidate in findings.Where(f => f.Sources.All(s => SecondarySources.Contains(s))))
            {
                var text = TextTools.NormalizeForHash(candidate.Text!);
                var kept = findings.FirstOrDefault(other =>
                    !ReferenceEquals(other, candidate)
                    && !duplicates.Contains(other)
                    && other.Sources.Any(s => !SecondarySources.Contains(s))
                    && other.Urls.Intersect(candidate.Urls).Any()
                    && candidate.Verdicts.All(v => other.Verdicts.Any(o => o.Jurisdiction == v.Jurisdiction))
                    && TextTools.NormalizeForHash(other.Text!) is { Length: > 0 } shorter
                    && text.Contains(shorter, StringComparison.Ordinal)
                    && text.Length - shorter.Length <= maxExtraChars);
                if (kept is not null)
                {
                    duplicates.Add(candidate);
                }
            }
        }

        foreach (var duplicate in duplicates)
        {
            output.Findings.Remove(duplicate);
            MarkDuplicate(output, duplicate);
        }
    }

    /// <summary>
    /// The same text (for example a badge "Vegan" next to many products) found by the same rule in different contexts is
    /// one finding listing all its pages; the one with the highest score keeps its context and probabilities. In every
    /// jurisdiction the verdict with the highest score is kept, with the notes of all.
    /// </summary>
    private static void MergeSameTexts(RuleEngineOutput output)
    {
        var groups = output.Findings
            .Where(f => f.Scope == "segment" && f.Text is not null)
            .GroupBy(f => (f.RuleId, Text: TextTools.NormalizeForHash(f.Text!)))
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var group in groups)
        {
            var findings = group.OrderByDescending(f => f.Score).ToList();
            var kept = findings[0];
            var jurisdictions = findings.SelectMany(f => f.Verdicts.Select(v => v.Jurisdiction)).Distinct().ToList();
            var verdicts = jurisdictions.Select(jurisdiction =>
            {
                var own = findings.Select(f => f.Verdicts.FirstOrDefault(v => v.Jurisdiction == jurisdiction)).OfType<JurisdictionVerdict>().ToList();
                var best = own.OrderByDescending(v => v.Score).First();
                return WithNotes(best, own.SelectMany(v => v.Notes).Distinct().ToList());
            });
            var index = output.Findings.IndexOf(kept);
            output.Findings[index] = With(kept, VerdictOrder.Sort(verdicts), findings.SelectMany(f => f.Urls).Distinct().ToList(),
                findings.SelectMany(f => f.Sources).Distinct().ToList());
            foreach (var other in findings.Skip(1))
            {
                output.Findings.Remove(other);
                MarkDuplicate(output, other);
            }
        }
    }

    private static void MarkDuplicate(RuleEngineOutput output, Finding duplicate)
    {
        for (var i = 0; i < output.RuleResults.Count; i++)
        {
            var old = output.RuleResults[i];
            if (old.RuleId == duplicate.RuleId && old.SegmentHash == duplicate.SegmentHash && old.Outcome == RuleOutcome.Finding)
            {
                output.RuleResults[i] = new RuleResult
                {
                    RuleId = old.RuleId, SegmentHash = old.SegmentHash, Outcome = RuleOutcome.Duplicate, Score = old.Score, Jurisdiction = old.Jurisdiction, RuleSet = old.RuleSet,
                };
            }
        }
    }

    private static Finding With(Finding finding, IReadOnlyList<JurisdictionVerdict> verdicts, IReadOnlyList<string> urls, IReadOnlyList<SegmentSource> sources) => new()
    {
        RuleId = finding.RuleId,
        Module = finding.Module,
        Scope = finding.Scope,
        Text = finding.Text,
        ContextBefore = finding.ContextBefore,
        ContextAfter = finding.ContextAfter,
        Sources = sources,
        Urls = urls,
        Boilerplate = finding.Boilerplate,
        SegmentHash = finding.SegmentHash,
        TextFingerprint = finding.TextFingerprint,
        Verdicts = verdicts,
        Params = finding.Params,
    };

    private static JurisdictionVerdict WithNotes(JurisdictionVerdict verdict, IReadOnlyList<FindingNote> notes) => new()
    {
        Jurisdiction = verdict.Jurisdiction,
        Status = verdict.Status,
        Band = verdict.Band,
        Score = verdict.Score,
        Severity = verdict.Severity,
        Checkability = verdict.Checkability,
        LegalRefs = verdict.LegalRefs,
        RuleSet = verdict.RuleSet,
        RuleSetVersion = verdict.RuleSetVersion,
        ExplanationVariant = verdict.ExplanationVariant,
        EffectiveFrom = verdict.EffectiveFrom,
        Notes = notes,
        QuestionProbs = verdict.QuestionProbs,
    };

    private static Finding MissingLegalPages(RuleEngineInput input)
    {
        var notes = new List<FindingNote>();
        if (input.UncheckedDocuments.Count > 0)
        {
            notes.Add(PdfNote(input.UncheckedDocuments));
        }

        var jurisdictions = input.RuleSets.Where(s => s.Module == "legal").SelectMany(s => s.Jurisdictions)
            .Where(input.Jurisdictions.Contains).Distinct().Order(StringComparer.Ordinal).ToList();
        if (jurisdictions.Count == 0)
        {
            jurisdictions = input.Jurisdictions.ToList();
        }

        return new Finding
        {
            RuleId = MissingLegalPagesRuleId,
            Module = "legal",
            Scope = "site",
            Verdicts = jurisdictions.Select(jurisdiction => new JurisdictionVerdict
            {
                Jurisdiction = jurisdiction,
                Band = input.UncheckedDocuments.Count > 0 ? FindingBand.Review : FindingBand.High,
                Score = 1,
                Severity = "high",
                Checkability = "text",
                RuleSet = Texts.RuleTextRenderer.BuiltInRuleSet,
                RuleSetVersion = "builtin",
                Notes = notes,
            }).ToList(),
        };
    }

    private static FindingNote PdfNote(IReadOnlyList<UncheckedDocument> documents) =>
        UrlsNote(EngineCodes.UnreadPdfDocuments, documents.Select(d => d.Url).ToList());

    /// <summary>A note listing at most five URLs and how many more there are.</summary>
    private static FindingNote UrlsNote(string code, IReadOnlyList<string> urls) =>
        new(code, NoteParams.Of(
            ("urls", urls.Take(MaxUrlsInNote).ToList()),
            ("more", urls.Count > MaxUrlsInNote ? new FindingNote(EngineCodes.ListMore, NoteParams.Of(("count", urls.Count - MaxUrlsInNote))) : null)));

    private static FindingBand? SegmentBand(double score, Bands bands) =>
        score >= bands.High ? FindingBand.High : score >= bands.Review ? FindingBand.Review : null;

    /// <summary>One rule of one set evaluated for one jurisdiction.</summary>
    private sealed record RuleContext(RuleSet Set, RuleDefinition Rule, string Jurisdiction, RuleEngineInput? Input)
    {
        private DateOnly? EffectiveFrom => Rule.EffectiveFromFor(Jurisdiction);

        private bool Upcoming => EffectiveFrom is { } from && Input?.AsOf is { } asOf && asOf < from;

        /// <summary>
        /// The verdict of the rule in the jurisdiction: before the rule takes effect it is upcoming, in the review band and with
        /// a note of the date.
        /// </summary>
        public JurisdictionVerdict Verdict(FindingBand band, double score, IReadOnlyList<FindingNote> notes, IReadOnlyDictionary<string, double> probabilities)
        {
            var upcoming = Upcoming;
            return new JurisdictionVerdict
            {
                Jurisdiction = Jurisdiction,
                Status = upcoming ? VerdictStatus.Upcoming : VerdictStatus.Finding,
                Band = upcoming ? FindingBand.Review : band,
                Score = Math.Round(score, 3),
                Severity = Rule.SeverityFor(Jurisdiction),
                Checkability = Rule.CheckabilityFor(Jurisdiction),
                LegalRefs = Rule.LegalRefs.Where(r => r.Jurisdiction == "eu" || r.Jurisdiction == Jurisdiction).ToList(),
                RuleSet = Set.Name,
                RuleSetVersion = Set.Version,
                ExplanationVariant = Set.ExplanationVariants.TryGetValue(Rule.Id, out var variants) && variants.Contains(Jurisdiction)
                    ? Jurisdiction
                    : JurisdictionVerdict.DefaultVariant,
                EffectiveFrom = EffectiveFrom,
                Notes = upcoming ? [.. notes, new FindingNote(EngineCodes.EffectiveFrom, NoteParams.Of(("date", EffectiveFrom!.Value)))] : notes,
                QuestionProbs = probabilities,
            };
        }

        public RuleResult Result(Segment segment, RuleOutcome outcome, double? score = null, FindingBand? band = null) =>
            new()
            {
                RuleId = Rule.Id, SegmentHash = segment.Hash, Outcome = outcome, Score = score is null ? null : Math.Round(score.Value, 3), Band = band,
                Jurisdiction = Jurisdiction, RuleSet = Set.Name,
            };

        public RuleResult SiteResult(string? segmentHash, RuleOutcome outcome, double score, FindingBand? band = null) =>
            new() { RuleId = Rule.Id, SegmentHash = segmentHash, Outcome = outcome, Score = score, Band = band, Jurisdiction = Jurisdiction, RuleSet = Set.Name };

        public SiteObligation Obligation(ObligationStatus status, IReadOnlyList<string>? urls = null, string? reason = null) => new()
        {
            Jurisdiction = Jurisdiction,
            RuleId = Rule.Id,
            Module = Set.Module,
            RuleSet = Set.Name,
            Status = status,
            Reason = reason,
            EffectiveFrom = EffectiveFrom,
            Urls = urls ?? [],
        };
    }
}
