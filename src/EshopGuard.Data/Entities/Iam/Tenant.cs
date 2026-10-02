using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Customer account (company); billing details. No RLS, access only through membership. Table <c>iam.tenants</c>.</summary>
public sealed class Tenant : GlobalEntity, ISoftDeletable
{
    public required string Name { get; set; }

    public string? LegalName { get; set; }

    public string? Ico { get; set; }

    public string? Dic { get; set; }

    public string? IcDph { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public required string CountryCode { get; set; }

    public string? BillingEmail { get; set; }

    public required string Locale { get; set; }

    public string? Currency { get; set; }

    public required string MarketCode { get; set; }

    public TenantStatus Status { get; set; }

    public string? StripeCustomerId { get; set; }

    public string? SuperfakturaClientId { get; set; }

    public PartnerKind? PartnerKind { get; set; }

    public DateTimeOffset? FounderUntil { get; set; }

    /// <summary>Verification of <see cref="IcDph"/> by Stripe (VIES), change 12.</summary>
    public TaxIdStatus TaxIdStatus { get; set; }

    public DateTimeOffset? TaxIdVerifiedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Version of the row for optimistic concurrency (PostgreSQL <c>xmin</c>, no column of its own).</summary>
    public uint Version { get; set; }
}
