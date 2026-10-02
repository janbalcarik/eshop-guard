using System.Text.RegularExpressions;
using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>
/// Triggers <c>pg_notify</c> of change 11 (AD 13): an event of a run, a change of a run, a proposal, a group, a publication and
/// a question each send only ids (<c>tenant:run:event</c>, <c>tenant:shop:entity:id</c>), never a text of a page.
/// </summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed partial class NotifyTriggersTests(PostgresTestDatabase database, TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Notifications_CarryOnlyIds()
    {
        var a = tenants.A;
        var received = new List<(string Channel, string Payload)>();
        await using var listener = await database.For("Admin").OpenConnectionAsync(Ct);
        listener.Notification += (_, e) =>
        {
            lock (received)
            {
                received.Add((e.Channel, e.Payload));
            }
        };
        await using (var listen = new NpgsqlCommand("LISTEN eg_run; LISTEN eg_shop;", listener))
        {
            await listen.ExecuteNonQueryAsync(Ct);
        }

        var admin = database.For("Admin");
        await ExecuteAsync(admin, $"INSERT INTO checks.run_events (tenant_id, run_id, at, level, code, message, created_at) VALUES ('{a.Tenant.TenantId}', '{a.RunId}', now(), 'info', 'run.progress', 'Šampón s levanduľou', now())");
        await ExecuteAsync(admin, $"UPDATE checks.runs SET progress = jsonb_build_object('pages_fetched', 7) WHERE id = '{a.RunId}'");
        await ExecuteAsync(admin, $"UPDATE fixes.fix_proposals SET recheck_status = CASE recheck_status WHEN 'ok' THEN 'pending' ELSE 'ok' END WHERE id = '{a.FixProposalId}'");
        await ExecuteAsync(admin, $"UPDATE fixes.fix_groups SET status = CASE status WHEN 'draft' THEN 'needs_value' ELSE 'draft' END WHERE id = '{a.FixGroupId}'");
        await ExecuteAsync(admin, $"UPDATE checks.questions SET status = CASE status WHEN 'open' THEN 'answered' ELSE 'open' END WHERE tenant_id = '{a.Tenant.TenantId}'");
        await ExecuteAsync(admin, $"UPDATE fixes.publications SET status = 'failed' WHERE tenant_id = '{a.Tenant.TenantId}'");
        // A change of another column of a proposal says nothing.
        await ExecuteAsync(admin, $"UPDATE fixes.fix_proposals SET reason = 'iný dôvod' WHERE id = '{a.FixProposalId}'");

        for (var i = 0; i < 20 && Count(received) < 8; i++)
        {
            await listener.WaitAsync(TimeSpan.FromMilliseconds(250), Ct);
        }

        List<(string Channel, string Payload)> mine;
        lock (received)
        {
            mine = received.Where(r => r.Payload.StartsWith(a.Tenant.TenantId.ToString("D"), StringComparison.Ordinal)).ToList();
        }

        Assert.Contains(mine, r => r.Channel == "eg_run" && RunEvent().IsMatch(r.Payload) && r.Payload.Split(':')[1] == a.RunId.ToString("D") && r.Payload.Split(':')[2] != "0");
        Assert.Contains(mine, r => r.Channel == "eg_run" && r.Payload == $"{a.Tenant.TenantId:D}:{a.RunId:D}:0");
        foreach (var entity in new[] { "run", "proposal", "group", "question", "publication" })
        {
            Assert.Contains(mine, r => r.Channel == "eg_shop" && r.Payload.StartsWith($"{a.Tenant.TenantId:D}:{a.ShopId:D}:{entity}:", StringComparison.Ordinal));
        }

        Assert.Single(mine, r => r.Payload == $"{a.Tenant.TenantId:D}:{a.ShopId:D}:proposal:{a.FixProposalId:D}");
        Assert.All(mine, r => Assert.Matches(Payload(), r.Payload));
        Assert.DoesNotContain(mine, r => r.Payload.Contains("Šampón", StringComparison.Ordinal) || r.Payload.Contains("dôvod", StringComparison.Ordinal));
    }

    private static int Count(List<(string, string)> received)
    {
        lock (received)
        {
            return received.Count;
        }
    }

    private static async Task ExecuteAsync(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(Ct);
    }

    [GeneratedRegex("^[0-9a-f-]{36}:[0-9a-f-]{36}:[0-9]+$")]
    private static partial Regex RunEvent();

    [GeneratedRegex("^[0-9a-f-]{36}:[0-9a-f-]{36}:(([0-9]+)|((run|proposal|group|publication|question):[0-9a-f-]{36}))$")]
    private static partial Regex Payload();
}
