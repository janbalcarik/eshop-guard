using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The state machine of an e-shop (change 10, AD 11; shared with the payments of change 12): <c>draft → sample</c> (the free
/// sample), <c>sample → awaiting_payment → analyzing → active</c>, <c>active ↔ paused</c> and every state except itself
/// to <c>canceled</c>. Anything else is refused.
/// </summary>
public static class ShopStatusTransitions
{
    private static readonly HashSet<(ShopStatus From, ShopStatus To)> Allowed =
    [
        (ShopStatus.Draft, ShopStatus.Sample),
        (ShopStatus.Sample, ShopStatus.AwaitingPayment),
        (ShopStatus.AwaitingPayment, ShopStatus.Analyzing),
        (ShopStatus.Analyzing, ShopStatus.Active),
        (ShopStatus.Active, ShopStatus.Paused),
        (ShopStatus.Paused, ShopStatus.Active),
    ];

    public static bool IsAllowed(ShopStatus from, ShopStatus to) =>
        Allowed.Contains((from, to)) || (to == ShopStatus.Canceled && from != ShopStatus.Canceled);

    /// <summary>The state an e-shop can be ordered from (change 12): after the free sample (<c>sample → awaiting_payment</c>).</summary>
    public static bool IsOrderable(ShopStatus status) => IsAllowed(status, ShopStatus.AwaitingPayment);
}
