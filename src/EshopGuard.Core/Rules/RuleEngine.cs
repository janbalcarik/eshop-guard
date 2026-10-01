using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Everything the rule engine needs. The engine does no I/O, so it is tested with fixed probabilities.
/// </summary>
internal sealed class RuleEngineInput
{
    /// <summary>Rule sets selected for the run (enabled, requested module, scanned country).</summary>
    public required IReadOnlyList<RuleSet> RuleSets { get; init; }

    public required LabelConfiguration Labels { get; init; }

    /// <summary>Segments with probabilities filled by the evaluation.</summary>
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

    /// <summary>Country of the run; legal references are shown for the EU and this country.</summary>
    public required string Country { get; init; }

    /// <summary>False when no legal texts were supplied (check-text of a sentence); site rules are skipped.</summary>
    public bool EvaluateSitePresence { get; init; } = true;

    /// <summary>Legal documents that were not read; site findings then get a note and at most the review band.</summary>
    public IReadOnlyList<UncheckedDocument> UncheckedDocuments { get; init; } = [];

    /// <summary>
    /// Pages whose text was not in the HTML (rendered by JavaScript). Site findings that a page could contradict get a note,
    /// presence findings with such a legal page at most the review band.
    /// </summary>
    public IReadOnlyList<PageInfo> TextNotLoadedPages { get; init; } = [];

    /// <summary>Adds the built-in finding "Nenalezeny právní stránky".</summary>
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
}

/// <summary>
/// Combines question probabilities into findings according to the rule sets. No legal logic is hard-coded here,
/// except the built-in finding for a site without legal pages required by the specification.
/// </summary>
internal static class RuleEngine
{
    public const string MissingLegalPagesRuleId = "legal_pages_missing";

    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs-CZ");

    public static RuleEngineOutput Evaluate(RuleEngineInput input)
    {
        var output = new RuleEngineOutput();
        var matcher = new LabelMatcher(input.Labels);
        foreach (var set in input.RuleSets)
        {
            var kind = set.AppliesTo == RuleValidator.Sentence ? SegmentKind.Sentence : SegmentKind.LegalParagraph;
            var segments = input.Segments.Where(s => s.Kind == kind).ToList();
            foreach (var rule in set.Rules)
            {
                if (rule.Scope == RuleValidator.SegmentScope)
                {
                    EvaluateSegmentRule(set, rule, segments, input, matcher, output);
                }
                else if (rule.Scope == RuleValidator.SiteSignalScope)
                {
                    if (input.EvaluateSiteSignals)
                    {
                        EvaluateSiteSignal(set, rule, input, output);
                    }
                }
                else if (input.EvaluateSitePresence)
                {
                    EvaluateSitePresence(set, rule, segments, input, output);
                }
            }
        }

        MergeRepeatedTexts(output);
        MergeSameTexts(output);

        if (input.AddMissingLegalPagesFinding)
        {
            output.Findings.Add(MissingLegalPages(input));
            output.RuleResults.Add(new RuleResult { RuleId = MissingLegalPagesRuleId, Outcome = RuleOutcome.Finding, Score = 1 });
        }

        return output;
    }

    private static void EvaluateSegmentRule(
        RuleSet set, RuleDefinition rule, List<Segment> segments, RuleEngineInput input, LabelMatcher matcher, RuleEngineOutput output)
    {
        var logic = rule.Logic!;
        var questions = logic.All.Concat(logic.Any).Concat(logic.None).Select(c => c.Q).Distinct().ToList();
        foreach (var segment in segments)
        {
            if (segment.SkippedModules.Contains(set.Module))
            {
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.SkippedBySieve));
                continue;
            }

            if (!questions.All(segment.Probabilities.ContainsKey))
            {
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.NotEvaluated));
                continue;
            }

            double P(RuleCondition condition) => segment.Probabilities[condition.Q];

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
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.ConditionsNotMet, score));
                continue;
            }

            var band = SegmentBand(score, rule.Bands);
            if (band is null)
            {
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.BelowThreshold, score));
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
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.ExcludedByAllowlist, score));
                continue;
            }

            var notes = new List<string>();
            foreach (var check in rule.CodeChecks.Where(c => c.Type == RuleValidator.ClaimListMatch))
            {
                urls = urls.Where(url => ClaimListHolds(check, segment.Text, input.PageCategories.GetValueOrDefault(url, ""), input, notes)).ToList();
            }

            if (urls.Count == 0)
            {
                output.RuleResults.Add(Result(rule, segment, RuleOutcome.NotInList, score));
                continue;
            }

            if (rule.CodeChecks.Any(c => c.Type == RuleValidator.LabelNotes))
            {
                notes.AddRange(matcher.NotesFor(segment.Text));
            }

            output.RuleResults.Add(Result(rule, segment, RuleOutcome.Finding, score, band));
            output.Findings.Add(new Finding
            {
                RuleId = rule.Id,
                Module = set.Module,
                Title = rule.Title,
                Severity = rule.Severity,
                Checkability = rule.Checkability,
                Scope = "segment",
                Band = band.Value,
                Score = Math.Round(score, 3),
                Text = segment.Text,
                ContextBefore = segment.ContextBefore,
                ContextAfter = segment.ContextAfter,
                Sources = segment.Sources,
                Urls = urls,
                Boilerplate = segment.Boilerplate,
                QuestionProbs = questions.ToDictionary(q => q, q => segment.Probabilities[q]),
                LegalRefs = RefsFor(rule, input.Country),
                Explanation = rule.ExplanationFor(input.Country),
                Recommendation = rule.Recommendation,
                Notes = notes.Distinct().ToList(),
                SegmentHash = segment.Hash,
            });
        }
    }

    /// <summary>
    /// The sentence makes the claim of a list item whose category matches the page category (never the sentence itself);
    /// <c>outcomes</c> filters the items, <c>absent</c> turns the check around. Notes name the items, so the finding says
    /// which requirement and legal basis it rests on, or why the list did not decide.
    /// </summary>
    private static bool ClaimListHolds(CodeCheck check, string text, string category, RuleEngineInput input, List<string> notes)
    {
        var claimed = input.LegalRequirements.For(input.Country).Where(r => r.MatchesClaim(text)).ToList();
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
            ? $"Stejné tvrzení je v seznamu zákonných požadavků u kategorie {string.Join("; ", others)}; kategorie výrobku podle stránky: „{shown}“. Ověřte, zda do ní výrobek patří."
            : category.Length == 0
                ? "Kategorii výrobku se ze stránky nepodařilo zjistit a tvrzení není v seznamu zákonných požadavků."
                : $"Tvrzení není v seznamu zákonných požadavků pro kategorii výrobku podle stránky: „{shown}“.");
        return true;
    }

    private static string Describe(LegalRequirement requirement)
    {
        var since = requirement.Since is { Length: > 0 } s ? $" Platí od: {s}." : "";
        var note = requirement.Note is { Length: > 0 } n ? $" {n}" : "";
        return $"Seznam zákonných požadavků: {requirement.CategoryName}, „{requirement.Feature}“. Základ: {requirement.Basis}.{since}{note}";
    }

    private static void EvaluateSitePresence(
        RuleSet set, RuleDefinition rule, List<Segment> paragraphs, RuleEngineInput input, RuleEngineOutput output)
    {
        var question = rule.Question!;
        var evaluated = paragraphs.Where(p => p.Probabilities.ContainsKey(question)).ToList();
        var best = evaluated.MaxBy(p => p.Probabilities[question]);
        var max = best?.Probabilities[question] ?? 0;
        var present = max >= set.PresenceThreshold;

        var legalText = string.Join("\n", paragraphs.Select(p => p.Text));
        var missingPatterns = rule.CodeChecks
            .Where(c => c.Type == RuleValidator.RegexRequired && !IsMatch(legalText, c.Pattern!))
            .Select(c => c.Pattern!)
            .ToList();

        var notes = new List<string>();
        double score;
        FindingBand band;
        if (!present)
        {
            // A missing piece of information is never ignored: below the high band it is still "to review".
            score = 1 - max;
            band = score >= rule.Bands.High ? FindingBand.High : FindingBand.Review;
            if (best is not null)
            {
                notes.Add(string.Create(Czech, $"Nejbližší nalezený odstavec má pravděpodobnost {max:0.00}, práh přítomnosti je {set.PresenceThreshold:0.00}."));
            }

            notes.AddRange(missingPatterns.Select(p => $"Na právních stránkách chybí i text odpovídající vzoru {p}."));
        }
        else if (missingPatterns.Count > 0)
        {
            score = rule.Bands.Review;
            band = FindingBand.Review;
            notes.AddRange(missingPatterns.Select(p => string.Create(Czech,
                $"Jev informaci našel (pravděpodobnost {max:0.00}), ale na právních stránkách chybí text odpovídající vzoru {p}.")));
        }
        else
        {
            output.RuleResults.Add(new RuleResult { RuleId = rule.Id, SegmentHash = best?.Hash, Outcome = RuleOutcome.Present, Score = max });
            return;
        }

        var notEvaluated = paragraphs.Count - evaluated.Count;
        if (notEvaluated > 0)
        {
            band = FindingBand.Review;
            notes.Add($"{notEvaluated} odstavců právních stránek se nepodařilo vyhodnotit, informace může být v nich.");
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
            notes.Add(NotLoadedNote(notLoadedLegal, "Text těchto právních stránek se nenačetl (web ho nejspíš vykresluje JavaScriptem), informace může být na nich"));
        }

        output.RuleResults.Add(new RuleResult { RuleId = rule.Id, SegmentHash = best?.Hash, Outcome = RuleOutcome.Finding, Score = score, Band = band });
        output.Findings.Add(new Finding
        {
            RuleId = rule.Id,
            Module = set.Module,
            Title = rule.Title,
            Severity = rule.Severity,
            Checkability = rule.Checkability,
            Scope = "site",
            Band = band,
            Score = Math.Round(score, 3),
            Text = best?.Text,
            Sources = best?.Sources ?? [],
            Urls = best?.Urls ?? [],
            QuestionProbs = new Dictionary<string, double> { [question] = max },
            LegalRefs = RefsFor(rule, input.Country),
            Explanation = rule.ExplanationFor(input.Country),
            Recommendation = rule.Recommendation,
            Notes = notes,
            SegmentHash = best?.Hash,
        });
    }

    /// <summary>
    /// A sign on the whole site: required (a finding when no page has it) or forbidden (a finding listing the pages with it).
    /// Pages the crawler never downloads (cart, checkout, customer account) are not seen, so a finding is always to review.
    /// </summary>
    private static void EvaluateSiteSignal(RuleSet set, RuleDefinition rule, RuleEngineInput input, RuleEngineOutput output)
    {
        var required = rule.CodeChecks[0].Type == RuleValidator.SitePatternRequired;
        var matches = input.PageSignals
            .Where(page => rule.CodeChecks.Any(c => IsMatch(page.Value.For(c.Where), c.Pattern!)))
            .Select(page => page.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (required ? matches.Count > 0 : matches.Count == 0)
        {
            output.RuleResults.Add(new RuleResult { RuleId = rule.Id, Outcome = RuleOutcome.Present, Score = 1 });
            return;
        }

        var notes = new List<string>();
        if (required)
        {
            notes.Add($"Na žádné z {input.PageSignals.Count} stažených stránek se nenašel obrázek, odkaz ani text, který by to ukazoval.");
            notes.Add("Košík, pokladnu a zákaznický účet nástroj nestahuje; tam to ověřte ručně.");
            if (input.TextNotLoadedPages.Count > 0)
            {
                notes.Add(NotLoadedNote(input.TextNotLoadedPages, "Text těchto stránek se nenačetl (web ho nejspíš vykresluje JavaScriptem), může to být na nich"));
            }
        }
        else
        {
            notes.Add($"Nalezeno na {matches.Count} z {input.PageSignals.Count} stažených stránek.");
        }

        output.RuleResults.Add(new RuleResult { RuleId = rule.Id, Outcome = RuleOutcome.Finding, Score = 1, Band = FindingBand.Review });
        output.Findings.Add(new Finding
        {
            RuleId = rule.Id,
            Module = set.Module,
            Title = rule.Title,
            Severity = rule.Severity,
            Checkability = rule.Checkability,
            Scope = "site",
            Band = FindingBand.Review,
            Score = 1,
            Urls = matches,
            LegalRefs = RefsFor(rule, input.Country),
            Explanation = rule.ExplanationFor(input.Country),
            Recommendation = rule.Recommendation,
            Notes = notes,
        });
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
    /// a short tail (such as the shop name), it is the same claim, so only the finding from the page text is kept.
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
            var index = output.RuleResults.FindIndex(r => r.RuleId == duplicate.RuleId && r.SegmentHash == duplicate.SegmentHash && r.Outcome == RuleOutcome.Finding);
            if (index >= 0)
            {
                var old = output.RuleResults[index];
                output.RuleResults[index] = new RuleResult { RuleId = old.RuleId, SegmentHash = old.SegmentHash, Outcome = RuleOutcome.Duplicate, Score = old.Score };
            }
        }
    }

    /// <summary>
    /// The same text (for example a badge "Vegan" next to many products) found by the same rule in different contexts is
    /// one finding listing all its pages; the one with the highest score keeps its context and probabilities.
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
            var index = output.Findings.IndexOf(kept);
            output.Findings[index] = new Finding
            {
                RuleId = kept.RuleId,
                Module = kept.Module,
                Title = kept.Title,
                Severity = kept.Severity,
                Checkability = kept.Checkability,
                Scope = kept.Scope,
                Band = kept.Band,
                Score = kept.Score,
                Text = kept.Text,
                ContextBefore = kept.ContextBefore,
                ContextAfter = kept.ContextAfter,
                Sources = findings.SelectMany(f => f.Sources).Distinct().ToList(),
                Urls = findings.SelectMany(f => f.Urls).Distinct().ToList(),
                Boilerplate = kept.Boilerplate,
                QuestionProbs = kept.QuestionProbs,
                LegalRefs = kept.LegalRefs,
                Explanation = kept.Explanation,
                Recommendation = kept.Recommendation,
                Notes = findings.SelectMany(f => f.Notes).Distinct().ToList(),
                SegmentHash = kept.SegmentHash,
            };
            foreach (var other in findings.Skip(1))
            {
                output.Findings.Remove(other);
                var resultIndex = output.RuleResults.FindIndex(r => r.RuleId == other.RuleId && r.SegmentHash == other.SegmentHash && r.Outcome == RuleOutcome.Finding);
                if (resultIndex >= 0)
                {
                    var old = output.RuleResults[resultIndex];
                    output.RuleResults[resultIndex] = new RuleResult { RuleId = old.RuleId, SegmentHash = old.SegmentHash, Outcome = RuleOutcome.Duplicate, Score = old.Score };
                }
            }
        }
    }

    private static Finding MissingLegalPages(RuleEngineInput input)
    {
        var notes = new List<string>();
        if (input.UncheckedDocuments.Count > 0)
        {
            notes.Add(PdfNote(input.UncheckedDocuments));
        }

        return new Finding
        {
            RuleId = MissingLegalPagesRuleId,
            Module = "legal",
            Title = "Nenalezeny právní stránky",
            Severity = "high",
            Checkability = "text",
            Scope = "site",
            Band = input.UncheckedDocuments.Count > 0 ? FindingBand.Review : FindingBand.High,
            Score = 1,
            Explanation = "Na webu se nenašla žádná stránka s obchodními podmínkami, reklamačním řádem, informacemi o odstoupení, dopravě ani kontakty, takže povinné informace nešlo ověřit.",
            Recommendation = "Zveřejněte obchodní podmínky a reklamační řád jako stránky webu a odkažte na ně z patičky.",
            Notes = notes,
        };
    }

    private static string PdfNote(IReadOnlyList<UncheckedDocument> documents) =>
        "Informace může být v PDF, které nástroj nečte: " + string.Join(", ", documents.Take(5).Select(d => d.Url))
        + (documents.Count > 5 ? $" a dalších {documents.Count - 5}" : "") + ".";

    private static string NotLoadedNote(IReadOnlyList<PageInfo> pages, string intro) =>
        $"{intro}: " + string.Join(", ", pages.Take(5).Select(p => p.Url))
        + (pages.Count > 5 ? $" a dalších {pages.Count - 5}" : "") + ".";

    private static FindingBand? SegmentBand(double score, Bands bands) =>
        score >= bands.High ? FindingBand.High : score >= bands.Review ? FindingBand.Review : null;

    private static List<LegalReference> RefsFor(RuleDefinition rule, string country) =>
        rule.LegalRefs.Where(r => r.Jurisdiction == "eu" || r.Jurisdiction == country).ToList();

    private static RuleResult Result(RuleDefinition rule, Segment segment, RuleOutcome outcome, double? score = null, FindingBand? band = null) =>
        new() { RuleId = rule.Id, SegmentHash = segment.Hash, Outcome = outcome, Score = score is null ? null : Math.Round(score.Value, 3), Band = band };
}
