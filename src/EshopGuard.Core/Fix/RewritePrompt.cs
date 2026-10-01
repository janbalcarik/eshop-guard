using System.Text;
using EshopGuard.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Content of <c>config/rewrite.yaml</c>: instructions, examples of bad and good wordings and verbatim legal texts.
/// Together they form the shared part of every request, which is the same for all pages, so OpenAI serves it from its
/// prompt cache; the page and its findings come after it.
/// </summary>
public sealed class RewritePromptFile
{
    /// <summary>Version of the prompt; part of the cache key of rewrites.</summary>
    public string Version { get; set; } = "";

    /// <summary>Instructions for the model (English, the new texts stay in the language of the page).</summary>
    public string Instructions { get; set; } = "";

    /// <summary>Examples of bad and good rewrites.</summary>
    public List<RewriteExample> Examples { get; set; } = [];

    /// <summary>Verbatim excerpts of the law, the directive and the Commission's answers.</summary>
    public List<RewriteLegalText> LegalTexts { get; set; } = [];
}

/// <summary>
/// An example: the situation, a bad rewrite and why it is bad, a good one and why it is good.
/// </summary>
public sealed class RewriteExample
{
    /// <summary>What is on the page and what the finding is.</summary>
    public string Situation { get; set; } = "";

    /// <summary>A bad rewrite.</summary>
    public string Bad { get; set; } = "";

    /// <summary>Why the bad rewrite is bad.</summary>
    public string WhyBad { get; set; } = "";

    /// <summary>A good rewrite (or what to do, e.g. keep the wording).</summary>
    public string Good { get; set; } = "";

    /// <summary>Why the good rewrite is good.</summary>
    public string WhyGood { get; set; } = "";
}

/// <summary>
/// A verbatim legal text with its source.
/// </summary>
public sealed class RewriteLegalText
{
    /// <summary>Heading, e.g. the provision.</summary>
    public string Title { get; set; } = "";

    /// <summary>Where the text comes from (file of the downloaded source or its URL).</summary>
    public string Source { get; set; } = "";

    /// <summary>The text itself, verbatim.</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// A page prepared for the model: numbered blocks and the findings with their ids.
/// </summary>
internal sealed class RewriteWork
{
    public required RewritePageInput Page { get; init; }

    /// <summary>Blocks in page order: B1.. for the main text, then TITLE, META, JSONLD or CHROME when needed.</summary>
    public required IReadOnlyList<(string Id, string Text)> Blocks { get; init; }

    public required IReadOnlyList<RewriteFinding> Findings { get; init; }
}

/// <summary>
/// Loads <c>config/rewrite.yaml</c> and renders the two parts of a request.
/// </summary>
internal static class RewritePrompt
{
    public const string PlaceholderMarker = "[doplňte";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static RewritePromptFile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Soubor se zadáním pro přepis {path} neexistuje.");
        }

        RewritePromptFile? prompt;
        try
        {
            prompt = Deserializer.Deserialize<RewritePromptFile?>(File.ReadAllText(path));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"Soubor {path} je neplatný (řádek {ex.Start.Line}): {ex.InnerException?.Message ?? ex.Message}", ex);
        }

        var errors = new List<string>();
        if (prompt is null)
        {
            errors.Add("soubor je prázdný");
        }
        else
        {
            var missing = new (bool Missing, string Name)[]
            {
                (string.IsNullOrWhiteSpace(prompt.Version), "version"),
                (string.IsNullOrWhiteSpace(prompt.Instructions), "instructions"),
                (prompt.Examples.Count == 0, "examples"),
                (prompt.LegalTexts.Count == 0, "legal_texts"),
            };
            errors.AddRange(missing.Where(m => m.Missing).Select(m => "chybí " + m.Name));
            errors.AddRange(prompt.Examples
                .Select((e, i) => (e, i))
                .Where(x => new[] { x.e.Situation, x.e.Bad, x.e.WhyBad, x.e.Good, x.e.WhyGood }.Any(string.IsNullOrWhiteSpace))
                .Select(x => $"příklad {x.i + 1} nemá vyplněné situation, bad, why_bad, good i why_good"));
            errors.AddRange(prompt.LegalTexts
                .Select((t, i) => (t, i))
                .Where(x => string.IsNullOrWhiteSpace(x.t.Title) || string.IsNullOrWhiteSpace(x.t.Text))
                .Select(x => $"právní text {x.i + 1} nemá title nebo text"));
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Soubor {path} je neplatný: {string.Join("; ", errors)}.");
        }

        return prompt!;
    }

    /// <summary>
    /// The shared part: instructions, examples, legal texts. It depends only on the file, never on the page, so it is
    /// byte for byte the same in every request and OpenAI reads it from its prompt cache.
    /// </summary>
    public static string SharedPart(RewritePromptFile prompt)
    {
        var text = new StringBuilder();
        text.Append(prompt.Instructions.Trim()).Append("\n\n");
        text.Append("Examples of bad and good rewrites (the texts are Slovak like the pages):\n");
        for (var i = 0; i < prompt.Examples.Count; i++)
        {
            var e = prompt.Examples[i];
            text.Append('\n').Append("Example ").Append(i + 1).Append(". ").Append(e.Situation.Trim()).Append('\n')
                .Append("  Bad: ").Append(e.Bad.Trim()).Append('\n')
                .Append("  Why bad: ").Append(e.WhyBad.Trim()).Append('\n')
                .Append("  Good: ").Append(e.Good.Trim()).Append('\n')
                .Append("  Why good: ").Append(e.WhyGood.Trim()).Append('\n');
        }

        text.Append("\nLegal texts (verbatim):\n");
        foreach (var legal in prompt.LegalTexts)
        {
            text.Append("\n### ").Append(legal.Title.Trim());
            if (!string.IsNullOrWhiteSpace(legal.Source))
            {
                text.Append(" (").Append(legal.Source.Trim()).Append(')');
            }

            text.Append('\n').Append(legal.Text.Trim()).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The page part: everything that differs from page to page, sent after the shared part.</summary>
    public static string PagePart(RewriteWork work)
    {
        var page = work.Page;
        var text = new StringBuilder();
        text.Append("URL: ").Append(page.Url).Append('\n');
        text.Append("Typ stránky: ").Append(page.Type).Append('\n');
        text.Append("Titulok a kategória: ").Append(string.IsNullOrWhiteSpace(page.Category) ? page.Title : page.Category).Append("\n\n");
        text.Append("Text stránky (bloky):\n");
        foreach (var (id, block) in work.Blocks)
        {
            text.Append('[').Append(id).Append("] ").Append(block).Append('\n');
        }

        text.Append("\nNálezy:\n");
        foreach (var finding in work.Findings)
        {
            var f = finding.Finding;
            text.Append("- ").Append(finding.Id).Append(" | skupina: ").Append(Group(f.Checkability))
                .Append(" | bloky: ").Append(string.Join(", ", finding.Blocks)).Append('\n')
                .Append("  veta: „").Append(f.Text).Append("“\n")
                .Append("  pravidlo: ").Append(f.Title).Append('\n')
                .Append("  vysvetlenie: ").Append(f.Explanation).Append('\n')
                .Append("  odporúčanie: ").Append(f.Recommendation).Append('\n');
            if (finding.AlsoOn.Count > 0)
            {
                text.Append("  rovnaký text je aj na ").Append(finding.AlsoOn.Count).Append(" ďalších stránkach\n");
            }
        }

        return text.ToString();
    }

    public static string Group(string checkability) => checkability switch
    {
        "text" => "porušení (rozhoduje text zákona)",
        "assess" => "k posouzení (posuzuje se případ od případu)",
        _ => checkability,
    };

    /// <summary>JSON schema of the answer (strict structured output).</summary>
    public static object Schema { get; } = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["changes"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["block_ids"] = StringArray(),
                        ["original"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["rewritten"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["finding_ids"] = StringArray(),
                        ["placeholders"] = StringArray(),
                        ["reason_cs"] = new Dictionary<string, object> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "block_ids", "original", "rewritten", "finding_ids", "placeholders", "reason_cs" },
                    ["additionalProperties"] = false,
                },
            },
            ["kept"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["finding_id"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["reason_cs"] = new Dictionary<string, object> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "finding_id", "reason_cs" },
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new[] { "changes", "kept" },
        ["additionalProperties"] = false,
    };

    private static Dictionary<string, object> StringArray() => new()
    {
        ["type"] = "array",
        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
    };
}
