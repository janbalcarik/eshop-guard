using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The library returns codes and parameters, never finished sentences (change 6): every note and warning of a scan has a
/// code of <see cref="EngineCodes"/>, the result has no title, explanation or recommendation, and every code renders.
/// </summary>
public sealed class NoSentencesInResultTests
{
    [Fact]
    public async Task ScanResult_HasOnlyCodes()
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Jurisdictions = ["sk", "cz"] }, ct: TestContext.Current.CancellationToken);
        var json = JsonNode.Parse(JsonSerializer.Serialize(result, ReportFormat.Json))!;

        // What the library says itself; pages keep the title of the web page, which is content of the shop.
        var names = new List<string>();
        var codes = new List<string>();
        foreach (var part in new[] { "findings", "warnings", "site_obligations", "jurisdiction_coverage", "rule_sets" })
        {
            Walk(json[part], names, codes);
        }

        Assert.DoesNotContain("title", names);
        Assert.DoesNotContain("explanation", names);
        Assert.DoesNotContain("recommendation", names);
        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Contains(code, EngineCodes.All));

        // Every note and warning has its sentence.
        Assert.All(result.Findings.SelectMany(f => f.Verdicts).SelectMany(v => v.Notes), n => Assert.NotEmpty(TestTexts.Renderer.Note(n, "cs")));
        Assert.All(result.Warnings, w => Assert.NotEmpty(TestTexts.Renderer.Warning(w, "cs")));
    }

    [Fact]
    public void ClosestParagraphNote_HasItsNumbersAsParameters()
    {
        var set = TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_cz");
        var paragraph = new Segment
        {
            Hash = "sha256:p",
            Kind = SegmentKind.LegalParagraph,
            Text = "Spory\nSpory řešíme dohodou.",
            Urls = ["https://shop.example/vop"],
            Probabilities = set.Questions.Keys.ToDictionary(q => QuestionKey.Of(set, q), _ => 0.42),
        };

        var output = RuleEngine.Evaluate(new RuleEngineInput { RuleSets = [set], Labels = TestTexts.Catalog.Labels, Segments = [paragraph], Jurisdictions = ["cz"] });

        var note = Assert.Single(Assert.Single(output.Findings, f => f.RuleId == "legal_adr_missing").Strictest.Notes);
        Assert.Equal(EngineCodes.PresenceClosestParagraph, note.Code);
        Assert.Equal((0.42, 0.7), ((double)note.Params["probability"]!, (double)note.Params["threshold"]!));
        Assert.Equal("Nejbližší nalezený odstavec má pravděpodobnost 0,42, práh přítomnosti je 0,70.", TestTexts.Renderer.Note(note, "cs"));
    }

    [Fact]
    public void Notes_RenderTheSameAfterARoundTripThroughJson()
    {
        var note = new FindingNote(EngineCodes.UnreadPdfDocuments, NoteParams.Of(
            ("urls", new List<string> { "https://shop.example/a.pdf", "https://shop.example/b.pdf" }),
            ("more", new FindingNote(EngineCodes.ListMore, NoteParams.Of(("count", 3))))));

        var copy = JsonSerializer.Deserialize<FindingNote>(JsonSerializer.Serialize(note, ReportFormat.Json), ReportFormat.Json)!;

        Assert.Equal(note, copy);
        Assert.Equal("Informace může být v PDF, které nástroj nečte: https://shop.example/a.pdf, https://shop.example/b.pdf a dalších 3.", TestTexts.Renderer.Note(copy, "cs"));
        Assert.Equal(TestTexts.Renderer.Note(note, "cs"), TestTexts.Renderer.Note(copy, "cs"));
    }

    /// <summary>Every property name, and the code of every object that has one.</summary>
    private static void Walk(JsonNode? node, List<string> names, List<string> codes)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    names.Add(name);
                    if (name == "code" && value is JsonValue code && obj.ContainsKey("params"))
                    {
                        codes.Add(code.GetValue<string>());
                    }

                    Walk(value, names, codes);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Walk(item, names, codes);
                }

                break;
        }
    }
}
