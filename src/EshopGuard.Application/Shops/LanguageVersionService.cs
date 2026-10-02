using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Core.Markets;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The language versions of an e-shop (change 10, AD 6): the stored versions without the unsupported ones, whether they are
/// checked and for which jurisdictions by the same <see cref="ShopScopeCalculator"/> as the price, the confirmation of a
/// version on another domain and the exclusion of a version by the client (never the last checked one).
/// </summary>
public sealed class LanguageVersionService(EshopGuardDb db, ShopReader reader, ShopCatalog catalog, ScopeInputsLoader loader, SecurityAuditWriter audit, TimeProvider time)
{
    public const string SingleVersion = "single_version";
    public const string AllChecked = "all_checked";
    public const string SomeNotChecked = "some_not_checked";
    public const string NeedsConfirmation = "needs_confirmation";

    public async Task<LanguageVersionsDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            return Build(stored, ShopScopeCalculator.Calculate(stored.Input(), markets));
        }, ct).ConfigureAwait(false);
    }

    /// <summary><c>POST …/languages/{language}/confirmation</c>: a version on another domain belongs to the e-shop (<c>active</c>) or not (<c>excluded</c>).</summary>
    public async Task<LanguageVersionsDto> ConfirmAsync(Guid userId, Guid shopId, string language, bool? belongsToShop, CancellationToken ct)
    {
        if (belongsToShop is null)
        {
            throw DomainException.Validation(new ValidationResult().Add("belongsToShop", ProblemCodes.Fields.Required));
        }

        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, version) = await RequireAsync(shopId, language, ct).ConfigureAwait(false);
            if (version.Status != ShopLanguageStatus.NeedsConfirmation)
            {
                throw new DomainException(ProblemCodes.LanguageNotAwaitingConfirmation, 409);
            }

            Decide(version, belongsToShop.Value ? ShopLanguageStatus.Active : ShopLanguageStatus.Excluded, userId);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(belongsToShop.Value ? AuditActions.LanguageConfirmed : AuditActions.LanguageRejectedOtherDomain,
                shop.TenantId, userId, "shop", shopId.ToString("D"), new JsonObject { ["language"] = version.Language }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        return await GetAsync(shopId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>PUT …/languages/{language}/exclusion</c>: the client leaves a version out (<c>excluded</c>) or takes it back
    /// (<c>active</c>); not a version waiting for a confirmation (<c>409</c>), never the last checked one (<c>400</c>).
    /// </summary>
    public async Task<LanguageVersionsDto> ExcludeAsync(Guid userId, Guid shopId, string language, bool? excluded, CancellationToken ct)
    {
        if (excluded is null)
        {
            throw DomainException.Validation(new ValidationResult().Add("excluded", ProblemCodes.Fields.Required));
        }

        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, version) = await RequireAsync(shopId, language, ct).ConfigureAwait(false);
            if (version.Status == ShopLanguageStatus.NeedsConfirmation)
            {
                throw new DomainException(ProblemCodes.LanguageAwaitingConfirmation, 409);
            }

            if (excluded.Value)
            {
                var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
                var others = stored.Languages.Where(l => l.Status == ShopLanguageStatus.Excluded && l.DecidedAt is not null).Select(l => l.Language)
                    .Append(version.Language).ToList();
                var after = ShopScopeCalculator.Calculate(stored.Input(excluded: others), markets);
                var noVersion = stored.ActiveMarkets.Count > 0
                    ? after.Issues.Contains(ProblemCodes.ScopeNoCheckableVersion) || after.CheckedVersions.Count == 0
                    : !stored.Languages.Any(l => l.Language != version.Language && l.Status is ShopLanguageStatus.Active or ShopLanguageStatus.Excluded
                        && !(l.Status == ShopLanguageStatus.Excluded && l.DecidedAt is not null));
                if (noVersion)
                {
                    throw new DomainException(ProblemCodes.LanguageLastCheckedVersion, 400);
                }
            }

            Decide(version, excluded.Value ? ShopLanguageStatus.Excluded : ShopLanguageStatus.Active, userId);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(excluded.Value ? AuditActions.LanguageExcluded : AuditActions.LanguageIncluded,
                shop.TenantId, userId, "shop", shopId.ToString("D"), new JsonObject { ["language"] = version.Language }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        return await GetAsync(shopId, ct).ConfigureAwait(false);
    }

    /// <summary>The DTO of the versions from the stored state and its scope (also for the onboarding).</summary>
    public static LanguageVersionsDto Build(StoredScope stored, ShopScope scope)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(scope);
        var versions = new List<LanguageVersionDto>();
        foreach (var language in stored.Languages.Where(l => l.Status != ShopLanguageStatus.Unsupported))
        {
            var checkedVersion = scope.CheckedVersions.FirstOrDefault(v => v.Language == language.Language && v.BaseUrl == language.BaseUrl);
            var notChecked = scope.NotCheckedVersions.FirstOrDefault(v => v.Language == language.Language && v.BaseUrl == language.BaseUrl);
            var description = language.DescriptionLanguages?.RootElement;
            versions.Add(new LanguageVersionDto(
                language.Language,
                language.BaseUrl,
                language.Source == LanguageSource.Main,
                language.SwitchMethod is { } method ? ShopReader.Text(method) : null,
                ShopReader.Text(language.Source),
                StatusText(language),
                language.ProductCount,
                language.TranslatedShare is { } share ? Math.Round(share, 4) : null,
                Int(description, "labeled_products"),
                Int(description, "foreign_text_products"),
                Str(description, "foreign_text_language"),
                language.LanguageShare?.RootElement.Clone(),
                checkedVersion is not null,
                checkedVersion?.Reason ?? notChecked?.Reason ?? ShopScopeCalculator.NotNeeded,
                checkedVersion?.Jurisdictions ?? []));
        }

        var kind = versions.Any(v => v.Status == "needs_confirmation") ? NeedsConfirmation
            : versions.Count <= 1 ? SingleVersion
            : versions.All(v => v.Checked) ? AllChecked
            : SomeNotChecked;
        return new LanguageVersionsDto(
            new LanguageSummaryDto(
                kind, versions.Count, versions.Where(v => v.Checked).Select(v => v.Language).ToList(),
                scope.CheckedVersions.Any(v => v.Jurisdictions.Count > 1),
                scope.ByMarket.Select(m => new ScopeMarketDto(m.MarketCode, m.Language, m.ProductCount)).ToList()),
            versions);
    }

    /// <summary>The state as the client sees it: an exclusion of the analysis (a version no ticked market needs) is not his: <c>active</c>.</summary>
    private static string StatusText(ShopLanguage language) =>
        language.Status == ShopLanguageStatus.Excluded && language.DecidedAt is null ? "active" : ShopReader.Text(language.Status);

    private void Decide(ShopLanguage version, ShopLanguageStatus status, Guid userId)
    {
        version.Status = status;
        version.DecidedBy = userId;
        version.DecidedAt = time.GetUtcNow();
    }

    private async Task<(Shop Shop, ShopLanguage Version)> RequireAsync(Guid shopId, string language, CancellationToken ct)
    {
        var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
        var code = LanguageTags.Normalize(language);
        var version = await db.ShopLanguages.FirstOrDefaultAsync(l => l.ShopId == shopId && l.Language == code, ct).ConfigureAwait(false);
        return version is null || version.Status == ShopLanguageStatus.Unsupported
            ? throw new DomainException(ProblemCodes.LanguageNotFound, 404)
            : (shop, version);
    }

    private static int? Int(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var value) && value.TryGetInt32(out var n) ? n : null;

    private static string? Str(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
