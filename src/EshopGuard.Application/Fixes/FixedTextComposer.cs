using System.Text.RegularExpressions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Jobs.Fixes;

namespace EshopGuard.Application.Fixes;

/// <summary>The text of a field with the accepted changes written in.</summary>
/// <param name="Text">The whole field for „Kopírovať text“ (a publication writes it whole).</param>
/// <param name="SourceText">The text of the field in the version of the page (null when the extraction has none).</param>
/// <param name="Applied">Accepted (or published) proposals written into the text.</param>
/// <param name="Pending">Proposals of the field not decided yet; their text is not in <see cref="Text"/>.</param>
/// <param name="Unplaced">
/// Accepted proposals whose original text is not in the field any more (the page changed): not written in, never silently
/// dropped (fail-closed).
/// </param>
public sealed record ComposedText(string Text, string? SourceText, IReadOnlyList<Guid> Applied, IReadOnlyList<Guid> Pending, IReadOnlyList<Guid> Unplaced);

/// <summary>
/// Composes the text of a field of a page (change 11, AD 8) from the extraction of its version and the accepted proposals:
/// <c>name</c> = the title, <c>short_description</c> = the meta description, <c>description</c> = the JSON-LD description,
/// <c>block</c> = the main text (one block per line), as the worker maps the blocks of a rewrite (<c>RewriteBatchHandler.Field</c>).
/// Every accepted change replaces its original text (whitespace may differ, a line break too, so a change may span two blocks),
/// the one of its block first; several changes of one block go in together; changes that overlap or are no longer in the text
/// are <see cref="ComposedText.Unplaced"/>. Proposals not accepted are only listed.
/// </summary>
public static class FixedTextComposer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <summary>The text of the field in the extraction of the version (null when it has none).</summary>
    public static string? Source(PageText text, FixField field)
    {
        ArgumentNullException.ThrowIfNull(text);
        return field switch
        {
            FixField.Name => text.Title,
            FixField.ShortDescription => text.MetaDescription,
            FixField.Description => text.JsonLdDescription,
            _ => text.Blocks.Count == 0 ? null : string.Join('\n', text.Blocks),
        };
    }

    public static ComposedText Compose(PageText text, FixField field, IEnumerable<FixProposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(proposals);
        var source = Source(text, field);
        var own = proposals.Where(p => p.Field == field).ToList();
        var pending = own.Where(p => p.Status is FixProposalStatus.Proposed or FixProposalStatus.Edited).Select(p => p.Id).ToList();
        var accepted = own.Where(p => p.Status is FixProposalStatus.Accepted or FixProposalStatus.Published)
            .OrderBy(p => p.BlockIndex ?? int.MaxValue).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id).ToList();

        var current = source ?? "";
        var starts = BlockStarts(field, text);
        var placed = new List<(int Start, int Length, string Replacement, Guid Id)>();
        var applied = new List<Guid>();
        var unplaced = new List<Guid>();
        foreach (var proposal in accepted)
        {
            var replacement = ProposalText.Text(proposal).Trim();
            var from = proposal.BlockIndex is { } index && index >= 1 && index <= starts.Count ? starts[index - 1] : 0;
            if ((Find(current, proposal.OriginalText, from, placed) ?? Find(current, proposal.OriginalText, 0, placed)) is { } match)
            {
                placed.Add((match.Index, match.Length, replacement, proposal.Id));
                applied.Add(proposal.Id);
            }
            else if (proposal.Status == FixProposalStatus.Published && replacement.Length > 0 && Find(current, replacement, 0, []) is not null)
            {
                // Published and already in the text of a later version: nothing to write in again.
                applied.Add(proposal.Id);
            }
            else
            {
                unplaced.Add(proposal.Id);
            }
        }

        foreach (var (start, length, replacement, _) in placed.OrderByDescending(p => p.Start))
        {
            current = string.Concat(current.AsSpan(0, start), replacement, current.AsSpan(start + length));
        }

        return new ComposedText(Tidy(current, field), source, applied, pending, unplaced);
    }

    /// <summary>The first occurrence of the text at or after <paramref name="from"/> that overlaps no change placed before.</summary>
    private static Match? Find(string text, string? original, int from, List<(int Start, int Length, string Replacement, Guid Id)> placed)
    {
        var words = original?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (words.Length == 0)
        {
            return null;
        }

        var pattern = new Regex(string.Join(@"\s+", words.Select(Regex.Escape)), RegexOptions.CultureInvariant, Timeout);
        for (var match = pattern.Match(text, from); match.Success; match = match.NextMatch())
        {
            if (!placed.Any(p => match.Index < p.Start + p.Length && p.Start < match.Index + match.Length))
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Where every block starts in the main text (blocks joined by a line break).</summary>
    private static List<int> BlockStarts(FixField field, PageText text)
    {
        var starts = new List<int>();
        if (field != FixField.Block)
        {
            return starts;
        }

        var at = 0;
        foreach (var block in text.Blocks)
        {
            starts.Add(at);
            at += block.Length + 1;
        }

        return starts;
    }

    /// <summary>Spaces left by a removed sentence go; a block left empty goes too.</summary>
    private static string Tidy(string text, FixField field)
    {
        static string Line(string line) => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return field == FixField.Block
            ? string.Join('\n', text.Split('\n').Select(Line).Where(l => l.Length > 0))
            : Line(text.Trim());
    }
}
