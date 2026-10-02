using System.Text;
using System.Text.Json;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Protocols;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Tests.Runs.Support;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Protocols;

/// <summary>
/// The job <c>protocol.render</c> (change 11, task 9.4): the stored content goes through the renderer into the PDF of the
/// protocol, which becomes <c>ready</c> with <c>protocol_ready</c>; without a renderer (K rozhodnutí 6 still open) it is
/// <c>failed</c> with <c>pdf_renderer_unavailable</c> and <c>protocol_failed</c>, nothing is hidden.
/// </summary>
public sealed class ProtocolRenderHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task WithARenderer_ThePdfIsStored_AndTheEditorsAreNotified()
    {
        var (shop, protocolId, storage) = await SeedAsync();
        await Workers.StartAsync("protocol", RunTests.WorkerSettings(storage), s =>
        {
            s.AddEshopGuardStorage();
            s.AddProtocolJobs();
            s.AddSingleton<IPdfRenderer, TextPdfRenderer>();
        });

        await EnqueueAsync(shop, protocolId);
        var (status, error, key) = await WaitAsync(shop, protocolId);

        Assert.Equal(("ready", null), (status, error));
        Assert.Equal(ProtocolJobs.PdfKey(shop.TenantId, shop.ShopId, "EG-2026-0142").Value, key);
        await using (var pdf = await new FileSystemBlobStore(storage).OpenReadAsync(ProtocolJobs.PdfKey(shop.TenantId, shop.ShopId, "EG-2026-0142"), Ct))
        {
            using var reader = new StreamReader(pdf!);
            Assert.StartsWith("%PDF-fake EG-2026-0142", await reader.ReadToEndAsync(Ct), StringComparison.Ordinal);
        }

        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM iam.notifications WHERE kind = 'protocol_ready' AND params->>'number' = 'EG-2026-0142'"));
    }

    [Fact]
    public async Task WithoutARenderer_TheProtocolFails_WithItsCode()
    {
        var (shop, protocolId, storage) = await SeedAsync();
        await Workers.StartAsync("protocol", RunTests.WorkerSettings(storage), s => s.AddEshopGuardStorage().AddProtocolJobs());

        await EnqueueAsync(shop, protocolId);
        var (status, error, key) = await WaitAsync(shop, protocolId);

        Assert.Equal(("failed", ProtocolRenderHandler.RendererUnavailable, null), (status, error, key));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM iam.notifications WHERE kind = 'protocol_failed' AND params->>'error_code' = 'pdf_renderer_unavailable'"));
    }

    /// <summary>The renderer of the tests: the number and the title as text (a PDF library is K rozhodnutí 6).</summary>
    private sealed class TextPdfRenderer : IPdfRenderer
    {
        public Task<byte[]> RenderAsync(ProtocolDocument document, CancellationToken ct) =>
            Task.FromResult(Encoding.UTF8.GetBytes($"%PDF-fake {document.Number} {document.Title}"));
    }

    private async Task<(RunShop Shop, Guid ProtocolId, string Storage)> SeedAsync()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-protocol-").FullName;
        var userId = Guid.CreateVersion7();
        var protocolId = Guid.CreateVersion7();
        var document = new ProtocolDocument("EG-2026-0142", "sk", "Protokol o kontrole", "Protokol o kontrole textov e-shopu", "Č. EG-2026-0142", "Vystavené 31. 10. 2026",
            [new ProtocolFact("shop", "E-shop", shop.Domain)], [new ProtocolSummaryItem("initial", 43, "nálezov pri úvodnej kontrole")],
            new ProtocolTable("Rozhodnutia a opravy", ["Dátum", "Stránky", "Pôvodne", "Riešenie", "Bod"], [], "Žiadne."),
            new ProtocolSection("Doklady prevádzkovateľa", [], "Žiadne."), "Nie je právnym posúdením.", "EG-2026-0142.pdf");
        await using (var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(document, ProtocolJobs.Json)))
        {
            await new FileSystemBlobStore(storage).PutAsync(ProtocolJobs.DocumentKey(shop.TenantId, shop.ShopId, "EG-2026-0142"), content, "application/json", Ct);
        }

        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH u AS (INSERT INTO iam.users (id, email, email_confirmed, access_failed_count, created_at, updated_at) VALUES ($1, $2, true, 0, now(), now()) RETURNING 1),
            m AS (INSERT INTO iam.memberships (tenant_id, user_id, role, created_at, updated_at) SELECT $3, $1, 'editor', now(), now() FROM u RETURNING 1),
            p AS (INSERT INTO fixes.protocols (id, tenant_id, shop_id, number, period_from, period_to, locale, generated_by, rule_set_ids, status, created_at, updated_at)
                SELECT $4, $3, $5, 'EG-2026-0142', '2026-09-30', '2026-10-31', 'sk', $1, '{}', 'rendering', now(), now() FROM m RETURNING 1)
            SELECT count(*)::int FROM p
            """, userId, $"jana-{userId:N}@example.invalid", shop.TenantId, protocolId, shop.ShopId);
        return (shop, protocolId, storage);
    }

    private async Task EnqueueAsync(RunShop shop, Guid protocolId)
    {
        await using var scope = (await Workers.StartAsync("enqueue", RunTests.WorkerSettings(Path.GetTempPath(), slots: 0), _ => { })).Host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        await db.ExecuteInTenantTransactionAsync(() =>
            queue.EnqueueAsync(ProtocolJobs.Render(shop.TenantId, shop.ShopId, protocolId), (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), Ct), Ct);
    }

    private async Task<(string Status, string? Error, string? Key)> WaitAsync(RunShop shop, Guid protocolId)
    {
        for (var i = 0; i < 300; i++)
        {
            var rows = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT status, error_code, pdf_blob_key FROM fixes.protocols WHERE id = $1", protocolId);
            if ((string)rows[0][0]! != "rendering")
            {
                return ((string)rows[0][0]!, (string?)rows[0][1], (string?)rows[0][2]);
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException(await Db.DumpJobsAsync());
    }
}
