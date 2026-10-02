using EshopGuard.Application.Shops;
using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Tests.Shops;

/// <summary>The state machine of an e-shop (change 10, task 1.5; AD 11): every pair of states against the table.</summary>
public sealed class ShopStatusTransitionsTests
{
    private static readonly HashSet<(ShopStatus, ShopStatus)> Allowed =
    [
        (ShopStatus.Draft, ShopStatus.Sample),
        (ShopStatus.Sample, ShopStatus.AwaitingPayment),
        (ShopStatus.AwaitingPayment, ShopStatus.Analyzing),
        (ShopStatus.Analyzing, ShopStatus.Active),
        (ShopStatus.Active, ShopStatus.Paused),
        (ShopStatus.Paused, ShopStatus.Active),
        (ShopStatus.Draft, ShopStatus.Canceled),
        (ShopStatus.Sample, ShopStatus.Canceled),
        (ShopStatus.AwaitingPayment, ShopStatus.Canceled),
        (ShopStatus.Analyzing, ShopStatus.Canceled),
        (ShopStatus.Active, ShopStatus.Canceled),
        (ShopStatus.Paused, ShopStatus.Canceled),
    ];

    public static TheoryData<ShopStatus, ShopStatus> AllPairs()
    {
        var data = new TheoryData<ShopStatus, ShopStatus>();
        foreach (var from in Enum.GetValues<ShopStatus>())
        {
            foreach (var to in Enum.GetValues<ShopStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_FollowsTheTable(ShopStatus from, ShopStatus to) =>
        Assert.Equal(Allowed.Contains((from, to)), ShopStatusTransitions.IsAllowed(from, to));

    [Fact]
    public void OnlyAFinishedSample_CanBeOrdered() =>
        Assert.Equal([ShopStatus.Sample], Enum.GetValues<ShopStatus>().Where(ShopStatusTransitions.IsOrderable));
}
