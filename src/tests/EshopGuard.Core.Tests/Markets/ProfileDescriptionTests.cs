using AngleSharp.Html.Parser;
using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Tests;

/// <summary>The product description by the profile: the description regions without the regions of other roles inside them.</summary>
public sealed class ProfileDescriptionTests
{
    private const string Page = """
        <html><body><main><h1>Čaj</h1>
        <div class="popis"><p>Čaj pochádza z horských lúk. Lístky zbierame ručne.</p>
          <div class="recenzie"><p>Chutná mi každý večer. Dorazil rychle.</p></div></div>
        <div class="kratky">Bylinkový čaj na večer.</div>
        <div class="parametre">Hmotnosť: 50 g</div></main></body></html>
        """;

    [Fact]
    public void Description_LeavesOutTheReviewsInsideIt()
    {
        var profile = Profile(("main_description", ".popis"), ("short_description", ".kratky"), ("reviews", ".recenzie"), ("parameters", ".parametre"));

        var sentences = ProfileDescription.Sentences(new HtmlParser().ParseDocument(Page), profile);

        Assert.Equal(["Čaj pochádza z horských lúk.", "Lístky zbierame ručne.", "Bylinkový čaj na večer."], sentences);
    }

    [Fact]
    public void ProfileWithoutADescriptionRegion_OrWithoutAMatch_GivesNothing()
    {
        var document = new HtmlParser().ParseDocument(Page);

        Assert.Null(ProfileDescription.Sentences(document, Profile(("product_box", "main"), ("reviews", ".recenzie"))));
        Assert.Null(ProfileDescription.Sentences(document, Profile(("main_description", ".popis-produktu"))));
    }

    private static PageProfile Profile(params (string Role, string Selector)[] regions) => new()
    {
        Id = "t#1",
        Site = "t",
        Regions = regions.Select(r => new ProfileRegion { Role = r.Role, Action = ProfileRegion.Check, Selector = r.Selector }).ToList(),
    };
}
