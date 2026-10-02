using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Version of the price list of a market. Table <c>billing.price_lists</c>.</summary>
public sealed class PriceList : GlobalEntity
{
    public required string Name { get; set; }

    public required string MarketCode { get; set; }

    public required string Currency { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public PriceListStatus Status { get; set; }

    public int NoticeDays { get; set; }

    /// <summary>Mode of Stripe of its prices and coupons; null until the first synchronization.</summary>
    public StripeMode? StripeMode { get; set; }

    public PriceListSyncStatus SyncStatus { get; set; }

    /// <summary>Code of the last error of the synchronization (no text of Stripe).</summary>
    public string? SyncError { get; set; }

    /// <summary>Other pages above this multiple of the counted products make an individual offer (K rozhodnutí 10).</summary>
    public decimal FairUseOtherPagesFactor { get; set; } = 2m;

    /// <summary>When the price list became the active one of its market (<c>ref.markets.price_list_id</c>).</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>The last preview of the impact on running subscriptions (computed by the worker).</summary>
    public System.Text.Json.JsonDocument? Impact { get; set; }

    public DateTimeOffset? ImpactAt { get; set; }
}
