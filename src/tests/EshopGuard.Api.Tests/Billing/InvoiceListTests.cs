using System.IO.Compression;
using System.Net;
using System.Text.Json;
using EshopGuard.Api.Tests.Shops;
using static EshopGuard.Api.Tests.Billing.InvoiceSeed;
using static EshopGuard.Api.Tests.Billing.OrderFlowTests;
using static EshopGuard.Api.Tests.Billing.SubscriptionFlowTests;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The invoices of the tenant (change 12, tasks 11.1–11.3; requirement „Seznam faktur a export ZIP“): the list by e-shop and local
/// year with the scheduled payments of the running subscriptions, the PDF only through a short signed link, the ZIP of the issued
/// documents with the count of the skipped ones.
/// </summary>
public sealed class InvoiceListTests : ShopTestBase
{
    [Fact]
    public async Task List_ByShopAndYear_HasOnlyTheDocumentsOfTheShop_AndTheScheduledPaymentOnlyInItsYear()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var tenant = ready.Owner.TenantId;
        var bylinkovo = ready.ShopId;
        var domain = await AdminScalarAsync<string>("SELECT domain FROM shop.shops WHERE id = $1", bylinkovo);
        var darceky = await ShopRowAsync(tenant, "darceky", 200, "t500");
        var czech = await ShopRowAsync(tenant, "bylinkovocz", 1460, "t2000");
        var order = await OrderIdAsync(ready);
        var payment = await AdminScalarAsync<Guid>(
            "INSERT INTO billing.payments (tenant_id, order_id, amount_gross, currency, status, paid_at) VALUES ($1, $2, 244.77, 'EUR', 'succeeded', $3) RETURNING id",
            tenant, order, At(2026, 10, 5));
        var analysisPrice = $"price_{ready.PriceListId.ToString("N")[..8]}_t20000_analysis";

        // 18 documents of three e-shops in 2026 and 2027.
        await DocumentAsync(factory, tenant, bylinkovo, "2026000101", At(2026, 10, 5), 199m, paymentId: payment, items: Lines((analysisPrice, At(2026, 10, 5), At(2026, 10, 5))));
        await DocumentAsync(factory, tenant, bylinkovo, "2026000102", At(2026, 11, 5), 59m, items: Monthly(At(2026, 11, 5)));
        await DocumentAsync(factory, tenant, bylinkovo, "2026000103", At(2026, 12, 5), 59m, items: Monthly(At(2026, 12, 5)));
        await DocumentAsync(factory, tenant, bylinkovo, "2027000101", At(2027, 1, 5), 59m, items: Monthly(At(2027, 1, 5)));
        var february = await DocumentAsync(factory, tenant, bylinkovo, "2027000102", At(2027, 2, 5), 59m, items: Monthly(At(2027, 2, 5)));
        await DocumentAsync(factory, tenant, bylinkovo, "2027000103", At(2027, 2, 10), 59m, kind: "credit_note", creditNoteFor: february);
        var newYearsEve = new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);
        var gifts = new[] { At(2026, 10, 10), At(2026, 11, 10), newYearsEve, At(2027, 1, 10), At(2027, 2, 10), At(2027, 3, 10) };
        for (var i = 0; i < gifts.Length; i++)
        {
            await DocumentAsync(factory, tenant, darceky, $"D{i + 1:000}", gifts[i], 9m, items: Monthly(gifts[i]));
        }

        for (var i = 0; i < 6; i++)
        {
            var at = At(2026, 10, 15).AddMonths(i);
            await DocumentAsync(factory, tenant, czech, $"C{i + 1:000}", at, 19m, items: Monthly(at));
        }

        await SubscribedAsync(factory, tenant, bylinkovo, ready.PriceListId, "t20000", 59m, "active", At(2027, 3, 5), ordinal: 1, orderId: order);
        await SubscribedAsync(factory, tenant, czech, ready.PriceListId, "t2000", 19m, "trialing", At(2026, 12, 20), At(2026, 12, 20), ordinal: 2);

        var shop2026 = await ListAsync(ready.Owner, $"?shopId={bylinkovo}&year=2026");

        var rows = shop2026.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["2026000103", "2026000102", "2026000101"], rows.Select(r => r.GetProperty("number").GetString()));
        Assert.All(rows, r => Assert.Equal((bylinkovo, domain, "EUR", true), (r.GetProperty("shopId").GetGuid(), r.GetProperty("shopDomain").GetString(),
            r.GetProperty("currency").GetString(), r.GetProperty("hasPdf").GetBoolean())));
        Assert.Equal([2027, 2026], shop2026.GetProperty("years").EnumerateArray().Select(y => y.GetInt32()));
        Assert.Equal(("invoice", "paid", "monitoring", "2026-12-05", "2026-12-05", "2027-01-04", 59m, 72.57m), Row(rows[0]));
        Assert.Equal(("invoice", "paid", "analysis", "2026-10-05", null, null, 199m, 244.77m), Row(rows[2]));

        var shop2027 = (await ListAsync(ready.Owner, $"?shopId={bylinkovo}&year=2027")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([null, "2027000103", "2027000102", "2027000101"], shop2027.Select(r => r.GetProperty("number").GetString()));
        var scheduled = shop2027[0];
        Assert.Equal(("scheduled", "scheduled", "monitoring", "2027-03-05", "2027-03-05", "2027-04-04", 59m, 72.57m), Row(scheduled));
        Assert.Equal((JsonValueKind.Null, false), (scheduled.GetProperty("id").ValueKind, scheduled.GetProperty("hasPdf").GetBoolean()));
        Assert.Equal(("credit_note", "refunded", "monitoring"), (shop2027[1].GetProperty("kind").GetString(), shop2027[1].GetProperty("status").GetString(),
            shop2027[1].GetProperty("item").GetString()));

        var all2026 = (await ListAsync(ready.Owner, "?year=2026")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            ["2026-12-20", "2026-12-15", "2026-12-05", "2026-11-15", "2026-11-10", "2026-11-05", "2026-10-15", "2026-10-10", "2026-10-05"],
            all2026.Select(r => r.GetProperty("date").GetString()));
        Assert.Equal(("scheduled", czech, 19m, 23.37m), (all2026[0].GetProperty("kind").GetString(), all2026[0].GetProperty("shopId").GetGuid(),
            all2026[0].GetProperty("amountNet").GetDecimal(), all2026[0].GetProperty("amountGross").GetDecimal()));
        var all2027 = (await ListAsync(ready.Owner, "?year=2027")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(11, all2027.Count);
        Assert.Contains(all2027, r => r.GetProperty("number").GetString() == "D003" && r.GetProperty("date").GetString() == "2027-01-01");
        Assert.Equal(20, (await ListAsync(ready.Owner, string.Empty)).GetProperty("items").GetArrayLength());

        using var foreign = await ready.Owner.Browser.GetAsync($"/api/t/{tenant}/invoices?shopId={Guid.NewGuid()}");
        var problem = await ApiClient.ProblemAsync(foreign);
        Assert.Equal((HttpStatusCode.NotFound, "shop.not_found"), (problem.Status, problem.Code));
    }

    [Fact]
    public async Task Pdf_IsGivenOnlyThroughAShortSignedLink_ForAnIssuedDocumentOfTheTenant()
    {
        await using var factory = Factory();
        using var a = await People.OwnerAsync(factory);
        using var b = await People.OwnerAsync(factory);
        using var editor = await People.MemberAsync(factory, a, "editor");
        var shopA = await ShopRowAsync(a.TenantId, "bylinkovo", 100, "t500");
        var shopB = await ShopRowAsync(b.TenantId, "bylinkovo", 100, "t500");
        var issued = await DocumentAsync(factory, a.TenantId, shopA, "EG/2026/0042", At(2026, 10, 5), 59m);
        var review = await DocumentAsync(factory, a.TenantId, shopA, null, At(2026, 10, 6), 59m, status: "needs_review");
        var ofB = await DocumentAsync(factory, b.TenantId, shopB, "2026000001", At(2026, 10, 5), 59m);

        using var redirect = await a.Browser.GetAsync($"/api/t/{a.TenantId}/invoices/{issued}/pdf");

        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        var location = redirect.Headers.Location!.OriginalString;
        Assert.StartsWith("/api/files/", location, StringComparison.Ordinal);
        using var file = await a.Browser.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(PdfOf("EG/2026/0042"), await file.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal(("application/pdf", "EG_2026_0042.pdf"), (file.Content.Headers.ContentType?.MediaType, file.Content.Headers.ContentDisposition?.FileName?.Trim('"')));

        using var pending = await a.Browser.GetAsync($"/api/t/{a.TenantId}/invoices/{review}/pdf");
        await ProblemAsync(pending, HttpStatusCode.Conflict, "billing.invoice_pdf_missing");
        using var foreign = await a.Browser.GetAsync($"/api/t/{a.TenantId}/invoices/{ofB}/pdf");
        await ProblemAsync(foreign, HttpStatusCode.NotFound, "billing.invoice_not_found");
        using var list = await editor.Browser.GetAsync($"/api/t/{a.TenantId}/invoices");
        await ProblemAsync(list, HttpStatusCode.Forbidden, "auth.forbidden_role");
        // The role is checked before the document, so an editor learns nothing about any document.
        using var editorPdf = await editor.Browser.GetAsync($"/api/t/{a.TenantId}/invoices/{ofB}/pdf");
        await ProblemAsync(editorPdf, HttpStatusCode.Forbidden, "auth.forbidden_role");
        using var own = await b.Browser.GetAsync($"/api/t/{b.TenantId}/invoices/{ofB}/pdf");
        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
    }

    [Fact]
    public async Task Zip_HasThePdfsOfTheIssuedDocumentsOfTheYear_NamedByNumber_AndCountsTheSkippedOne()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var first = await ShopRowAsync(owner.TenantId, "bylinkovo", 100, "t500");
        var second = await ShopRowAsync(owner.TenantId, "darceky", 100, "t500");
        var secondDomain = await AdminScalarAsync<string>("SELECT domain FROM shop.shops WHERE id = $1", second);
        string[] numbers = ["2026000001", "2026000002", "2026000003", "2026000004", "EG/2026/0005", "2026000006"];
        for (var i = 0; i < numbers.Length; i++)
        {
            await DocumentAsync(factory, owner.TenantId, i < 4 ? first : second, numbers[i], At(2026, 7 + i, 5), 59m);
        }

        await DocumentAsync(factory, owner.TenantId, second, null, At(2026, 12, 6), 59m, status: "needs_review");
        await DocumentAsync(factory, owner.TenantId, first, "2027000001", At(2027, 1, 5), 59m);

        using var response = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invoices/zip?year=2026");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("1", Assert.Single(response.Headers.GetValues("X-EshopGuard-Skipped")));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal("invoices-2026.zip", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var entries = await EntriesAsync(response);
        Assert.Equal(numbers.Select(n => n.Replace('/', '_') + ".pdf").Order(StringComparer.Ordinal), entries.Keys.Order(StringComparer.Ordinal));
        Assert.All(numbers, n => Assert.Equal(PdfOf(n), entries[n.Replace('/', '_') + ".pdf"]));

        using var byShop = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invoices/zip?shopId={second}&year=2026");
        Assert.Equal("1", Assert.Single(byShop.Headers.GetValues("X-EshopGuard-Skipped")));
        Assert.Equal($"invoices-{secondDomain}-2026.zip", byShop.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(["2026000006.pdf", "EG_2026_0005.pdf"], (await EntriesAsync(byShop)).Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Zip_OfAnIssuedDocumentWhoseFileIsGone_SkipsIt_AndOverTheCapIsRefused()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shop = await ShopRowAsync(owner.TenantId, "bylinkovo", 100, "t500");
        await DocumentAsync(factory, owner.TenantId, shop, "2026000001", At(2026, 10, 5), 59m);
        await DocumentAsync(factory, owner.TenantId, shop, "2026000002", At(2026, 11, 5), 59m, file: false);

        using var gone = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invoices/zip");
        Assert.Equal(HttpStatusCode.OK, gone.StatusCode);
        Assert.Equal("1", Assert.Single(gone.Headers.GetValues("X-EshopGuard-Skipped")));
        Assert.Equal(["2026000001.pdf"], (await EntriesAsync(gone)).Keys);

        await AdminAsync(
            """
            INSERT INTO billing.invoices (id, tenant_id, shop_id, kind, buyer, items, amount_net, vat_amount, amount_gross, currency, status, number, issued_at,
                pdf_blob_key, einvoice_status)
            SELECT g.id, $1, $2, 'invoice', '{}'::jsonb, '[]'::jsonb, 9, 2.07, 11.07, 'EUR', 'issued', 'CAP' || g.n, '2025-06-01 08:00:00+00',
                'tenants/' || $1::text || '/invoices/' || g.id::text || '.pdf', 'not_required'
            FROM (SELECT gen_random_uuid() AS id, n FROM generate_series(1, 499) AS n) g
            """, owner.TenantId, shop);

        using var tooLarge = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invoices/zip");
        await ProblemAsync(tooLarge, (HttpStatusCode)422, "billing.zip_too_large");
        using var narrower = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invoices/zip?year=2026");
        Assert.Equal(HttpStatusCode.OK, narrower.StatusCode);
    }

    private static async Task<JsonElement> ListAsync(Person person, string query)
    {
        using var response = await person.Browser.GetAsync($"/api/t/{person.TenantId}/invoices{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }

    private static (string?, string?, string?, string?, string?, string?, decimal?, decimal?) Row(JsonElement row) => (
        row.GetProperty("kind").GetString(), row.GetProperty("status").GetString(), row.GetProperty("item").GetString(), row.GetProperty("date").GetString(),
        row.GetProperty("periodFrom").GetString(), row.GetProperty("periodTo").GetString(), Number(row.GetProperty("amountNet")), Number(row.GetProperty("amountGross")));

    private static decimal? Number(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((status, code), (problem.Status, problem.Code));
        Assert.Null(response.Headers.Location);
    }

    private static async Task<Dictionary<string, byte[]>> EntriesAsync(HttpResponseMessage response)
    {
        using var content = new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct));
        using var zip = new ZipArchive(content, ZipArchiveMode.Read);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var data = entry.Open();
            using var copy = new MemoryStream();
            data.CopyTo(copy);
            entries[entry.FullName] = copy.ToArray();
        }

        return entries;
    }
}
