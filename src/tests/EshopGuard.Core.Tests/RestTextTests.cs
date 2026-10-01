using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Visible text that SmartReader leaves out of the main text (a product box with badges, price and delivery) is still
/// checked, and every page reports how much of its visible text was checked.
/// </summary>
public class RestTextTests
{
    private const string Description =
        "<article id=\"description\" class=\"description\"><h2>Popis produktu</h2>"
        + "<p>Krém hydratuje a chráni pokožku pred vplyvmi prostredia. Podporuje obnovu a udržiavanie zdravej bariérovej funkcie pokožky a schopnosti zadržiavať vodu.</p>"
        + "<p>Obsahuje komplex ceramidov, olivový skvalán a marulový olej. Vhodný pre suchú a citlivú pleť, aj na noc pod očné okolie.</p>"
        + "<p>Nanášajte ráno a večer na vyčistenú pleť a jemne vmasírujte. Po otvorení spotrebujte do šiestich mesiacov a skladujte pri izbovej teplote.</p>"
        + "<p>Kozmetika je certifikovaná podľa Ecocert a vyrobená v malej manufaktúre. Zloženie: Aqua, Glycerin, Squalane, Ceramide NP.</p>"
        + "<p>Krém neobsahuje parabény ani silikóny. Balenie obsahuje 50 ml krému v sklenenom tégliku s hliníkovým viečkom.</p>"
        + "<p>Pri podráždení prestaňte krém používať. Uchovávajte mimo dosahu detí a chráňte pred priamym slnečným žiarením.</p>"
        + "<p>Ceramidy sú prirodzenou súčasťou kožnej bariéry. S pribúdajúcim vekom ich v pokožke ubúda, preto ich krém dopĺňa zvonka a pomáha pokožke udržať vlhkosť počas celého dňa.</p>"
        + "<p>Olivový skvalán sa rýchlo vstrebáva a nezanecháva mastný film. Marulový olej obsahuje antioxidanty a mastné kyseliny, ktoré pokožku zjemňujú a vyživujú.</p>"
        + "<p>Krém má ľahkú textúru a jemnú vôňu bez pridaného parfumu. Hodí sa aj ako podklad pod make-up, pretože sa rýchlo vstrebe a pleť nelesknúť.</p>"
        + "<p>Výrobok bol dermatologicky testovaný na dobrovoľníkoch so suchou a citlivou pleťou. Výsledky testu vám na požiadanie pošleme e-mailom.</p></article>";

    private const string Page =
        "<!doctype html><html lang=\"sk\"><head><meta charset=\"utf-8\"><title>Krém s ceramidmi 50 ml</title></head><body>"
        + "<header><nav><ul><li><a href=\"/kozmetika\">Kozmetika</a></li><li><a href=\"/drogeria\">Drogéria</a></li><li><a href=\"/potraviny\">Potraviny</a></li></ul></nav></header>"
        + "<div id=\"content\"><div class=\"product-top\"><div class=\"gallery\"><img src=\"/krem.jpg\" alt=\"\"></div>"
        + "<div class=\"product-box\"><h1>Krém s ceramidmi 50 ml</h1><div class=\"flags\"><span class=\"flag flag-eco\">Eco</span><span class=\"flag\">SK výrobok</span></div>"
        + "<div class=\"delivery\">Doprava ZDARMA nad 39,90 €</div><div class=\"stock\">Skladom, odosielame ihneď.</div></div></div>"
        + "<div class=\"product-detail\"><div class=\"tabs\"><a href=\"#description\">Popis</a></div>" + Description + "</div></div>"
        + "<footer><p>© Bylinkovo s. r. o., všetky práva vyhradené.</p></footer></body></html>";

    [Fact]
    public void ExtractRest_ChecksWhatTheMainTextLeftOut()
    {
        // On naturfyt.sk on 30. 9. 2026 SmartReader returned only the description, without the box with badges and price.
        var description = new ContentExtractor(NullLogger<ContentExtractor>.Instance)
            .Extract(new Uri("https://shop.example/krem"), $"<html><body>{Description}</body></html>").MainBlocks;
        var footer = new HashSet<string> { TextTools.NormalizeForHash("© Bylinkovo s. r. o., všetky práva vyhradené.") };

        var remainder = ContentExtractor.ReadRemainder(new Uri("https://shop.example/krem"), new AngleSharp.Html.Parser.HtmlParser().ParseDocument(Page), description, footer);
        var (rest, navigation) = (remainder.Rest, remainder.NavigationChars);

        var texts = rest.Select(b => b.Text).ToList();
        Assert.Contains("Eco", texts);
        Assert.Contains("SK výrobok", texts);
        Assert.Contains("Doprava ZDARMA nad 39,90 €", texts);
        Assert.Contains("Krém s ceramidmi 50 ml", texts);
        // "Eco" stays although the main text mentions Ecocert; long duplicates of the main text are left out.
        Assert.DoesNotContain(texts, t => t.Contains("komplex ceramidov", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("Bylinkovo", StringComparison.Ordinal));
        Assert.DoesNotContain("Kozmetika", texts);
        Assert.Equal("KozmetikaDrogériaPotraviny".Length, navigation);
    }

    [Fact]
    public async Task Scan_EvaluatesOtherTextAndReportsCoverage()
    {
        var root = Directory.CreateTempSubdirectory("EshopGuard-rest-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "index.html"), Page);
            File.WriteAllText(Path.Combine(root, "robots.txt"), "User-agent: *\nAllow: /\n");
            var fetcher = new FileSystemPageFetcher(root, FileSystemPageFetcher.DefaultBaseUrl);
            await using var provider = TestServices.Create(fetcher);
            var result = await provider.GetRequiredService<IEshopGuard>()
                .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);

            var home = Assert.Single(result.Pages);
            // Whether SmartReader keeps the box in the main text or not, every visible block is checked from one of the two.
            Assert.Contains("Eco", home.MainText.Split('\n').Concat(home.RestText.Split('\n')));
            Assert.Contains("Popis", home.RestText.Split('\n'));
            Assert.Contains(result.Segments, s => s.Text == "Doprava ZDARMA nad 39,90 €");
            Assert.True(home.CheckedTextChars > 0);
            Assert.False(home.HasUncheckedText, $"{home.UncheckedTextChars} of {home.VisibleTextChars} characters unchecked");
            Assert.Equal(home.VisibleTextChars, result.Stats.VisibleTextChars);
            Assert.Equal(0, result.Stats.PagesWithUncheckedText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Mohlo by sa vám hodiť", "div", "div")]
    [InlineData("Zákazníci kúpili aj", "ul", "li")]
    [InlineData("Súvisiace produkty", "div", "a")]
    public void ReadRemainder_LeavesOutTilesOfOtherProductsWhateverTheHeading(string heading, string listTag, string tileTag)
    {
        // The last case is vegis.sk on 1. 10. 2026: the whole tile is the link, <a class="product" href="...">.
        string Tile(int i) => tileTag == "a"
            ? $"<a class=\"product\" href=\"/produkt-{i}\"><span class=\"subtitle\">BIO olej z marhuľových jadier {i} – 50 ml</span><span class=\"price\"><span>1{i}.99 €</span></span></a>"
            : $"<{tileTag} class=\"product\"><a href=\"/produkt-{i}\"><span>BIO olej z marhuľových jadier {i} – 50 ml</span></a>"
              + $"<span class=\"price\">1{i},99 €</span><span class=\"flag\">Eco</span></{tileTag}>";
        var related = $"<section><h2>{heading}</h2><{listTag} class=\"products\">{string.Concat(Enumerable.Range(1, 4).Select(Tile))}</{listTag}></section>";
        // Three linked benefits without a price are the shop's own claims, not other products.
        var benefits = "<div class=\"benefits\">"
            + "<div><a href=\"/predajne\">2 kamenné predajne v Bratislave</a></div>"
            + "<div><a href=\"/znacky\">Certifikovaná prírodná kozmetika</a></div>"
            + "<div><a href=\"/poradenstvo\">Profesionálne poradenstvo</a></div></div>";
        var html = Page.Replace("<footer>", benefits + related + "<footer>", StringComparison.Ordinal);
        var description = new ContentExtractor(NullLogger<ContentExtractor>.Instance)
            .Extract(new Uri("https://shop.example/krem"), $"<html><body>{Description}</body></html>").MainBlocks;

        var remainder = ContentExtractor.ReadRemainder(new Uri("https://shop.example/krem"), new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html), description, []);

        var texts = remainder.Rest.Select(b => b.Text).ToList();
        Assert.DoesNotContain(texts, t => t.Contains("marhuľových", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("€", StringComparison.Ordinal) && t.Contains("99", StringComparison.Ordinal));
        Assert.True(remainder.ListingChars > 0);
        Assert.Contains("Certifikovaná prírodná kozmetika", texts);
        // The product's own badge in the product box stays.
        Assert.Contains("Eco", texts);
        Assert.Contains("Doprava ZDARMA nad 39,90 €", texts);
    }

    [Fact]
    public void Extract_DropsLongTileTextsFromTheMainTextButKeepsShortBadges()
    {
        var tiles = string.Concat(Enumerable.Range(1, 4).Select(i =>
            $"<div class=\"product\"><a href=\"/produkt-{i}\">Kakaové maslo pastilky BIO – potravinárske, 100 % čisté {i}</a> <span>1{i},49 €</span> <span class=\"flag flag-eco\">Eco</span></div>"));
        var html = $"<html><head><title>Krém</title></head><body><article><h1>Krém s ceramidmi</h1><div class=\"flags\"><span class=\"flag flag-eco\">Eco</span></div>"
            + Description.Replace("<article id=\"description\" class=\"description\">", "<div>", StringComparison.Ordinal).Replace("</article>", "</div>", StringComparison.Ordinal)
            + $"<div class=\"related\">{tiles}</div></article></body></html>";

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/krem"), html);

        var all = page.MainBlocks.Concat(page.RestBlocks).Select(b => b.Text).ToList();
        Assert.DoesNotContain(all, t => t.Contains("Kakaové maslo pastilky", StringComparison.Ordinal));
        Assert.Contains("Eco", all);
        Assert.True(page.ListingChars > 0);
    }

    [Fact]
    public void UncheckedText_IsWhatIsNeitherCheckedNorNavigation()
    {
        var page = new PageInfo { Url = "https://shop.example/", VisibleTextChars = 1000, NavigationTextChars = 600, CheckedTextChars = 150 };

        Assert.Equal(250, page.UncheckedTextChars);
        Assert.True(page.HasUncheckedText);
        Assert.False(new PageInfo { Url = "https://shop.example/", VisibleTextChars = 1000, NavigationTextChars = 600, CheckedTextChars = 420 }.HasUncheckedText);
    }
}
