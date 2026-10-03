using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Billing.Customers;

/// <summary>
/// The billing details of the tenant (design BillingDetails, 8b; task 9.4): the buyer on the invoices.
/// <list type="bullet">
/// <item>required are the business name, IČO, the seat and the e-mail for invoices; the format of IČO is not checked, since each
/// country has its own;</item>
/// <item>the country of the seat is fixed after the first payment (<c>tenants.currency</c> is set): another one is
/// <c>409 billing.country_locked</c>, only the support changes it;</item>
/// <item>a new or removed VAT id starts its verification again (<c>tax_id_status = none</c>); the old tax ids of the customer of
/// Stripe go away before the save (Stripe Tax would apply them), so when Stripe is unavailable nothing is saved
/// (<c>503 billing.unavailable</c>);</item>
/// <item>only over the version the client has read (<c>409 concurrency.conflict</c>); invoices already issued keep their buyer.</item>
/// </list>
/// The customer of Stripe gets the new details after the save, best effort: when it fails, the details stay saved and the
/// payment sends them and the VAT id later (<see cref="Tax.PaymentTaxGate"/>).
/// </summary>
public sealed class BillingDetailsService(EshopGuardDb db, StripeCustomers customers, SecurityAuditWriter audit, ILogger<BillingDetailsService> logger)
{
    public async Task<BillingDetailsDto> GetAsync(CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var tenant = await db.ExecuteInTenantTransactionAsync(async () =>
            await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.TenantNotFound, 404), ct).ConfigureAwait(false);
        return ToDto(tenant);
    }

    public async Task<BillingDetailsDto> UpdateAsync(Guid userId, BillingDetailsInput input, uint? version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var details = Validate(input, version);
        var tenantId = db.TenantContext.RequireTenantId();
        var saved = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            // Serializes the changes of the details with each other and with the webhooks of the VAT id.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM iam.tenants WHERE id = {tenantId} FOR UPDATE", ct).ConfigureAwait(false);
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.TenantNotFound, 404);
            if (tenant.Version != version!.Value)
            {
                throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
            }

            if (tenant.Currency is not null && !string.Equals(tenant.CountryCode, details.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException(BillingCodes.CountryLocked, 409);
            }

            var vatChanged = !string.Equals(tenant.IcDph is null ? null : StripeCustomers.Normalize(tenant.IcDph), details.IcDph, StringComparison.Ordinal);
            var changed = Apply(tenant, details);
            if (changed.Count == 0)
            {
                return new Saved(false, tenant.StripeCustomerId);
            }

            if (vatChanged)
            {
                tenant.TaxIdStatus = TaxIdStatus.None;
                tenant.TaxIdVerifiedAt = null;

                // Before the save: when Stripe is unavailable nothing changes (503), so the old VAT id cannot stay with the customer.
                if (tenant.StripeCustomerId is { } customerId)
                {
                    await customers.RemoveTaxIdsAsync(customerId, ct).ConfigureAwait(false);
                }
            }

            db.Entry(tenant).Property(t => t.Version).OriginalValue = version.Value;
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.TenantBillingDetailsUpdated, tenantId, userId, "tenant", tenantId.ToString("D"),
                new JsonObject { ["fields"] = new JsonArray([.. changed.Select(f => JsonValue.Create(f))]) }), ct).ConfigureAwait(false);
            return new Saved(true, tenant.StripeCustomerId);
        }, ct).ConfigureAwait(false);

        if (saved.Changed && (saved.StripeCustomerId is not null || details.IcDph is not null))
        {
            await SyncStripeAsync(tenantId, ct).ConfigureAwait(false);
        }

        return await GetAsync(ct).ConfigureAwait(false);
    }

    private static Details Validate(BillingDetailsInput input, uint? version)
    {
        var result = new ValidationResult();
        var legalName = FieldValidators.Name(result, "legalName", input.LegalName);
        var ico = FieldValidators.Name(result, "ico", input.Ico);
        var dic = FieldValidators.Name(result, "dic", input.Dic, required: false);
        var icDph = FieldValidators.Name(result, "icDph", input.IcDph, required: false);
        var street = FieldValidators.Name(result, "street", input.Street);
        var postalCode = FieldValidators.Name(result, "postalCode", input.PostalCode);
        var city = FieldValidators.Name(result, "city", input.City);
        var country = Country(result, input.CountryCode);
        var email = FieldValidators.Email(result, "billingEmail", input.BillingEmail);
        if (version is null)
        {
            result.Add("version", ProblemCodes.Fields.Required);
        }

        result.ThrowIfInvalid();
        return new Details(legalName!, ico!, dic, icDph is null ? null : StripeCustomers.Normalize(icDph), street!, postalCode!, city!, country!, email!);
    }

    /// <summary>A country by its ISO 3166 code (<c>SK</c>, <c>CZ</c>…), in capitals.</summary>
    private static string? Country(ValidationResult result, string? value)
    {
        var code = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            result.Add("countryCode", ProblemCodes.Fields.Required);
            return null;
        }

        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper) || !IsRegion(code))
        {
            result.Add("countryCode", ProblemCodes.Fields.ValueNotAllowed);
            return null;
        }

        return code;
    }

    private static bool IsRegion(string code)
    {
        try
        {
            return string.Equals(new RegionInfo(code).TwoLetterISORegionName, code, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The new details on the tenant; returns the names of the fields that changed (for the audit, without their values).</summary>
    private static List<string> Apply(Tenant tenant, Details details)
    {
        var changed = new List<string>();
        Set(changed, "legalName", tenant.LegalName, details.LegalName, v => tenant.LegalName = v);
        Set(changed, "ico", tenant.Ico, details.Ico, v => tenant.Ico = v);
        Set(changed, "dic", tenant.Dic, details.Dic, v => tenant.Dic = v);
        Set(changed, "icDph", tenant.IcDph, details.IcDph, v => tenant.IcDph = v);
        Set(changed, "street", tenant.Street, details.Street, v => tenant.Street = v);
        Set(changed, "postalCode", tenant.PostalCode, details.PostalCode, v => tenant.PostalCode = v);
        Set(changed, "city", tenant.City, details.City, v => tenant.City = v);
        Set(changed, "countryCode", tenant.CountryCode, details.CountryCode, v => tenant.CountryCode = v!);
        Set(changed, "billingEmail", tenant.BillingEmail, details.BillingEmail, v => tenant.BillingEmail = v);
        return changed;
    }

    private static void Set(List<string> changed, string field, string? current, string? value, Action<string?> set)
    {
        if (!string.Equals(current, value, StringComparison.Ordinal))
        {
            set(value);
            changed.Add(field);
        }
    }

    /// <summary>
    /// The customer of Stripe gets the new details and a VAT id not yet sent starts its verification; best effort, the payment
    /// sends them later (<see cref="Tax.PaymentTaxGate"/>).
    /// </summary>
    private async Task SyncStripeAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
        }
        catch (BillingUnavailableException)
        {
            logger.LogInformation("billing_details.stripe_skipped {TenantId}", tenantId);
        }
        catch (StripeGatewayException e)
        {
            logger.LogWarning("billing_details.stripe_failed {TenantId} {Code} {Status}", tenantId, e.Code, e.HttpStatus);
        }
    }

    private static BillingDetailsDto ToDto(Tenant tenant) => new(
        tenant.LegalName,
        tenant.Ico,
        tenant.Dic,
        tenant.IcDph,
        tenant.Street,
        tenant.PostalCode,
        tenant.City,
        tenant.CountryCode.ToUpperInvariant(),
        tenant.BillingEmail,
        SnakeCaseEnumConverter<TaxIdStatus>.ToText(tenant.TaxIdStatus),
        tenant.TaxIdVerifiedAt,
        tenant.Currency is not null,
        !string.IsNullOrWhiteSpace(tenant.LegalName) && !string.IsNullOrWhiteSpace(tenant.Ico) && !string.IsNullOrWhiteSpace(tenant.Street)
            && !string.IsNullOrWhiteSpace(tenant.PostalCode) && !string.IsNullOrWhiteSpace(tenant.City) && !string.IsNullOrWhiteSpace(tenant.BillingEmail),
        tenant.Version);

    private sealed record Details(
        string LegalName, string Ico, string? Dic, string? IcDph, string Street, string PostalCode, string City, string CountryCode, string BillingEmail);

    private sealed record Saved(bool Changed, string? StripeCustomerId);
}
