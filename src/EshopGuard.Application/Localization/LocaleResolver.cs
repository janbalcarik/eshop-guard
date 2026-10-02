using System.Globalization;
using EshopGuard.Application.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Localization;

/// <summary>Where the language of a user came from (<c>MeDto.localeSource</c>).</summary>
public enum LocaleSource
{
    User,
    AcceptLanguage,
    Market,
}

/// <summary>A resolved language and its source.</summary>
public sealed record ResolvedLocale(string Locale, LocaleSource Source)
{
    public string SourceCode => Source switch
    {
        LocaleSource.User => "user",
        LocaleSource.AcceptLanguage => "accept_language",
        _ => "market",
    };
}

/// <summary>
/// Language of a user (AD 10): (1) <c>users.locale</c> when it is enabled in <c>ref.locales</c>; (2) the first entry of
/// <c>Accept-Language</c> by <c>q</c> whose primary subtag is an enabled language; (3) the default language of the market (the
/// tenant's, the web edition's of the request, otherwise <c>Localization:DefaultMarket</c>). The market's language is the last
/// resort even when it is not enabled yet: the e-mails exist in it.
/// </summary>
public sealed class LocaleResolver(IRefCatalog catalog, IOptions<LocalizationOptions> options)
{
    public async Task<ResolvedLocale> ResolveAsync(string? userLocale, string? acceptLanguage, string? marketCode, CancellationToken ct = default)
    {
        var locales = await catalog.GetLocalesAsync(ct).ConfigureAwait(false);
        var markets = await catalog.GetMarketsAsync(ct).ConfigureAwait(false);
        return Resolve(userLocale, acceptLanguage, marketCode, locales, markets, options.Value.DefaultMarket);
    }

    /// <summary>Whether <paramref name="locale"/> is an enabled language.</summary>
    public async Task<bool> IsEnabledAsync(string locale, CancellationToken ct = default) =>
        (await catalog.GetLocalesAsync(ct).ConfigureAwait(false)).Any(l => l.Enabled && l.Code == locale);

    /// <summary>The market with the code, or <c>null</c>.</summary>
    public async Task<MarketInfo?> FindMarketAsync(string? code, CancellationToken ct = default) =>
        code is null ? null : (await catalog.GetMarketsAsync(ct).ConfigureAwait(false)).FirstOrDefault(m => m.Code == code);

    /// <summary>The market of <paramref name="code"/>, or the default market.</summary>
    public async Task<MarketInfo> MarketOrDefaultAsync(string? code, CancellationToken ct = default)
    {
        var markets = await catalog.GetMarketsAsync(ct).ConfigureAwait(false);
        return markets.FirstOrDefault(m => m.Code == code) ?? markets.FirstOrDefault(m => m.Code == options.Value.DefaultMarket)
            ?? throw new InvalidOperationException("config.default_market_unknown");
    }

    public static ResolvedLocale Resolve(
        string? userLocale, string? acceptLanguage, string? marketCode, IReadOnlyList<LocaleInfo> locales, IReadOnlyList<MarketInfo> markets, string defaultMarket)
    {
        ArgumentNullException.ThrowIfNull(locales);
        ArgumentNullException.ThrowIfNull(markets);
        var enabled = locales.Where(l => l.Enabled).Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        if (userLocale is not null && enabled.Contains(userLocale))
        {
            return new ResolvedLocale(userLocale, LocaleSource.User);
        }

        foreach (var tag in ParseAcceptLanguage(acceptLanguage))
        {
            if (enabled.Contains(tag))
            {
                return new ResolvedLocale(tag, LocaleSource.AcceptLanguage);
            }
        }

        var market = markets.FirstOrDefault(m => m.Code == marketCode) ?? markets.FirstOrDefault(m => m.Code == defaultMarket)
            ?? throw new InvalidOperationException("config.default_market_unknown");
        return new ResolvedLocale(market.DefaultLocale, LocaleSource.Market);
    }

    /// <summary>Primary subtags of <c>Accept-Language</c>, ordered by <c>q</c> (stable), without <c>q=0</c> and <c>*</c>.</summary>
    public static IReadOnlyList<string> ParseAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return [];
        }

        var entries = new List<(string Tag, double Q, int Index)>();
        var parts = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length && i < 20; i++)
        {
            var pieces = parts[i].Split(';', StringSplitOptions.TrimEntries);
            var q = 1.0;
            foreach (var parameter in pieces.Skip(1))
            {
                if (parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                    && !double.TryParse(parameter[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out q))
                {
                    q = 0;
                }
            }

            var tag = Primary(pieces[0]);
            if (q > 0 && tag.Length is >= 2 and <= 3 && tag.All(char.IsAsciiLetterLower))
            {
                entries.Add((tag, q, i));
            }
        }

        return entries.OrderByDescending(e => e.Q).ThenBy(e => e.Index).Select(e => e.Tag).Distinct().ToList();
    }

    /// <summary>Primary subtag in lower case (<c>sk-SK</c> → <c>sk</c>).</summary>
    public static string Primary(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var dash = tag.IndexOfAny(['-', '_']);
        return (dash < 0 ? tag : tag[..dash]).Trim().ToLowerInvariant();
    }
}
