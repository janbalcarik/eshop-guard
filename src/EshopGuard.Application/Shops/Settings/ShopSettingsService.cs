using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops.Settings;

/// <summary>
/// The settings of an e-shop (change 10, AD 10): the name, the modules (at least one, only those available for the markets of
/// the e-shop: their rule sets cover one of its markets) and the check on save (only with a connector, not on BiznisWeb),
/// over the version of the row the client read (<c>409 concurrency.conflict</c>).
/// </summary>
public sealed class ShopSettingsService(EshopGuardDb db, ShopReader reader, ShopCatalog catalog, ScopeInputsLoader loader, SecurityAuditWriter audit)
{
    public async Task<ShopSettingsDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        var modules = await catalog.ModulesAsync(ct).ConfigureAwait(false);
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            return ToDto(stored, modules);
        }, ct).ConfigureAwait(false);
    }

    public async Task<ShopSettingsDto> UpdateAsync(
        Guid userId, Guid shopId, string? name, bool nameGiven, IReadOnlyList<string>? modules, bool? checkHiddenOnSave, uint? version, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var validName = nameGiven ? FieldValidators.Name(validation, "name", name, required: false) : null;
        if (version is null)
        {
            validation.Add("version", ProblemCodes.Fields.Required);
        }

        validation.ThrowIfInvalid();
        var catalogModules = await catalog.ModulesAsync(ct).ConfigureAwait(false);
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            var shop = await reader.RequireAsync(shopId, ct).ConfigureAwait(false);
            db.Entry(shop).Property(s => s.Version).OriginalValue = version!.Value;
            var changed = new JsonArray();
            if (nameGiven)
            {
                shop.Name = string.IsNullOrWhiteSpace(validName) ? null : validName;
                changed.Add("name");
            }

            if (modules is not null)
            {
                var wanted = modules.Select(m => m?.Trim() ?? "").Where(m => m.Length > 0).Distinct(StringComparer.Ordinal).ToList();
                if (wanted.Count == 0)
                {
                    throw new DomainException(ProblemCodes.SettingsNoModule, 400);
                }

                var available = Available(catalogModules, stored);
                if (wanted.FirstOrDefault(m => !available.Contains(m)) is { } unavailable)
                {
                    throw new DomainException(ProblemCodes.SettingsModuleUnavailable, 400, new Dictionary<string, object?> { ["module"] = unavailable });
                }

                shop.Modules = [.. wanted.Order(StringComparer.Ordinal)];
                changed.Add("modules");
            }

            if (checkHiddenOnSave is { } hidden)
            {
                if (hidden && shop.SourceMode != ShopSourceMode.Connector)
                {
                    throw new DomainException(ProblemCodes.SettingsHiddenCheckRequiresConnector, 409);
                }

                if (hidden && shop.Platform == ShopPlatform.Biznisweb)
                {
                    throw new DomainException(ProblemCodes.SettingsHiddenCheckUnsupportedPlatform, 409);
                }

                shop.CheckHiddenOnSave = hidden;
                changed.Add("checkHiddenOnSave");
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.SettingsChanged, shop.TenantId, userId, "shop", shopId.ToString("D"),
                new JsonObject { ["fields"] = changed }), ct).ConfigureAwait(false);
            await db.Entry(shop).ReloadAsync(ct).ConfigureAwait(false);
            return ToDto(stored with { Shop = shop }, catalogModules);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The modules available for the markets of the e-shop (task 10.1): the ticked or preselected markets, otherwise the
    /// country of the e-shop; every module when no market is known yet.
    /// </summary>
    public static IReadOnlyList<string> Available(IReadOnlyList<ModuleInfo> modules, StoredScope stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        IReadOnlyCollection<string>? markets = stored.ActiveMarkets.Count > 0 ? stored.ActiveMarkets : null;
        return ShopCatalog.Available(modules, markets);
    }

    private static ShopSettingsDto ToDto(StoredScope stored, IReadOnlyList<ModuleInfo> modules)
    {
        var shop = stored.Shop;
        var available = Available(modules, stored);
        return new ShopSettingsDto(
            shop.Name,
            modules.Select(m => new ShopModuleDto(m.Module, shop.Modules.Contains(m.Module) && available.Contains(m.Module), available.Contains(m.Module), m.Jurisdictions)).ToList(),
            shop.CheckHiddenOnSave,
            shop.SourceMode == ShopSourceMode.Connector && shop.Platform != ShopPlatform.Biznisweb,
            stored.Languages.Where(l => l.Status == ShopLanguageStatus.Excluded && l.DecidedAt is not null).Select(l => l.Language).ToList(),
            shop.Version);
    }
}
