using EshopGuard.Core.Models;

namespace EshopGuard.Core.Rules;

/// <summary>
/// How strict a verdict is: group (<c>text</c> before <c>assess</c>, <c>verify</c>, <c>not_checkable</c>), then severity
/// (<c>high</c>, <c>medium</c>, <c>low</c>), then band (high before review), then status (a valid finding before an upcoming
/// one), then jurisdiction alphabetically, so the order is stable. An upcoming verdict never makes a finding stricter than
/// a valid one: the status comes before the group.
/// </summary>
public static class VerdictOrder
{
    /// <summary>Negative when <paramref name="a"/> is stricter than <paramref name="b"/>.</summary>
    public static int Compare(JurisdictionVerdict a, JurisdictionVerdict b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var status = a.Status.CompareTo(b.Status);
        if (status != 0)
        {
            return status;
        }

        var group = CheckabilityRank(a.Checkability).CompareTo(CheckabilityRank(b.Checkability));
        if (group != 0)
        {
            return group;
        }

        var severity = SeverityRank(a.Severity).CompareTo(SeverityRank(b.Severity));
        if (severity != 0)
        {
            return severity;
        }

        var band = a.Band.CompareTo(b.Band);
        return band != 0 ? band : string.CompareOrdinal(a.Jurisdiction, b.Jurisdiction);
    }

    /// <summary>The strictest of the verdicts.</summary>
    /// <exception cref="InvalidOperationException">There is no verdict.</exception>
    public static JurisdictionVerdict Strictest(IReadOnlyList<JurisdictionVerdict> verdicts)
    {
        ArgumentNullException.ThrowIfNull(verdicts);
        if (verdicts.Count == 0)
        {
            throw new InvalidOperationException("A finding has no verdict.");
        }

        var strictest = verdicts[0];
        for (var i = 1; i < verdicts.Count; i++)
        {
            if (Compare(verdicts[i], strictest) < 0)
            {
                strictest = verdicts[i];
            }
        }

        return strictest;
    }

    /// <summary>The verdicts from the strictest.</summary>
    public static List<JurisdictionVerdict> Sort(IEnumerable<JurisdictionVerdict> verdicts)
    {
        var list = verdicts.ToList();
        list.Sort(Compare);
        return list;
    }

    /// <summary>Order of the groups: text, assess, verify, anything else.</summary>
    public static int CheckabilityRank(string checkability) => checkability switch
    {
        "text" => 0,
        "assess" => 1,
        "verify" => 2,
        _ => 3,
    };

    /// <summary>Order of the severities: high, medium, low, anything else.</summary>
    public static int SeverityRank(string severity) => severity switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };
}
