using EshopGuard.Application.RateLimits;

namespace EshopGuard.Application.Shops;

/// <summary>
/// Limits of the e-shops in <c>ops.rate_limit_buckets</c> (change 10, AD 12, proposals of K rozhodnutí 11, not measured):
/// the keys carry only the id of the tenant or the e-shop.
/// </summary>
public static class ShopLimits
{
    public static readonly AuthLimit Create = new("shops:create:tenant:", 20, TimeSpan.FromHours(1), "tenant_hourly");
    public static readonly AuthLimit Sample = new("shops:sample:tenant:", 5, TimeSpan.FromDays(1), "tenant_daily");
    public static readonly AuthLimit Detect = new("shops:detect:shop:", 10, TimeSpan.FromHours(1), "shop_hourly");
    public static readonly AuthLimit Verify = new("shops:verify:shop:", 20, TimeSpan.FromHours(1), "shop_hourly");
}
