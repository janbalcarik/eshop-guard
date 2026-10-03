using System.Net;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The billing details of the tenant (change 12, task 9.4; requirement „Fakturační údaje tenanta“, design BillingDetails 8b):
/// reading and saving over the version, required fields without a format of IČO, the country fixed after the first payment and
/// the VAT id at Stripe (the old tax ids go away before the save, the new one starts its verification).
/// </summary>
public sealed class BillingDetailsTests : ApiTestBase
{
    [Fact]
    public async Task Details_AreSaved_WithAnyIco_AndTheAuditHasOnlyTheNamesOfTheFields()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var empty = await GetAsync(owner);

        Assert.Equal((JsonValueKind.Null, "SK", "none", false, false), (empty.GetProperty("legalName").ValueKind, empty.GetProperty("countryCode").GetString(),
            empty.GetProperty("taxIdStatus").GetString(), empty.GetProperty("countryLocked").GetBoolean(), empty.GetProperty("complete").GetBoolean()));

        using var response = await owner.Browser.PutAsync(PathOf(owner), Body(VersionOf(empty), ico: "123", country: " sk ", dic: " "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await ApiClient.JsonAsync(response);
        var read = await GetAsync(owner);

        Assert.Equal(("Bylinkovo s.r.o.", "123", JsonValueKind.Null, "SK", "faktury@bylinkovo-test.sk", true),
            (read.GetProperty("legalName").GetString(), read.GetProperty("ico").GetString(), read.GetProperty("dic").ValueKind, read.GetProperty("countryCode").GetString(),
                read.GetProperty("billingEmail").GetString(), read.GetProperty("complete").GetBoolean()));
        Assert.Equal(VersionOf(saved), VersionOf(read));
        Assert.NotEqual(VersionOf(empty), VersionOf(read));
        var audit = Assert.Single(await AdminRowsAsync(
            "SELECT data->'fields' ? 'legalName', data->'fields' ? 'ico', data->'fields' ? 'dic', data::text LIKE '%Bylinkovo%', actor_user_id FROM ops.audit_log WHERE tenant_id = $1 AND action = 'tenant.billing_details_updated'",
            owner.TenantId));
        Assert.Equal(new object?[] { true, true, false, false, owner.UserId }, audit);
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task MissingFields_AndAnUnknownCountry_Are400()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        using var missing = await owner.Browser.PutAsync(PathOf(owner), new { legalName = " ", countryCode = "XX", billingEmail = "faktury" });
        var problem = await ApiClient.ProblemAsync(missing);

        Assert.Equal((HttpStatusCode.BadRequest, "validation.failed"), (problem.Status, problem.Code));
        var errors = problem.Body.GetProperty("errors");
        Assert.Equal(["billingEmail", "city", "countryCode", "ico", "legalName", "postalCode", "street", "version"],
            errors.EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
        Assert.Equal("value.not_allowed", errors.GetProperty("countryCode")[0].GetString());
        Assert.Null(await AdminScalarAsync<string>("SELECT legal_name FROM iam.tenants WHERE id = $1", owner.TenantId));
    }

    [Fact]
    public async Task AfterTheFirstPayment_TheCountryIsFixed_AndTheOtherFieldsChange()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        await PutAsync(owner, Body(VersionOf(await GetAsync(owner))));
        await AdminAsync("UPDATE iam.tenants SET currency = 'EUR' WHERE id = $1", owner.TenantId);
        var locked = await GetAsync(owner);

        using var other = await owner.Browser.PutAsync(PathOf(owner), Body(VersionOf(locked), country: "CZ"));
        var problem = await ApiClient.ProblemAsync(other);
        var same = await PutAsync(owner, Body(VersionOf(locked), city: "Košice"));

        Assert.True(locked.GetProperty("countryLocked").GetBoolean());
        Assert.Equal((HttpStatusCode.Conflict, "billing.country_locked"), (problem.Status, problem.Code));
        Assert.Equal(("SK", "Košice"), (same.GetProperty("countryCode").GetString(), same.GetProperty("city").GetString()));
    }

    [Fact]
    public async Task AnOldVersion_Is409_AndUnchangedDetailsWriteNothing()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var version = VersionOf(await GetAsync(owner));
        var first = await PutAsync(owner, Body(version));

        using var stale = await owner.Browser.PutAsync(PathOf(owner), Body(version, city: "Nitra"));
        var problem = await ApiClient.ProblemAsync(stale);
        var again = await PutAsync(owner, Body(VersionOf(first)));

        Assert.Equal((HttpStatusCode.Conflict, "concurrency.conflict"), (problem.Status, problem.Code));
        Assert.Equal(("Bratislava", VersionOf(first)), (again.GetProperty("city").GetString(), VersionOf(again)));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'tenant.billing_details_updated'", owner.TenantId));
    }

    [Fact]
    public async Task AVatId_CreatesTheCustomer_AndStartsTheVerification()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        var saved = await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ", icDph: "cz 1234 5678"));

        Assert.Equal(("CZ12345678", "pending"), (saved.GetProperty("icDph").GetString(), saved.GetProperty("taxIdStatus").GetString()));
        var customer = await AdminScalarAsync<string>("SELECT stripe_customer_id FROM iam.tenants WHERE id = $1", owner.TenantId);
        Assert.Equal("CZ12345678", Assert.Single(factory.Stripe.Customers[customer!].TaxIds).Value);
        Assert.Equal("Bylinkovo s.r.o.", factory.Stripe.CustomerRequests[customer!].Name);
    }

    [Fact]
    public async Task ANewVatId_ReplacesTheOldOneAtStripe_AndIsVerifiedAgain()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ", icDph: "CZ12345678"));
        await AdminAsync("UPDATE iam.tenants SET tax_id_status = 'verified', tax_id_verified_at = now() WHERE id = $1", owner.TenantId);
        var customer = await AdminScalarAsync<string>("SELECT stripe_customer_id FROM iam.tenants WHERE id = $1", owner.TenantId);

        var saved = await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ", icDph: "CZ87654321"));

        Assert.Equal(("CZ87654321", "pending", JsonValueKind.Null), (saved.GetProperty("icDph").GetString(), saved.GetProperty("taxIdStatus").GetString(),
            saved.GetProperty("taxIdVerifiedAt").ValueKind));
        Assert.Equal("CZ87654321", Assert.Single(factory.Stripe.Customers[customer!].TaxIds).Value);
        Assert.Equal((1, 2, 1), (factory.Stripe.Count("DeleteTaxIdAsync"), factory.Stripe.Count("CreateTaxIdAsync"), factory.Stripe.Count("CreateCustomerAsync")));
    }

    [Fact]
    public async Task ARemovedVatId_LeavesTheCustomerWithoutTaxIds()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ", icDph: "CZ12345678"));
        var customer = await AdminScalarAsync<string>("SELECT stripe_customer_id FROM iam.tenants WHERE id = $1", owner.TenantId);

        var saved = await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ"));

        Assert.Equal((JsonValueKind.Null, "none"), (saved.GetProperty("icDph").ValueKind, saved.GetProperty("taxIdStatus").GetString()));
        Assert.Empty(factory.Stripe.Customers[customer!].TaxIds);
        Assert.Equal(1, factory.Stripe.Count("CreateTaxIdAsync"));
    }

    [Fact]
    public async Task StripeUnavailable_KeepsTheOldVatId_ButOtherDetailsAreSaved()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        await PutAsync(owner, Body(VersionOf(await GetAsync(owner)), country: "CZ", icDph: "CZ12345678"));
        await AdminAsync("UPDATE iam.tenants SET tax_id_status = 'verified', tax_id_verified_at = now() WHERE id = $1", owner.TenantId);
        var version = VersionOf(await GetAsync(owner));

        factory.Stripe.FailNext = new("stripe.api_error", 500, true);
        using var vat = await owner.Browser.PutAsync(PathOf(owner), Body(version, country: "CZ", icDph: "CZ87654321"));
        var problem = await ApiClient.ProblemAsync(vat);
        factory.Stripe.FailNext = new("stripe.api_error", 500, true);
        var address = await PutAsync(owner, Body(version, country: "CZ", icDph: "CZ12345678", city: "Brno"));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "billing.unavailable"), (problem.Status, problem.Code));
        Assert.Equal(("CZ12345678", "verified", "Brno"), (address.GetProperty("icDph").GetString(), address.GetProperty("taxIdStatus").GetString(),
            address.GetProperty("city").GetString()));
        Assert.Equal(0, factory.Stripe.Count("DeleteTaxIdAsync"));
        Assert.Equal(2L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'tenant.billing_details_updated'", owner.TenantId));
    }

    private static string PathOf(Person person) => $"/api/t/{person.TenantId}/billing/details";

    private static uint VersionOf(JsonElement details) => details.GetProperty("version").GetUInt32();

    private static object Body(uint version, string ico = "12345678", string country = "SK", string? icDph = null, string? dic = "2020123456", string city = "Bratislava") => new
    {
        legalName = "Bylinkovo s.r.o.",
        ico,
        dic,
        icDph,
        street = "Hlavná 1",
        postalCode = "811 01",
        city,
        countryCode = country,
        billingEmail = "faktury@bylinkovo-test.sk",
        version,
    };

    private static async Task<JsonElement> GetAsync(Person person)
    {
        using var response = await person.Browser.GetAsync(PathOf(person));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiClient.JsonAsync(response);
    }

    private static async Task<JsonElement> PutAsync(Person person, object body)
    {
        using var response = await person.Browser.PutAsync(PathOf(person), body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }
}
