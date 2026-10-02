using System.Text.Json.Nodes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Npgsql;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Events of runs (design of change 8, task 10.4): notified only after the commit, and never with a text, price or provider;
/// the logs of a run never quote a page (task 12.6).
/// </summary>
public sealed class RunEventsTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task Notification_ArrivesOnlyAfterTheCommit_AndNeverAfterARollback()
    {
        var shop = await RunTests.CreateShopAsync();
        var runId = Guid.CreateVersion7();
        await using (var connection = await Db.For("Owner").OpenConnectionAsync(Ct))
        await using (var transaction = await TenantSql.BeginAsync(connection, shop.TenantId, ct: Ct))
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, created_at, updated_at) VALUES ($1, $2, $3, 'free_sample', 'user', 'queued', 0, now(), now())",
                connection, transaction)
            {
                Parameters = { new NpgsqlParameter { Value = runId }, new NpgsqlParameter { Value = shop.TenantId }, new NpgsqlParameter { Value = shop.ShopId } },
            };
            await insert.ExecuteNonQueryAsync(Ct);
            await transaction.CommitAsync(Ct);
        }

        var received = new List<string>();
        await using var listener = await Db.For("Worker").OpenConnectionAsync(Ct);
        listener.Notification += (_, e) => { lock (received) { received.Add(e.Payload); } };
        await using (var listen = new NpgsqlCommand("LISTEN run_events", listener))
        {
            await listen.ExecuteNonQueryAsync(Ct);
        }

        await using var writer = await Db.For("Worker").OpenConnectionAsync(Ct);
        await using (var rolledBack = await TenantSql.BeginAsync(writer, shop.TenantId, ct: Ct))
        {
            await RunEventWriter.WriteAsync(writer, rolledBack, shop.TenantId, runId, "info", RunCodes.EventProgress, new JsonObject { ["step"] = "fetch" }, Ct);
            await rolledBack.RollbackAsync(Ct);
        }

        await using (var committed = await TenantSql.BeginAsync(writer, shop.TenantId, ct: Ct))
        {
            await RunEventWriter.WriteAsync(writer, committed, shop.TenantId, runId, "info", RunCodes.EventProgress, new JsonObject { ["step"] = "fetch" }, Ct);
            await listener.WaitAsync(TimeSpan.FromMilliseconds(300), Ct);
            Assert.Empty(received);
            await committed.CommitAsync(Ct);
        }

        await listener.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal([runId.ToString("D")], received);
    }

    [Fact]
    public async Task EventsOfARun_HaveNoTextOfPagesNoPriceAndNoProvider()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-events-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(90), [worker], RunTests.Final);

        var events = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT code, coalesce(data::text, ''), coalesce(message, '') FROM checks.run_events WHERE run_id = $1", runId);
        var texts = (await RunTests.RowsAsync(Db, shop.TenantId, "SELECT text FROM checks.findings WHERE first_run_id = $1 AND text IS NOT NULL", runId)).Select(r => (string)r[0]!).ToList();

        Assert.NotEmpty(events);
        Assert.NotEmpty(texts);
        foreach (var row in events)
        {
            var all = $"{row[0]} {row[1]} {row[2]}";
            Assert.DoesNotContain("usd", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("jev", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("typesafe", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("openai", all, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, (string)row[2]!);
            Assert.DoesNotContain(texts, t => all.Contains(t, StringComparison.Ordinal));
        }

        // Logs of the whole run (task 12.6): no sentence of the pages, and the handlers of the run name tenant, run and job.
        var logs = worker.Logs.Logs.Select(l => l.AllText).ToList();
        Assert.DoesNotContain(logs, l => texts.Any(t => t.Length > 20 && l.Contains(t, StringComparison.Ordinal)));
        Assert.Contains(logs, l => l.Contains("run.finished", StringComparison.Ordinal) && l.Contains(runId.ToString(), StringComparison.Ordinal)
            && l.Contains(shop.TenantId.ToString(), StringComparison.Ordinal));
    }
}
