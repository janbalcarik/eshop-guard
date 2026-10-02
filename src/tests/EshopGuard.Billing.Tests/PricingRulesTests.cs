using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Tax;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;

namespace EshopGuard.Billing.Tests;

/// <summary>Tiers, fair use, discounts, periods and tax treatment (change 12, tasks 3.1, 4.2, 8.4 and 9.1).</summary>
public sealed class PricingRulesTests
{
    private static readonly List<PriceTier> Tiers = BillingData.Tiers.Select(t => new PriceTier
    {
        Code = t.Code, MinProducts = t.Min, MaxProducts = t.Max, AnalysisPrice = t.Analysis, MonitoringMonthly = t.Monthly, MonitoringYearly = t.Yearly,
    }).ToList();

    [Theory]
    [InlineData(0, "t500", false)]
    [InlineData(1, "t500", false)]
    [InlineData(500, "t500", false)]
    [InlineData(501, "t2000", false)]
    [InlineData(5834, "t20000", false)]
    [InlineData(20000, "t20000", false)]
    [InlineData(20001, "custom", true)]
    [InlineData(1_000_000, "custom", true)]
    public void TierResolver_BoundsIncludeBothEnds(int count, string tier, bool custom)
    {
        var resolution = TierResolver.Resolve(Tiers, count);

        Assert.Equal(tier, resolution.Code);
        Assert.Equal(custom, resolution.IsCustom);
    }

    [Fact]
    public void TierResolver_AboveTheLastBoundWithoutACustomTier_IsCustom()
    {
        var resolution = TierResolver.Resolve(Tiers.Take(4).ToList(), 25_000);

        Assert.True(resolution.IsCustom);
        Assert.Equal("custom", resolution.Code);
    }

    [Theory]
    [InlineData(null, 0, 500, 501, "ok")]
    [InlineData(null, 1, 500, 501, "first_not_zero")]
    [InlineData(null, 0, 500, 502, "gap_or_overlap")]
    [InlineData(null, 0, 500, 500, "gap_or_overlap")]
    [InlineData(10, 0, 500, 501, "price_incomplete")]
    public void TierProblem_FindsGapsOverlapsAndHalfPrices(int? onlyAnalysis, int firstMin, int firstMax, int secondMin, string expected)
    {
        var tiers = new List<PriceTier>
        {
            new() { Code = "a", MinProducts = firstMin, MaxProducts = firstMax, AnalysisPrice = onlyAnalysis ?? 10, MonitoringMonthly = onlyAnalysis is null ? 5 : null },
            new() { Code = "b", MinProducts = secondMin, MaxProducts = null, AnalysisPrice = 20, MonitoringMonthly = 9 },
        };

        Assert.Equal(expected, TierResolver.Problem(tiers) ?? "ok");
    }

    [Fact]
    public void TierProblem_CustomOnlyLast_AndDefaultTiersAreValid()
    {
        Assert.Null(TierResolver.Problem(Tiers));
        var customFirst = new List<PriceTier>
        {
            new() { Code = "custom", MinProducts = 0, MaxProducts = 10 },
            new() { Code = "b", MinProducts = 11, AnalysisPrice = 1, MonitoringMonthly = 1 },
        };
        Assert.Equal("custom_not_last", TierResolver.Problem(customFirst));
    }

    [Theory]
    [InlineData(300, 600, false)]
    [InlineData(300, 601, true)]
    [InlineData(0, 2, false)]
    [InlineData(0, 3, true)]
    public void FairUse_TwiceTheProductsIsStillFine(int products, int otherPages, bool exceeded)
    {
        var (limit, isExceeded) = FairUsePolicy.Evaluate("products", products, otherPages, 2m);

        Assert.Equal(exceeded, isExceeded);
        Assert.Equal(Math.Max(products, 1) * 2, limit);
    }

    [Fact]
    public void FairUse_ByPagesToCheck_HasNoLimit()
    {
        Assert.Equal((null, false), FairUsePolicy.Evaluate("pages", 300, 100_000, 2m));
    }

    [Fact]
    public void VolumeDiscount_FromTheThirdShop()
    {
        var discounts = new List<VolumeDiscount> { new() { FromShopNumber = 3, Percent = 10 }, new() { FromShopNumber = 10, Percent = 20 } };

        Assert.Null(VolumeDiscountResolver.Resolve(discounts, 2));
        Assert.Equal(10, VolumeDiscountResolver.Resolve(discounts, 3)!.Percent);
        Assert.Equal(20, VolumeDiscountResolver.Resolve(discounts, 11)!.Percent);
        Assert.Null(VolumeDiscountResolver.Resolve([], 5));
    }

    [Fact]
    public void Trial_ToTheSameDayOfTheNextMonth_Or30Days()
    {
        var paid = new DateTimeOffset(2027, 1, 31, 14, 5, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2027, 2, 28, 14, 5, 0, TimeSpan.Zero), BillingPeriods.TrialEnd(paid, TrialModes.CalendarMonth));
        Assert.Equal(new DateTimeOffset(2027, 3, 2, 14, 5, 0, TimeSpan.Zero), BillingPeriods.TrialEnd(paid, TrialModes.Days30));
    }

    [Fact]
    public void FirstPeriodStart_KeepsTheDayOfTheAnchor()
    {
        var anchor = new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero),
            BillingPeriods.FirstStartOnOrAfter(anchor, new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero),
            BillingPeriods.FirstStartOnOrAfter(anchor, new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("SK", null, TaxIdStatus.None, TaxTreatment.DomesticVat)]
    [InlineData("SK", "SK2020123456", TaxIdStatus.Verified, TaxTreatment.DomesticVat)]
    [InlineData("CZ", "CZ12345678", TaxIdStatus.Verified, TaxTreatment.ReverseCharge)]
    [InlineData("CZ", "CZ12345678", TaxIdStatus.Pending, TaxTreatment.PendingVerification)]
    [InlineData("CZ", "CZ12345678", TaxIdStatus.Unverified, TaxTreatment.Undetermined)]
    [InlineData("CZ", null, TaxIdStatus.None, TaxTreatment.Undetermined)]
    [InlineData("CH", "CHE123456789", TaxIdStatus.Verified, TaxTreatment.Undetermined)]
    public void TaxTreatment_ByCountryAndVerifiedVatId(string country, string? icDph, TaxIdStatus status, TaxTreatment expected)
    {
        Assert.Equal(expected, TaxTreatmentResolver.Resolve(new TaxBuyer(country, "12345678", icDph, status), BillingOptionsTests.Tax()));
    }

    [Fact]
    public void TaxTreatment_RefusesPendingAndUndetermined()
    {
        Assert.Equal(BillingCodes.TaxIdPending, TaxTreatmentResolver.RefusalCode(TaxTreatment.PendingVerification));
        Assert.Equal(BillingCodes.TaxTreatmentUndetermined, TaxTreatmentResolver.RefusalCode(TaxTreatment.Undetermined));
        Assert.Null(TaxTreatmentResolver.RefusalCode(TaxTreatment.ReverseCharge));
        Assert.Null(TaxTreatmentResolver.RefusalCode(TaxTreatment.DomesticVat));
    }

    [Fact]
    public void VatPreview_DomesticWithRate_ReverseChargeOnlyTheTreatment()
    {
        var domestic = TaxTreatmentResolver.Preview(TaxTreatment.DomesticVat, 199m, 59m, BillingOptionsTests.Tax());
        var reverse = TaxTreatmentResolver.Preview(TaxTreatment.ReverseCharge, 199m, 59m, BillingOptionsTests.Tax());

        Assert.Equal(244.77m, domestic["analysisGross"]!.GetValue<decimal>());
        Assert.Equal(72.57m, domestic["monitoringGross"]!.GetValue<decimal>());
        Assert.Equal("reverse_charge", reverse["treatment"]!.GetValue<string>());
        Assert.Null(reverse["rate"]);
    }
}
