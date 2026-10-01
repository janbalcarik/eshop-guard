using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Segmentation;

namespace EshopGuard.Core.Tests;

public class SegmentTests
{
    private static readonly SentenceSplitter Splitter = new(new SegmentationOptions());

    [Theory]
    [InlineData("Obsahuje např. levanduli a tzv. bylinný extrakt. Balení má 250 ml.", "Obsahuje např. levanduli a tzv. bylinný extrakt.|Balení má 250 ml.")]
    [InlineData("Výrobce je Bylinková dílna s.r.o. Sídlo je v Jihlavě.", "Výrobce je Bylinková dílna s.r.o.|Sídlo je v Jihlavě.")]
    [InlineData("Firma Bylinková dílna s.r.o. se sídlem v Jihlavě vyrábí mýdla.", "Firma Bylinková dílna s.r.o. se sídlem v Jihlavě vyrábí mýdla.")]
    [InlineData("Výrobcem je Mýdlárna a.s. Vše vyrábíme ručně.", "Výrobcem je Mýdlárna a.s.|Vše vyrábíme ručně.")]
    [InlineData("Cena je 3,5 Kč za gram. Obsah je 2.5 l.", "Cena je 3,5 Kč za gram.|Obsah je 2.5 l.")]
    [InlineData("Platí od 27. 9. 2026 pro všechny objednávky. Děkujeme.", "Platí od 27. 9. 2026 pro všechny objednávky.|Děkujeme.")]
    [InlineData("Otevřeno od 1. ledna do 31. prosince. Těšíme se.", "Otevřeno od 1. ledna do 31. prosince.|Těšíme se.")]
    [InlineData("Založeno roku 2010. Od té doby rosteme.", "Založeno roku 2010.|Od té doby rosteme.")]
    [InlineData("3. Odstoupení od smlouvy", "3. Odstoupení od smlouvy")]
    [InlineData("12. Závěrečná ustanovení. Platí od 1. 1. 2026.", "12. Závěrečná ustanovení.|Platí od 1. 1. 2026.")]
    [InlineData("Prodáváme mýdla, šampony atd. Vše je ručně vyráběné.", "Prodáváme mýdla, šampony atd.|Vše je ručně vyráběné.")]
    [InlineData("Vydrží cca 2 měsíce, min. 50 umytí a max. 3 roky od výroby.", "Vydrží cca 2 měsíce, min. 50 umytí a max. 3 roky od výroby.")]
    [InlineData("Zboží č. 123 je skladem, resp. na cestě. Objednejte ještě dnes!", "Zboží č. 123 je skladem, resp. na cestě.|Objednejte ještě dnes!")]
    [InlineData("Obsahuje napr. levanduľu, t. j. prírodnú zložku. Balenie má 250 ml.", "Obsahuje napr. levanduľu, t. j. prírodnú zložku.|Balenie má 250 ml.")]
    [InlineData("Opravdu? Ano! Je to tak.", "Opravdu?|Ano!|Je to tak.")]
    [InlineData("Říkáme „Méně je více.“ A tak to je.", "Říkáme „Méně je více.“|A tak to je.")]
    [InlineData("Web www.bylinkova-dilna.example je náš. Pište nám.", "Web www.bylinkova-dilna.example je náš.|Pište nám.")]
    [InlineData("Ekologický produkt – obal je z recyklovaného papíru.", "Ekologický produkt – obal je z recyklovaného papíru.")]
    public void Split_HandlesAbbreviationsNumbersAndQuotes(string text, string expected)
    {
        Assert.Equal(expected.Split('|'), Splitter.Split(text));
    }

    [Fact]
    public void SplitLong_CutsWithinLimitWithoutLosingText()
    {
        var sentence = string.Join(", ", Enumerable.Repeat("slovo slovo slovo", 50));

        var parts = SentenceSplitter.SplitLong(sentence, 600);

        Assert.True(parts.Count >= 2);
        Assert.All(parts, p => Assert.True(p.Length <= 600));
        Assert.Equal(sentence.Replace(" ", ""), string.Concat(parts).Replace(" ", ""));
    }

    [Theory]
    [InlineData("EKO", true)]
    [InlineData("BIO", true)]
    [InlineData("Ekologický", true)]
    [InlineData("Eko šampon", true)]
    [InlineData("Šetrné k přírodě", true)]
    [InlineData("Skladem", true)]
    [InlineData("Cena: 189 Kč", true)]
    [InlineData("5 %", false)]
    [InlineData("ks", false)]
    [InlineData("189,90 Kč", false)]
    [InlineData("★★★★★ 4,8/5", false)]
    public void MinimumRule_NeedsThreeCharactersAndOneWordOfThreeLetters(string text, bool expected)
    {
        Assert.Equal(expected, SegmentBuilder.IsWorthEvaluating(text, new SegmentationOptions()));
    }

    [Fact]
    public void MinimumRule_CharacterLimitIsConfigurable()
    {
        var options = new SegmentationOptions { MinSegmentLength = 8 };

        Assert.False(SegmentBuilder.IsWorthEvaluating("EKO", options));
        Assert.True(SegmentBuilder.IsWorthEvaluating("Ekologický", options));
    }

    [Fact]
    public void BuildSentences_DropsShortSegmentsAndAddsContext()
    {
        var builder = CreateBuilder();
        var page = new ExtractedPage
        {
            Title = "Bylinný šampon s levandulí | Shop",
            MainBlocks =
            [
                new TextBlock("Bylinný šampon", 1),
                new TextBlock("Tento šampon je šetrný k přírodě. Obsahuje 98 % složek přírodního původu. Balení má 250 ml."),
                new TextBlock("189 Kč"),
            ],
        };

        var sentences = builder.BuildSentences("https://shop.example/sampon", PageType.Product, page);

        Assert.Contains(sentences, s => s.Text == "Bylinný šampon");
        Assert.DoesNotContain(sentences, s => s.Text == "189 Kč");
        var claim = Assert.Single(sentences, s => s.Text == "Tento šampon je šetrný k přírodě.");
        Assert.Equal("Bylinný šampon", claim.ContextBefore);
        Assert.Equal("Obsahuje 98 % složek přírodního původu. Balení má 250 ml.", claim.ContextAfter);
        Assert.Equal(SegmentSource.Main, claim.Source);

        // Context never crosses from the main text into another part of the page.
        var title = Assert.Single(sentences, s => s.Source == SegmentSource.Title);
        Assert.Equal("", title.ContextBefore);
        Assert.Equal("", title.ContextAfter);
    }

    [Fact]
    public void BuildLegalParagraphs_PrefixesHeadingAndRespectsLimit()
    {
        var builder = CreateBuilder(o => o.MaxParagraphLength = 200);
        var longText = string.Join(" ", Enumerable.Repeat("Spotřebitel má právo odstoupit od smlouvy do 14 dnů.", 6));
        var page = new ExtractedPage
        {
            MainBlocks =
            [
                new TextBlock("Obchodní podmínky", 1),
                new TextBlock("Úvodní text podmínek pro všechny kupující."),
                new TextBlock("3. Odstoupení od smlouvy", 2),
                new TextBlock(longText),
            ],
        };

        var paragraphs = builder.BuildLegalParagraphs("https://shop.example/obchodni-podminky", page);

        Assert.Equal(3, paragraphs.Count);
        Assert.Equal("Obchodní podmínky\nÚvodní text podmínek pro všechny kupující.", paragraphs[0].Text);
        Assert.All(paragraphs.Skip(1), p => Assert.StartsWith("3. Odstoupení od smlouvy\n", p.Text));
        Assert.All(paragraphs, p => Assert.True(p.Text.Length <= 200, p.Text));
        Assert.All(paragraphs, p => Assert.Equal(SegmentKind.LegalParagraph, p.Kind));
    }

    [Fact]
    public void Aggregate_DeduplicatesByTextAndContextAndMarksBoilerplate()
    {
        var occurrences = new List<SegmentOccurrence>();
        for (var i = 0; i < 4; i++)
        {
            occurrences.Add(Sentence("Rodinný e-shop od roku 2010.", "", $"https://shop.example/{i}", SegmentSource.Chrome));
        }

        occurrences.Add(Sentence("Tento šampon je ekologický.", "", "https://shop.example/0", SegmentSource.Main));
        occurrences.Add(Sentence("Tento  šampon je EKOLOGICKÝ.", "", "https://shop.example/1", SegmentSource.Main));
        occurrences.Add(Sentence("Tento šampon je ekologický.", "Jiný kontext.", "https://shop.example/2", SegmentSource.Main));

        var segments = SegmentAggregator.Aggregate(occurrences, pageCount: 10, new SegmentationOptions());

        var footer = Assert.Single(segments, s => s.Text == "Rodinný e-shop od roku 2010.");
        Assert.True(footer.Boilerplate);
        Assert.Equal(4, footer.Occurrences);
        Assert.StartsWith("sha256:", footer.Hash);

        var claims = segments.Where(s => TextTools.NormalizeForHash(s.Text) == "tento šampon je ekologický.").ToList();
        Assert.Equal(2, claims.Count);
        Assert.Equal(2, claims.Single(s => s.ContextBefore == "").Occurrences);

        // Three pages of ten is 30 %, which is not more than the 30 % threshold.
        Assert.All(claims, s => Assert.False(s.Boilerplate));
    }

    private static SegmentOccurrence Sentence(string text, string before, string url, SegmentSource source) =>
        new(SegmentKind.Sentence, text, before, "", url, PageType.Content, source);

    private static SegmentBuilder CreateBuilder(Action<SegmentationOptions>? configure = null)
    {
        var options = new EshopGuardOptions();
        configure?.Invoke(options.Segmentation);
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        return new SegmentBuilder(new SentenceSplitter(wrapped), wrapped);
    }
}
