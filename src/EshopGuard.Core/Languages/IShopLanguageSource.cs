namespace EshopGuard.Core.Languages;

/// <summary>A language of a shop known from its connector, with the address of its version when the connector gives one.</summary>
public sealed record ConnectorLanguage(string Language, string? BaseUrl);

/// <summary>
/// Languages of a shop from its connector (change 15); a source of versions after <c>hreflang</c> and the switch. Without a
/// connector nothing is added.
/// </summary>
public interface IShopLanguageSource
{
    Task<IReadOnlyList<ConnectorLanguage>> GetLanguagesAsync(Uri site, CancellationToken ct);
}
