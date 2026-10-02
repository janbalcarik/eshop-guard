using EshopGuard.Application.Fixes;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Jobs.Fixes;

namespace EshopGuard.Application.Tests.Findings;

/// <summary>„Kopírovať text“ (change 11, task 8.2): the whole field with the accepted changes, nothing else.</summary>
public sealed class FixedTextComposerTests
{
    private static readonly PageText Zubna = new("Ekologická zubná pasta", null, null,
    [
        "Zubná pasta + bambusová kefka",
        "Okamžitý komfort v ekologickom sete. Všetky naše produkty balíme ekologicky.",
        "Bambusová kefka – ekologická",
        "alternatíva k plastu.",
        "Zloženie: voda, glycerín, mäta.",
    ]);

    private static int created;

    /// <summary>A proposal created after the previous one (the order of acceptance of overlapping changes).</summary>
    private static FixProposal Proposal(
        FixField field, string original, string proposed, int? block, FixProposalStatus status = FixProposalStatus.Accepted, string? placeholders = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Field = field,
        BlockIndex = block,
        OriginalText = original,
        ProposedText = proposed,
        Status = status,
        CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(Interlocked.Increment(ref created)),
        Placeholders = placeholders is null ? null : System.Text.Json.JsonDocument.Parse(placeholders),
    };

    [Fact]
    public void TwoChangesInOneBlock_GoInTogether_AndTheProposalNotDecidedOnlyListed()
    {
        var first = Proposal(FixField.Block, "Okamžitý komfort v ekologickom sete.", "Okamžitý komfort v sete s kefkou.", 2);
        var second = Proposal(FixField.Block, "Všetky naše produkty balíme ekologicky.", "Všetky naše produkty balíme do [doplňte: obal].", 2,
            placeholders: """[{"key":"obal","value":"papierovej krabice"}]""");
        var pending = Proposal(FixField.Block, "Zloženie: voda, glycerín, mäta.", "Zloženie: voda, glycerín.", 5, FixProposalStatus.Proposed);

        var composed = FixedTextComposer.Compose(Zubna, FixField.Block, [first, second, pending]);

        Assert.Equal("Okamžitý komfort v sete s kefkou. Všetky naše produkty balíme do papierovej krabice.", composed.Text.Split('\n')[1]);
        Assert.Equal("Zloženie: voda, glycerín, mäta.", composed.Text.Split('\n')[^1]);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), composed.Applied.Order());
        Assert.Equal([pending.Id], composed.Pending);
        Assert.Empty(composed.Unplaced);
    }

    [Fact]
    public void ChangeAcrossTheBoundaryOfTwoBlocks_ReplacesBoth()
    {
        var change = Proposal(FixField.Block, "Bambusová kefka – ekologická alternatíva k plastu.", "Bambusová kefka – rukoväť z bambusu namiesto plastu.", 3);

        var composed = FixedTextComposer.Compose(Zubna, FixField.Block, [change]);

        var lines = composed.Text.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("Bambusová kefka – rukoväť z bambusu namiesto plastu.", lines[2]);
        Assert.Equal("Zloženie: voda, glycerín, mäta.", lines[3]);
    }

    [Fact]
    public void Name_IsTheTitle_AndARemovedSentenceLeavesNoSpaces()
    {
        var name = Proposal(FixField.Name, "Ekologická zubná pasta", "Zubná pasta s mätou", null);
        var removed = Proposal(FixField.Block, "Všetky naše produkty balíme ekologicky.", "", 2);

        Assert.Equal("Zubná pasta s mätou", FixedTextComposer.Compose(Zubna, FixField.Name, [name, removed]).Text);
        Assert.Equal("Okamžitý komfort v ekologickom sete.", FixedTextComposer.Compose(Zubna, FixField.Block, [name, removed]).Text.Split('\n')[1]);
    }

    [Fact]
    public void ChangeNoLongerInThePage_IsUnplaced_AndAPublishedOneAlreadyInTheTextCounts()
    {
        var first = Proposal(FixField.Block, "Okamžitý komfort v ekologickom sete.", "Okamžitý komfort.", 2);
        var overlapping = Proposal(FixField.Block, "v ekologickom sete.", "v sete.", 2);
        var gone = Proposal(FixField.Block, "Hypoalergénna zubná pasta.", "Zubná pasta.", 2);
        var published = Proposal(FixField.Block, "Zloženie: voda, glycerín, mäta a cukor.", "Zloženie: voda, glycerín, mäta.", 5, FixProposalStatus.Published);

        var composed = FixedTextComposer.Compose(Zubna, FixField.Block, [first, overlapping, gone, published]);

        Assert.Equal(new[] { gone.Id, overlapping.Id }.Order(), composed.Unplaced.Order());
        Assert.Contains(published.Id, composed.Applied);
        Assert.StartsWith("Okamžitý komfort. Všetky", composed.Text.Split('\n')[1], StringComparison.Ordinal);
    }
}
