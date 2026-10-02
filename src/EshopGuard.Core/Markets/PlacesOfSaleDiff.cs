namespace EshopGuard.Core.Markets;

/// <summary>
/// Countries with strong evidence that a newer analysis found and the previous one did not have as strong (change 16 sends
/// „Vyzerá to, že predávate aj …“ for them).
/// </summary>
public static class PlacesOfSaleDiff
{
    public static IReadOnlyList<CountryEvidence> NewStrongCountries(PlacesOfSaleResult previous, PlacesOfSaleResult current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var before = previous.Countries.Where(c => c.EvidenceLevel == EvidenceLevels.Strong).Select(c => c.Country).ToHashSet(StringComparer.Ordinal);
        return current.Countries.Where(c => c.EvidenceLevel == EvidenceLevels.Strong && !before.Contains(c.Country)).ToList();
    }
}
