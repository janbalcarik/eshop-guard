using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using EshopGuard.Application.Email;
using EshopGuard.Application.Localization;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Outbox;
using EshopGuard.Jobs.Queue;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EshopGuard.Worker.Tests;

/// <summary>
/// E-mails of the outbox sent by the job <c>email.send</c> of the worker (change 9, task 3.7): two workers never send one row
/// twice, a failure counts <c>attempts</c> and is retried after the backoff, a template with a token never leaves the outbox.
/// </summary>
[Trait("Category", "Db")]
public sealed class OutboxEmailDispatcherTests : IAsyncLifetime
{
    private readonly InMemoryLoggerProvider _logs = new();
    private readonly CapturingTransport _transport = new();
    private readonly List<IHost> _hosts = [];
    private Guid _tenant;
    private Guid _inviter;
    private Guid _member;
    private Guid _run;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await TestDatabase.EnsureMigratedAsync(Ct);
        _tenant = Guid.CreateVersion7();
        _inviter = Guid.CreateVersion7();
        _member = Guid.CreateVersion7();
        _run = Guid.CreateVersion7();
        var shop = Guid.CreateVersion7();
        await AdminAsync("INSERT INTO iam.tenants (id, name, country_code, locale, market_code, status) VALUES ($1, 'Bylinkovo', 'SK', 'sk', 'sk', 'active')", _tenant);
        await AdminAsync("INSERT INTO iam.users (id, email, email_confirmed, locale, access_failed_count) VALUES ($1, $2, true, 'cs', 0)", _inviter, $"jana.{_inviter:N}@bylinkovo-test.sk");
        await AdminAsync("INSERT INTO iam.users (id, email, email_confirmed, access_failed_count) VALUES ($1, $2, true, 0)", _member, $"peter.{_member:N}@bylinkovo-test.sk");
        await AdminAsync("INSERT INTO iam.memberships (tenant_id, user_id, role, created_at, updated_at) VALUES ($1, $2, 'owner', now(), now()), ($1, $3, 'editor', now(), now())", _tenant, _inviter, _member);
        await AdminAsync(
            "INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status) VALUES ($1, $2, 'vegis.sk', 'https://vegis.sk/', '/', 'SK', 'unknown', 'web', 'active')",
            shop, _tenant);
        await AdminAsync(
            "INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, stats) VALUES ($1, $2, $3, 'full_analysis', 'user', 'partial', 1, $4::jsonb)",
            _run, _tenant, shop, """{"pages_checked": 120, "findings_by_severity": {"high": 2, "low": 3}, "unchecked": {"robots_blocked": 4, "not_loaded": 1}}""");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
    }

    [Fact]
    public async Task TwoWorkers_SendEveryRowOnce_InTheLanguageOfTheRecipient()
    {
        var first = await StartAsync("mail-a");
        await StartAsync("mail-b");
        var ids = new List<long>();
        for (var i = 0; i < 6; i++)
        {
            ids.Add(await AddAsync(first, new OutboxEmail(EmailTemplates.RunPartial, _inviter, null, null, RunParams())));
            ids.Add(await AddAsync(first, new OutboxEmail(EmailTemplates.InvitationAccepted, _inviter, null, null,
                new JsonObject { ["invitation_id"] = Guid.NewGuid().ToString("D"), ["member_user_id"] = _member.ToString("D") })));
        }

        await WaitAsync(async () => await AdminScalarAsync<long>("SELECT count(*) FROM ops.outbox WHERE id = ANY($1) AND sent_at IS NOT NULL", ids.ToArray()) == ids.Count);

        var mine = _transport.Sent.Where(m => m.To.StartsWith("jana." + _inviter.ToString("N"), StringComparison.Ordinal)).ToList();
        Assert.Equal(ids.Count, mine.Count);
        Assert.All(mine, m => Assert.Equal("cs", m.Locale));
        var partial = mine.First(m => m.Kind == EmailTemplates.RunPartial);
        Assert.Contains("Zkontrolované stránky: 120. Nezkontrolované stránky: 5. Nálezy: 5.", partial.Text, StringComparison.Ordinal);
        Assert.Contains($"https://app.eshopguard.test/app/{_tenant:D}/behy/{_run:D}", partial.Text, StringComparison.Ordinal);
        Assert.Contains("vegis.sk", partial.Subject, StringComparison.Ordinal);
        var accepted = mine.First(m => m.Kind == EmailTemplates.InvitationAccepted);
        Assert.Contains("peter." + _member.ToString("N"), accepted.Text, StringComparison.Ordinal);
        Assert.Contains("s rolí editor", accepted.Text, StringComparison.Ordinal);
        Assert.Equal(ids.Count, (int)await AdminScalarAsync<long>("SELECT sum(attempts) FROM ops.outbox WHERE id = ANY($1)", ids.ToArray()));
    }

    [Fact]
    public async Task FailedSend_CountsTheAttempt_AndIsRetriedAfterTheBackoff()
    {
        var host = await StartAsync("mail-retry");
        _transport.FailuresLeft = 1;

        var id = await AddAsync(host, new OutboxEmail(EmailTemplates.RunFinished, _member, null, null, RunParams()));

        await WaitAsync(async () => await AdminScalarAsync<DateTime?>("SELECT sent_at FROM ops.outbox WHERE id = $1", id) is not null);
        var row = (await AdminRowsAsync("SELECT attempts, error FROM ops.outbox WHERE id = $1", id)).Single();
        Assert.Equal(2, row[0]);
        Assert.Null(row[1]);
        var message = _transport.Sent.Single(m => m.To.StartsWith("peter." + _member.ToString("N"), StringComparison.Ordinal));
        Assert.Equal("sk", message.Locale); // no language of his own: the tenant's
        Assert.Equal(2, await AdminScalarAsync<int>("SELECT attempts FROM ops.jobs WHERE kind = 'email.send' AND dedupe_key = $1", "email.send:" + id));
    }

    [Fact]
    public async Task TemplateWithAToken_NeverLeavesTheOutbox()
    {
        var host = await StartAsync("mail-token");

        var id = await AddAsync(host, new OutboxEmail("login_link", _member, null, null, new JsonObject { ["link"] = "https://x/#t=secret" }));

        await WaitAsync(async () => await AdminScalarAsync<string>("SELECT error FROM ops.outbox WHERE id = $1", id) is not null);
        Assert.Equal("email.token_template_in_outbox", await AdminScalarAsync<string>("SELECT error FROM ops.outbox WHERE id = $1", id));
        Assert.Null(await AdminScalarAsync<DateTime?>("SELECT sent_at FROM ops.outbox WHERE id = $1", id));
        await WaitAsync(async () => await AdminScalarAsync<string>("SELECT state FROM ops.jobs WHERE dedupe_key = $1", "email.send:" + id) == "failed");
        Assert.DoesNotContain(_transport.Sent, m => m.To.StartsWith("peter." + _member.ToString("N"), StringComparison.Ordinal));
        Assert.True(_logs.Contains("email.refused"));
    }

    private JsonObject RunParams() => new() { ["run_id"] = _run.ToString("D"), ["shop_id"] = Guid.Empty.ToString("D"), ["event"] = "run.partial" };

    private async Task<IHost> StartAsync(string id)
    {
        var host = WorkerTestHost.Build(_logs, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
            ["Worker:Id"] = id,
            ["Worker:Slots:Io"] = "4",
            ["Worker:MinPollMilliseconds"] = "100",
            ["Worker:MaxPollSeconds"] = "0.5",
            ["Jobs:Retry:BaseSeconds"] = "0.5",
            ["Jobs:Retry:MaxSeconds"] = "1",
        }, configure: s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IEmailTransport>(_transport));
            s.Replace(ServiceDescriptor.Singleton<IRefCatalog>(sp => new EnabledLocalesCatalog(ActivatorUtilities.CreateInstance<RefCatalog>(sp))));
        });
        await host.StartAsync(Ct);
        _hosts.Add(host);
        return host;
    }

    private async Task<long> AddAsync(IHost host, OutboxEmail email)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Worker"));
        await connection.OpenAsync(Ct);
        await using var transaction = await TenantSql.BeginAsync(connection, _tenant, null, Ct);
        var id = await OutboxEmails.AddAsync(transaction, host.Services.GetRequiredService<IJobQueue>(), _tenant, email, Ct);
        await transaction.CommitAsync(Ct);
        return id;
    }

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold within 30 s.");
            await Task.Delay(100, Ct);
        }
    }

    private static async Task AdminAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<object?[]>> AdminRowsAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row.Select(v => v is DBNull ? null : v).ToArray());
        }

        return rows;
    }

    private static async Task<T?> AdminScalarAsync<T>(string sql, params object[] parameters)
    {
        var rows = await AdminRowsAsync(sql, parameters);
        return rows.Count == 0 || rows[0][0] is null ? default : (T)rows[0][0]!;
    }

    /// <summary>The real catalog with <c>sk</c> and <c>cs</c> enabled (the shared test database keeps them off as seeded).</summary>
    private sealed class EnabledLocalesCatalog(IRefCatalog inner) : IRefCatalog
    {
        public async Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken ct = default) =>
            (await inner.GetLocalesAsync(ct)).Select(l => l.Code is "sk" or "cs" ? l with { Enabled = true } : l).ToList();

        public Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken ct = default) => inner.GetMarketsAsync(ct);
    }

    /// <summary>Captures e-mails; fails the first <see cref="FailuresLeft"/> sends.</summary>
    private sealed class CapturingTransport : IEmailTransport
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();

        public int FailuresLeft;

        public IReadOnlyList<EmailMessage> Sent => [.. _sent];

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            if (Interlocked.Decrement(ref FailuresLeft) >= 0)
            {
                throw new EmailSendException("email.smtp_unavailable");
            }

            _sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}
