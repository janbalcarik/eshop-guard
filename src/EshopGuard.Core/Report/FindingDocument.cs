using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Report;

/// <summary>
/// One finding in <c>findings.json</c>: the codes, parameters and verdicts of <see cref="Finding"/> and, next to them, the
/// texts of the strictest verdict in the language of the run (the fields <c>findings.json</c> had before change 6, with the
/// same names), so a reader without the rules can show it and <see cref="Fix.ScanOutputReader"/> restores the finding
/// without loss.
/// </summary>
public sealed class FindingDocument
{
    /// <summary>Rule id.</summary>
    public required string RuleId { get; init; }

    /// <summary>Rule module.</summary>
    public required string Module { get; init; }

    /// <summary>Title of the strictest verdict in <see cref="Locale"/>.</summary>
    public string Title { get; init; } = "";

    /// <summary>Severity of the strictest verdict.</summary>
    public string Severity { get; init; } = "";

    /// <summary>Group of the strictest verdict.</summary>
    public string Checkability { get; init; } = "";

    /// <summary><c>segment</c> or <c>site</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>Band of the strictest verdict.</summary>
    public FindingBand Band { get; init; }

    /// <summary>Score of the strictest verdict.</summary>
    public double Score { get; init; }

    /// <summary>The text on the web.</summary>
    public string? Text { get; init; }

    /// <summary>Preceding sentences.</summary>
    public string ContextBefore { get; init; } = "";

    /// <summary>Following sentences.</summary>
    public string ContextAfter { get; init; } = "";

    /// <summary>Parts of pages where the text occurs.</summary>
    public IReadOnlyList<SegmentSource> Sources { get; init; } = [];

    /// <summary>Pages the finding applies to.</summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>Number of pages.</summary>
    public int Occurrences { get; init; }

    /// <summary>True when the text is page frame or menu.</summary>
    public bool Boilerplate { get; init; }

    /// <summary>Probabilities of the strictest verdict, by question id of its rule set.</summary>
    public IReadOnlyDictionary<string, double> QuestionProbs { get; init; } = new Dictionary<string, double>();

    /// <summary>Legal references of all verdicts as shown (EU once).</summary>
    public IReadOnlyList<RenderedLegalRef> LegalRefs { get; init; } = [];

    /// <summary>Explanation of the strictest verdict.</summary>
    public string Explanation { get; init; } = "";

    /// <summary>Recommendation of the strictest verdict.</summary>
    public string Recommendation { get; init; } = "";

    /// <summary>Notes of all verdicts as shown; with several jurisdictions a note of only some of them starts with their codes.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Hash of the segment.</summary>
    public string? SegmentHash { get; init; }

    /// <summary>Fingerprint of the sentence.</summary>
    public long? TextFingerprint { get; init; }

    /// <summary>Language of <see cref="Title"/>, <see cref="Explanation"/> and <see cref="Recommendation"/>.</summary>
    public string Locale { get; init; } = "";

    /// <summary>Parameters of the texts of the rule.</summary>
    public IReadOnlyDictionary<string, object?> Params { get; init; } = new Dictionary<string, object?>();

    /// <summary>Verdicts by jurisdiction with codes (the source of everything above).</summary>
    public IReadOnlyList<JurisdictionVerdict> Verdicts { get; init; } = [];

    /// <summary>The document of a finding with its texts in <paramref name="locale"/>.</summary>
    public static FindingDocument From(Finding finding, RuleTextRenderer texts, string locale, bool severalJurisdictions)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(texts);
        var rendered = texts.Render(finding, locale);
        return new FindingDocument
        {
            RuleId = finding.RuleId,
            Module = finding.Module,
            Title = rendered.Title,
            Severity = finding.Severity,
            Checkability = finding.Checkability,
            Scope = finding.Scope,
            Band = finding.Band,
            Score = finding.Score,
            Text = finding.Text,
            ContextBefore = finding.ContextBefore,
            ContextAfter = finding.ContextAfter,
            Sources = finding.Sources,
            Urls = finding.Urls,
            Occurrences = finding.Occurrences,
            Boilerplate = finding.Boilerplate,
            QuestionProbs = finding.QuestionProbs,
            LegalRefs = finding.Verdicts.SelectMany(v => texts.Render(finding, v, locale).LegalRefs).Distinct().ToList(),
            Explanation = rendered.Explanation,
            Recommendation = rendered.Recommendation,
            Notes = RenderNotes(finding, texts, locale, severalJurisdictions),
            SegmentHash = finding.SegmentHash,
            TextFingerprint = finding.TextFingerprint,
            Locale = rendered.Locale,
            Params = finding.Params,
            Verdicts = finding.Verdicts,
        };
    }

    /// <summary>
    /// Notes of all verdicts in order; in a run for several jurisdictions a note that not every verdict has starts with the
    /// codes of the jurisdictions that have it.
    /// </summary>
    public static List<string> RenderNotes(Finding finding, RuleTextRenderer texts, string locale, bool severalJurisdictions)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(texts);
        var notes = new List<string>();
        foreach (var verdict in finding.Verdicts)
        {
            foreach (var note in verdict.Notes)
            {
                var text = texts.Note(note, locale);
                var owners = finding.Verdicts.Where(v => v.Notes.Contains(note)).Select(v => v.Jurisdiction.ToUpperInvariant()).ToList();
                notes.Add(severalJurisdictions && owners.Count < finding.Verdicts.Count ? $"{string.Join(", ", owners)}: {text}" : text);
            }
        }

        return notes.Distinct().ToList();
    }

    /// <summary>
    /// The finding of the document. A document written before change 6 has no verdicts; it gets one for
    /// <paramref name="jurisdiction"/> from its own fields, its rule set is looked up by the rule id when its texts are rendered.
    /// </summary>
    public Finding ToFinding(string jurisdiction) => new()
    {
        RuleId = RuleId,
        Module = Module,
        Scope = Scope,
        Text = Text,
        ContextBefore = ContextBefore,
        ContextAfter = ContextAfter,
        Sources = Sources,
        Urls = Urls,
        Boilerplate = Boilerplate,
        SegmentHash = SegmentHash,
        TextFingerprint = TextFingerprint,
        Params = Params,
        Verdicts = Verdicts.Count > 0
            ? Verdicts
            :
            [
                new JurisdictionVerdict
                {
                    Jurisdiction = jurisdiction,
                    Band = Band,
                    Score = Score,
                    Severity = Severity,
                    Checkability = Checkability,
                    LegalRefs = LegalRefs.Select(r => new LegalReference { Jurisdiction = r.Jurisdiction, Ref = r.Ref, Status = r.StatusCode ?? LegacyStatus(r.Status) }).ToList(),
                    RuleSet = "",
                    RuleSetVersion = "",
                    QuestionProbs = QuestionProbs,
                },
            ],
    };

    /// <summary>Code of a status written as text before change 6.</summary>
    private static string LegacyStatus(string status) => status switch
    {
        "ověřit" => EngineCodes.ToVerify,
        "doplnit" => EngineCodes.ToComplete,
        _ => status,
    };
}
