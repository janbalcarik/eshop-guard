using System.Text.Json;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Jobs.Fixes;

namespace EshopGuard.Application.Fixes;

/// <summary>Proposals of fixes as the API returns them.</summary>
public static class ProposalMapper
{
    public static ProposalDto Dto(FixProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return new ProposalDto(
            proposal.Id, proposal.PageId, proposal.GroupId, proposal.FindingIds, FindingMapper.Text(proposal.Field), proposal.BlockIndex,
            proposal.OriginalText, proposal.ProposedText, ProposalText.Text(proposal),
            Alternatives(proposal), proposal.SelectedAlternative, proposal.EditedText, Placeholders(proposal), Recheck(proposal),
            FindingMapper.Text(proposal.Status), proposal.Version);
    }

    public static IReadOnlyList<AlternativeDto> Alternatives(FixProposal proposal) =>
        ProposalText.Alternatives(proposal.Alternatives).Select(a => new AlternativeDto(a.Key, a.Text, FindingMapper.Text(a.RecheckStatus))).ToList();

    public static IReadOnlyList<PlaceholderDto> Placeholders(FixProposal proposal) =>
        ProposalText.Placeholders(proposal.Placeholders).Select(p => new PlaceholderDto(p.Key, p.Value)).ToList();

    /// <summary>The recheck of the current text; for <c>still_finding</c> the countries and rules from <c>recheck_result</c>.</summary>
    public static RecheckDto Recheck(FixProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var status = ProposalText.Recheck(proposal);
        if (status != RecheckStatus.StillFinding)
        {
            return new RecheckDto(FindingMapper.Text(status), null, null);
        }

        var (jurisdictions, rules) = Failed(proposal.RecheckResult);
        return new RecheckDto(FindingMapper.Text(status), jurisdictions, rules);
    }

    /// <summary>The countries that still find something and the rules, from a result of <c>fix.recheck</c>.</summary>
    public static (IReadOnlyList<string> Jurisdictions, IReadOnlyList<string> RuleIds) Failed(JsonDocument? result)
    {
        if (result?.RootElement is not { ValueKind: JsonValueKind.Object } root)
        {
            return ([], []);
        }

        var jurisdictions = root.TryGetProperty("jurisdictions", out var j) && j.ValueKind == JsonValueKind.Object
            ? j.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "still_finding").Select(p => p.Name).Order(StringComparer.Ordinal).ToList()
            : [];
        var rules = root.TryGetProperty("rule_ids", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        return (jurisdictions, rules);
    }

    /// <summary>The countries a result of <c>fix.recheck</c> covers.</summary>
    public static IReadOnlySet<string> Covered(JsonDocument? result) =>
        result?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("jurisdictions", out var j) && j.ValueKind == JsonValueKind.Object
            ? j.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
