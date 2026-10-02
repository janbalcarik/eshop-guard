using EshopGuard.Application.Localization;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Rules;

namespace EshopGuard.Application.Shops;

/// <summary>A module of the checks (<c>legal</c>, <c>eco</c> …) and the jurisdictions its enabled rule sets cover.</summary>
public sealed record ModuleInfo(string Module, IReadOnlyList<string> Jurisdictions);

/// <summary>
/// The markets and modules the API offers, from the same rules and <c>config/jurisdictions.yaml</c> the worker checks with
/// (countries and languages are data, never code): a market is supported when an enabled rule set covers its jurisdiction
/// and <c>ref.markets.checks_status</c> is not <c>none</c>; customers of a market read the versions in its
/// <c>readable_languages</c>. The rules are read once (the API restarts with new rules), <c>ref.markets</c> by its cache.
/// </summary>
public sealed class ShopCatalog(IRuleSetProvider rules, IRefCatalog refCatalog)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RuleCatalog? _rules;

    public async Task<MarketCatalog> MarketsAsync(CancellationToken ct)
    {
        var catalog = await RulesAsync(ct).ConfigureAwait(false);
        var markets = await refCatalog.GetMarketsAsync(ct).ConfigureAwait(false);
        var allowed = markets.Where(m => m.ChecksStatus != "none").Select(m => m.Code).ToHashSet(StringComparer.Ordinal);
        return MarketCatalog.From(catalog, allowed.Contains);
    }

    /// <summary>Every module with an enabled rule set, with the jurisdictions of its sets, in the order of the modules.</summary>
    public async Task<IReadOnlyList<ModuleInfo>> ModulesAsync(CancellationToken ct)
    {
        var catalog = await RulesAsync(ct).ConfigureAwait(false);
        return catalog.RuleSets.Where(s => s.Enabled)
            .GroupBy(s => s.Module, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ModuleInfo(g.Key, g.SelectMany(s => s.Jurisdictions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()))
            .ToList();
    }

    /// <summary>The modules available for the markets (their jurisdictions meet the markets); every module when no market is given.</summary>
    public static IReadOnlyList<string> Available(IReadOnlyList<ModuleInfo> modules, IReadOnlyCollection<string>? markets) =>
        modules.Where(m => markets is null || m.Jurisdictions.Any(markets.Contains)).Select(m => m.Module).ToList();

    /// <summary>The rules and their texts, loaded once.</summary>
    public async Task<RuleCatalog> RulesAsync(CancellationToken ct)
    {
        if (_rules is { } loaded)
        {
            return loaded;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _rules ??= await rules.LoadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
