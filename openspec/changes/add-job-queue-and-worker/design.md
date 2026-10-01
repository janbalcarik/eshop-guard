# Design: Fronta úloh a worker

## Technical Approach

Cesty jsou relativní ke kořeni repozitáře. Tabulky `ops.*` a jejich sloupce jsou ze změny 3; časy se v SQL fronty berou z `clock_timestamp()`, ne z `now()` (ta vrací začátek transakce a v delší transakci by lease prodloužila málo).

### Rozhraní (`src/EshopGuard.Jobs/`)

```csharp
public enum JobResourceClass { Fetch, Cpu, Jev, Llm, Io, System }        // ze změny 3, text 'fetch' … 'system'
public enum JobPriority { P0 = 0, P1 = 1, P2 = 2, P3 = 3, P4 = 4 }

public sealed record JobRequest(
    string Kind, JobResourceClass ResourceClass, JobPriority Priority, JsonDocument Payload,
    Guid? TenantId = null, Guid? ShopId = null, Guid? RunId = null,
    string? DedupeKey = null, string? ConcurrencyKey = null,
    int? MaxAttempts = null, DateTimeOffset? NotBefore = null);

public sealed record EnqueueResult(long JobId, bool Created, JobState State); // Created = false → vrácena existující úloha s dedupe_key

public sealed record ClaimedJob(long Id, string Kind, JobResourceClass ResourceClass, JobPriority Priority,
    Guid? TenantId, Guid? ShopId, Guid? RunId, JsonDocument Payload, int Attempt, int MaxAttempts,
    string? ConcurrencyKey, string LeaseOwner);

public interface IJobQueue
{
    Task<EnqueueResult> EnqueueAsync(JobRequest request, DbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimedJob>> ClaimAsync(JobResourceClass resourceClass, int max, string workerId, CancellationToken ct = default);
    Task<HeartbeatResult> HeartbeatAsync(ClaimedJob job, CancellationToken ct = default);           // LeaseHeld, CancelRequested
    Task CompleteAsync(ClaimedJob job, Func<JobTransaction, Task>? writeResults, CancellationToken ct = default); // LeaseLostException
    Task<JobState> FailAsync(ClaimedJob job, JobError error, bool permanent, CancellationToken ct = default);   // queued (opakování) nebo failed
    Task DeferAsync(ClaimedJob job, TimeSpan delay, string reasonCode, CancellationToken ct = default);        // bez započtení pokusu
    Task<bool> CancelAsync(long jobId, CancellationToken ct = default);                                       // jen queued
    Task<int> CancelRunJobsAsync(Guid runId, DbTransaction transaction, CancellationToken ct = default);
    Task<int> RequeueExpiredLeasesAsync(int max, CancellationToken ct = default);
    Task PauseResourceClassAsync(JobResourceClass resourceClass, string reasonCode, CancellationToken ct = default);
    Task ResumeResourceClassAsync(JobResourceClass resourceClass, CancellationToken ct = default);
    Task<IReadOnlySet<JobResourceClass>> GetPausedClassesAsync(CancellationToken ct = default);
    Task<int> DeleteFinishedAsync(TimeSpan succeededOlderThan, TimeSpan failedOlderThan, int batchSize, CancellationToken ct = default);
}

public interface IWorkerStore
{
    Task RegisterAsync(WorkerRegistration worker, CancellationToken ct = default);           // ops.workers
    Task HeartbeatAsync(string workerId, bool draining, CancellationToken ct = default);
    Task UnregisterAsync(string workerId, CancellationToken ct = default);
    Task<int> DeleteStaleWorkersAsync(TimeSpan olderThan, CancellationToken ct = default);
    Task<DomainLease?> TryAcquireDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default);
    Task<bool> RenewDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default);
    Task ReleaseDomainAsync(string domain, long jobId, DomainPolitenessState state, CancellationToken ct = default);
    Task<RateLimitReservation> TryReserveAsync(string bucketKey, int tokens, JobPriority priority, CancellationToken ct = default);
    Task ReturnTokensAsync(string bucketKey, int tokens, CancellationToken ct = default);
}
```

- `EnqueueAsync` vyžaduje transakci volajícího (zásada „zakládání v transakci“: nemůže vzniknout běh bez úlohy). Rozšíření `EshopGuardDb.EnqueueJobAsync(request)` vezme `Database.CurrentTransaction`; bez transakce `InvalidOperationException` (`job.enqueue_requires_transaction`).
- `JobTransaction` (předává se do `writeResults`): `EshopGuardDb Db` (se stejnou transakcí a nastaveným tenantem úlohy), `NpgsqlTransaction Transaction`, `EnqueueAsync(JobRequest)` pro pokračování. Pokračování zrušeného běhu se nezaloží (`EnqueueResult.Created = false`, stav `Canceled`).
- `JobError(string Code, string? ExceptionType, string? Detail)`: `last_error` = `"{Code}: {ExceptionType}: {Detail}"` oříznuté na 500 znaků; `Detail` nesmí obsahovat texty stránek ani klíče (obsluhy hází výjimky s kódy).

### SQL fronty

Zařazení (`PgJobQueue.EnqueueAsync`):
```sql
INSERT INTO ops.jobs (tenant_id, shop_id, run_id, kind, resource_class, priority, payload, state,
                      dedupe_key, concurrency_key, attempts, max_attempts, not_before)
VALUES (@tenant, @shop, @run, @kind, @class, @priority, @payload, 'queued',
        @dedupe, @concurrency, 0, @max_attempts, coalesce(@not_before, clock_timestamp()))
ON CONFLICT (dedupe_key) DO NOTHING
RETURNING id;
-- nic nevráceno → SELECT id, state FROM ops.jobs WHERE dedupe_key = @dedupe
SELECT pg_notify('eshopguard_jobs', @class);   -- doručí se až po COMMIT, při ROLLBACK vůbec
```

Převzetí úloh bez klíče souběhu (`PgJobQueue.ClaimAsync`, krok 1):
```sql
WITH c AS (
  SELECT j.id FROM ops.jobs j
  WHERE j.state = 'queued' AND j.resource_class = @class AND j.not_before <= clock_timestamp()
    AND j.concurrency_key IS NULL
    AND (j.tenant_id IS NULL OR @tenant_cap = 0 OR
         (SELECT count(*) FROM ops.jobs r
           WHERE r.state = 'running' AND r.tenant_id = j.tenant_id AND r.resource_class = @class) < @tenant_cap)
  ORDER BY j.priority, j.id
  LIMIT @n
  FOR UPDATE SKIP LOCKED)
UPDATE ops.jobs j
SET state = 'running', attempts = j.attempts + 1, lease_owner = @worker,
    lease_until = clock_timestamp() + @lease, heartbeat_at = clock_timestamp(),
    started_at = coalesce(j.started_at, clock_timestamp()), updated_at = clock_timestamp()
FROM c WHERE j.id = c.id
RETURNING j.*;
```

Krok 2 (úlohy s `concurrency_key`, po jedné, opakovaně dokud jsou volné sloty): stejný dotaz s `j.concurrency_key IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ops.jobs r WHERE r.state = 'running' AND r.concurrency_key = j.concurrency_key)` a `LIMIT 1`. Když dva workery současně převezmou dvě úlohy se stejným klíčem, druhý `UPDATE` narazí na jedinečný index `ux_jobs_concurrency_running` (23505); jeho transakce se vrátí a worker to zkusí v dalším taktu. Proto se úlohy s klíčem neberou po víc v jednom příkazu.

Strop tenanta je **měkký**: dva workery ve stejné chvíli ho můžou překročit o počet právě převzatých úloh. Slouží spravedlnosti, ne bezpečnosti. Pozastavené druhy (`GetPausedClassesAsync`, mezipaměť 5 s) worker vůbec nedotazuje.

Heartbeat (`HeartbeatAsync`, každých `HeartbeatSeconds`):
```sql
UPDATE ops.jobs SET lease_until = clock_timestamp() + @lease, heartbeat_at = clock_timestamp(), updated_at = clock_timestamp()
WHERE id = @id AND lease_owner = @worker AND attempts = @attempt AND state = 'running'
RETURNING run_id, tenant_id;
-- 0 řádků → LeaseHeld = false (obsluze se zruší CancellationToken)
-- run_id není null → v transakci s TenantSql (tenant úlohy): SELECT cancel_requested FROM checks.runs WHERE id = @run
-- (stejným voláním se prodlouží i zámek domény úlohy, pokud ho drží: IWorkerStore.RenewDomainAsync)
```

Dokončení (`CompleteAsync`):
```sql
BEGIN;                                                   -- přes EshopGuardDb.Database.BeginTransactionAsync (nastaví app.tenant_id)
SELECT 1 FROM ops.jobs
WHERE id = @id AND lease_owner = @worker AND attempts = @attempt AND state = 'running'
FOR UPDATE;                                              -- 0 řádků → ROLLBACK + LeaseLostException
-- writeResults(jobTransaction): zápisy obsluhy, pokračování přes EnqueueAsync
UPDATE ops.jobs SET state = 'succeeded', finished_at = clock_timestamp(), lease_owner = NULL, lease_until = NULL,
       last_error = NULL, updated_at = clock_timestamp() WHERE id = @id;
COMMIT;
```
Řádek úlohy je od `SELECT … FOR UPDATE` do `COMMIT` zamčený, takže ho mezitím nemůže vrátit plánovač (ten zamčené řádky přeskakuje) ani převzít jiný worker.

Selhání (`FailAsync`):
```sql
UPDATE ops.jobs SET
  state = CASE WHEN @permanent OR attempts >= max_attempts THEN 'failed' ELSE 'queued' END,
  not_before = clock_timestamp() + make_interval(secs => least(@base * power(2, attempts - 1), @max) * (0.8 + random() * 0.4)),
  finished_at = CASE WHEN @permanent OR attempts >= max_attempts THEN clock_timestamp() END,
  lease_owner = NULL, lease_until = NULL, last_error = @error, updated_at = clock_timestamp()
WHERE id = @id AND lease_owner = @worker AND attempts = @attempt AND state = 'running'
RETURNING state;
```

Odložení (`DeferAsync`, zámek domény obsazený, pozastavení, ukončení workeru):
```sql
UPDATE ops.jobs SET state = 'queued', attempts = attempts - 1, not_before = clock_timestamp() + @delay,
       lease_owner = NULL, lease_until = NULL, last_error = @reason, updated_at = clock_timestamp()
WHERE id = @id AND lease_owner = @worker AND attempts = @attempt AND state = 'running';
```
Snížení `attempts` vrací jen zvýšení z tohoto převzetí, takže fencing zůstává bezpečný: každé další převzetí dá číslo větší než číslo jakéhokoli workeru, který lease ztratil dřív.

Vrácení propadlých leasů (`RequeueExpiredLeasesAsync`, plánovač každých 15 s):
```sql
WITH e AS (SELECT id FROM ops.jobs WHERE state = 'running' AND lease_until < clock_timestamp()
           ORDER BY lease_until LIMIT @max FOR UPDATE SKIP LOCKED)
UPDATE ops.jobs j SET
  state = CASE WHEN j.attempts >= j.max_attempts THEN 'failed' ELSE 'queued' END,
  not_before = clock_timestamp() + <odstup jako u FailAsync>,
  finished_at = CASE WHEN j.attempts >= j.max_attempts THEN clock_timestamp() END,
  lease_owner = NULL, lease_until = NULL, last_error = 'job.lease_expired', updated_at = clock_timestamp()
FROM e WHERE j.id = e.id RETURNING j.id, j.state;
```

Zrušení: `CancelAsync` = `UPDATE ops.jobs SET state = 'canceled', finished_at = clock_timestamp() WHERE id = @id AND state = 'queued'`; `CancelRunJobsAsync` totéž pro `run_id = @run` v transakci volajícího (API ve stejné transakci nastaví `checks.runs.cancel_requested = true`). Běžící úloha zrušení uvidí při heartbeatu nebo přes `JobExecutionContext.IsRunCancellationRequestedAsync()` mezi dílčími dávkami a vrátí `JobResult.Canceled`.

Pozastavení: `ops.system_settings` klíč `jobs.paused_classes`, hodnota `{"jev": {"reason": "jev.credit_exhausted", "at": "…"}}`; `PauseResourceClassAsync` = `UPDATE ops.system_settings SET value = value || jsonb_build_object(@class, …)`; `ResumeResourceClassAsync` = `value - @class`. Řádek založí migrace s hodnotou `{}`.

### Zámky domén a limity (`PgWorkerStore`)

Získání zámku domény:
```sql
INSERT INTO ops.domains (domain) VALUES (@domain) ON CONFLICT (domain) DO NOTHING;
WITH d AS (SELECT domain FROM ops.domains
           WHERE domain = @domain
             AND (lease_until IS NULL OR lease_until < clock_timestamp() OR lease_job_id = @job)
             AND (blocked_until IS NULL OR blocked_until <= clock_timestamp())
           FOR UPDATE SKIP LOCKED)
UPDATE ops.domains x SET lease_job_id = @job, lease_until = clock_timestamp() + @lease, updated_at = clock_timestamp()
FROM d WHERE x.domain = d.domain
RETURNING x.robots_txt, x.robots_fetched_at, x.crawl_delay_ms, x.sitemaps, x.last_request_at, x.rate, x.consecutive_errors, x.blocked_until;
-- nic nevráceno → null (obsluha zavolá DeferAsync s odstupem do lease_until, nejvýš 30 s)
```
Uvolnění: `UPDATE ops.domains SET lease_job_id = NULL, lease_until = NULL, last_request_at = @last, rate = @rate, crawl_delay_ms = @delay, consecutive_errors = @errors, robots_txt = @robots, robots_fetched_at = @robotsAt, sitemaps = @sitemaps, blocked_until = @blocked WHERE domain = @domain AND lease_job_id = @job` (cizí zámek se neuvolní). `ops.domains` obsahuje jen veřejná data cizích webů (podklad 3.8).

Klíč souběhu domény: `JobKeys.Domain(string domain)` = `"domain:" + domain.ToLowerInvariant()` (bez `www.`). Stahovací úlohy (změna 8) ho nastavují vždy.

Rezervace tokenů:
```sql
UPDATE ops.rate_limit_buckets b SET
  tokens = least(b.capacity, b.tokens + extract(epoch FROM clock_timestamp() - b.updated_at) * b.refill_per_sec) - @n,
  updated_at = clock_timestamp()
WHERE b.key = @key
  AND least(b.capacity, b.tokens + extract(epoch FROM clock_timestamp() - b.updated_at) * b.refill_per_sec) - @n
      >= CASE WHEN @priority <= 1 THEN 0 ELSE b.capacity * coalesce((b.reserved ->> 'p0_p1_share')::double precision, 0) END
RETURNING b.tokens;
```
- Nevráceno nic → `SELECT capacity, tokens, refill_per_sec, updated_at, reserved` → bucket neexistuje: `RateLimitBucketMissingException` (`ratelimit.bucket_missing`); jinak `RateLimitReservation.Denied(RetryAfter)` s `RetryAfter = (n + rezerva − dostupné) / refill_per_sec`.
- `n` větší než `capacity × (1 − p0_p1_share)` u P2–P4 (nebo než `capacity` u P0–P1) → `ArgumentOutOfRangeException` (`ratelimit.batch_too_large`), protože by se nikdy nesplnilo; obsluha musí dávku zmenšit.
- `ReturnTokensAsync`: `tokens = least(capacity, tokens + @n)` (nevyužité tokeny po zásahu cache).
- Klíče bucketů nesmí obsahovat osobní údaje ani texty zákazníků (např. limity odkazů pro přihlášení ve změně 9 přes otisk e-mailu).

### Obsluhy úloh a zpracování (`JobProcessingService`)

```csharp
public interface IJobHandler
{
    string Kind { get; }                          // např. "system.ensure_partitions"
    JobResourceClass ResourceClass { get; }
    Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct);
}

public abstract record JobResult
{
    public sealed record Succeeded : JobResult;                                       // runner dokončí, pokud obsluha nezavolala CompleteAsync
    public sealed record Retry(string Code, TimeSpan? After = null) : JobResult;      // dočasná chyba (429, 5xx, timeout)
    public sealed record Fail(string Code) : JobResult;                               // trvalá chyba
    public sealed record Defer(TimeSpan Delay, string Code) : JobResult;              // bez započtení pokusu
    public sealed record PauseClass(string Code) : JobResult;                         // došlý kredit, odmítnutý klíč
    public sealed record Canceled : JobResult;                                        // běh zrušen
}
```

- `JobHandlerRegistry` (z DI `IEnumerable<IJobHandler>`): duplicitní `Kind` = chyba při startu; neznámý `Kind` při zpracování = `FailAsync(permanent: true)` s `job.unknown_kind`.
- Pro každý druh zdroje jedna smyčka `ResourceClassLoop`:
  1. volné sloty = `Slots[class]` − běžící; druh pozastavený → čekat;
  2. `ClaimAsync` (krok 1 a 2) do počtu volných slotů;
  3. každou úlohu spustit v novém DI scope (`ITenantContext.Set(job.TenantId)`, pokud není null) s heartbeatem (`PeriodicTimer`, `HeartbeatSeconds`); ztráta leasu zruší token obsluhy;
  4. čekat na: oznámení `eshopguard_jobs` pro tento druh, uvolněný slot, nebo interval dotazování (0,5 s po úspěšném převzetí, prodlužuje se až na `MaxPollSeconds` při prázdné frontě).
- Výsledek obsluhy → `CompleteAsync` / `FailAsync` / `DeferAsync` / `PauseResourceClassAsync` + `DeferAsync` / stav `canceled`. Neošetřená výjimka → `FailAsync(permanent: false)` s `job.unhandled` a typem výjimky. `LeaseLostException` → jen log `job.lease_lost`, nic dalšího se nezapisuje.
- `JobNotificationListener` (`BackgroundService`): samostatné spojení, `LISTEN eshopguard_jobs`, `NpgsqlConnection.WaitAsync`; při výpadku spojení znovu s odstupem. Správnost na oznámeních nezávisí, jen latence P0.
- Registr: při startu `RegisterAsync` (id `Worker:Id`, verze sestavení, sloty jako `jsonb`), heartbeat workeru každých 30 s, při ukončení `draining = true`, po doběhnutí `UnregisterAsync`.
- Korektní ukončení (`StopAsync`): přestat brát úlohy → `draining` → čekat na běžící úlohy nejvýš `ShutdownSeconds − 5 s` → zbylým zrušit token a vrátit je `DeferAsync(0, "worker.shutdown")` → `UnregisterAsync`. `ShutdownSeconds` (90) < lease (120 s), dávka stahování je do 60 s.

### Plánovač (`SchedulerService`)

- Takt každých `Scheduler:TickSeconds` (15 s); každý takt v transakci s `SELECT pg_try_advisory_xact_lock(<konstanta 64 bit z "eshopguard.scheduler">)`; bez zámku takt nic nedělá. Zámek transakce funguje i za PgBouncerem v transakčním režimu.
- Úkoly `IScheduledTask`:
  - `LeaseReaperTask`: každý takt `RequeueExpiredLeasesAsync(500)`;
  - `PartitionMaintenanceTask`: každý takt zkusí zařadit `system.ensure_partitions` s `dedupe_key = system.ensure_partitions:{yyyy-MM-dd}` (UTC), P4, `system`; obsluha `EnsurePartitionsHandler` volá `PartitionMaintainer.EnsureMonthlyPartitionsAsync(3)`;
  - `JobCleanupTask`: `system.cleanup_jobs:{yyyy-MM-dd}`; obsluha `CleanupJobsHandler` volá `DeleteFinishedAsync(7 dní, 30 dní podle K rozhodnutí 3, dávka 10 000)` opakovaně, dokud něco maže;
  - `WorkerRegistryCleanupTask`: `DeleteStaleWorkersAsync(10 min)`.
- Plánovač nic nepočítá, jen zakládá úlohy (architektura část 2). Zpracování `ops.schedules` přidá změna 16 jako další `IScheduledTask`.

### Konfigurace workeru (`src/EshopGuard.Worker/appsettings.json`)

```json
{
  "Worker": {
    "Slots": { "fetch": 100, "cpu": 0, "jev": 8, "llm": 4, "io": 4, "system": 2 },
    "TenantCaps": { "fetch": 20, "cpu": 4, "jev": 4, "llm": 2, "io": 4, "system": 0 },
    "LeaseSeconds": 120, "HeartbeatSeconds": 30, "MinPollMilliseconds": 500, "MaxPollSeconds": 5,
    "ShutdownSeconds": 90
  },
  "Jobs": { "DefaultMaxAttempts": 5, "Retry": { "BaseSeconds": 10, "MaxSeconds": 900 } },
  "Scheduler": { "Enabled": true, "TickSeconds": 15 }
}
```
`cpu: 0` = `Environment.ProcessorCount` (při implementaci změněno: chybějící `cpu` = počet procesorů, 0 = druh vypnutý, viz Odchylky 5); `TenantCaps` 0 = bez stropu. Hodnoty jsou návrh (proposal, K rozhodnutí 1). Kontrola při startu: `HeartbeatSeconds` < `LeaseSeconds / 3`, `ShutdownSeconds` < `LeaseSeconds`, sloty ≥ 0; jinak `config.worker_invalid`.

### Migrace `F2JobQueue` (`src/EshopGuard.Data/Migrations/`)

- `CREATE UNIQUE INDEX ux_jobs_concurrency_running ON ops.jobs (concurrency_key) WHERE state = 'running' AND concurrency_key IS NOT NULL;`
- `CREATE INDEX ix_jobs_queued_keyed ON ops.jobs (resource_class, priority, id) WHERE state = 'queued' AND concurrency_key IS NOT NULL;`
- `CREATE INDEX ix_jobs_running_tenant ON ops.jobs (tenant_id, resource_class) WHERE state = 'running';`
- `CREATE INDEX ix_jobs_finished ON ops.jobs (finished_at) WHERE state IN ('succeeded', 'canceled', 'failed');`
- `ALTER TABLE ops.jobs ADD CONSTRAINT ck_jobs_attempts CHECK (attempts >= 0 AND max_attempts >= 1)`, `ck_jobs_lease CHECK ((state = 'running') = (lease_owner IS NOT NULL AND lease_until IS NOT NULL))`.
- Výchozí řádky: `ops.system_settings ('jobs.paused_classes', '{}')`; `ops.rate_limit_buckets ('jev', capacity 1200, tokens 1200, refill_per_sec 20, reserved '{"p0_p1_share": 0.2}')`.
- Práva: beze změny proti matici ze změny 3 (`eshopguard_worker` S I U D na `ops.jobs`, S I U D na `ops.workers`, `ops.domains`, S I U na `ops.rate_limit_buckets`, S U na `ops.system_settings`; `eshopguard_app` S I U na `ops.jobs`).

### PgBouncer (poznámka, nasazení až od desítek workerů)

V transakčním režimu PgBouncer funguje: `set_config(…, true)` (= `SET LOCAL`), `pg_try_advisory_xact_lock`, `SELECT … FOR UPDATE SKIP LOCKED`, `pg_notify`. Nefunguje: `LISTEN` (`JobNotificationListener` proto potřebuje přímé spojení, volitelný klíč `ConnectionStrings:WorkerListen`, výchozí `ConnectionStrings:Worker`), zámky na úrovni relace (nepoužíváme), nastavení relace `SET` (nepoužíváme). Připravené příkazy Npgsql (automatická příprava je ve výchozím stavu vypnutá) vyžadují PgBouncer ≥ 1.21 s `max_prepared_statements`. Ověří změna 17.

## Architecture Decisions

1. **Fronta v PostgreSQL** (podklad, rozhodnuto 1. 10. 2026): žádný další systém, zařazení v transakci s během, stejný kód pro 1 i 10 workerů. Rozhraní `IJobQueue` dovolí později jinou implementaci.
2. **Fencing přes `attempts` místo zvláštního tokenu:** sloupec z podkladu stačí, protože se zvyšuje při každém převzetí a odložení vrací jen vlastní zvýšení.
3. **Dokončení zamyká řádek úlohy před zápisem výsledků:** jen tak „zaseknutý worker nezapíše“ platí i pro zápisy obsluhy, nejen pro stav úlohy.
4. **Úlohy s `concurrency_key` po jedné + jedinečný částečný index:** `DISTINCT ON` ani okenní funkce s `FOR UPDATE` nejdou; index je poslední pojistka při souběhu dvou workerů.
5. **Doména jako `concurrency_key`** (proposal, K rozhodnutí 6): střídání běhů plyne z pořadí `id`, zámek `ops.domains` zůstává pojistkou a nositelem stavu zdvořilosti.
6. **Měkký strop tenanta v dotazu převzetí:** jednoduchý, bez dalších tabulek; přesnost stačí pro spravedlnost.
7. **Plánovač s transakčním zámkem a `dedupe_key` s datem:** bez stavu v paměti i bez zvláštní tabulky; víc instancí nic nezdvojí, výpadek jedné převezme jiná v příštím taktu.
8. **Probouzení přes `LISTEN/NOTIFY` jen jako zrychlení:** P0 do vteřin bez agresivního dotazování všech workerů; při výpadku spojení platí dotazování.
9. **Samostatná testovací databáze** `eshopguard_test_jobs` (proposal, K rozhodnutí 7) a testy fronty v jedné kolekci bez paralelizace.
10. **Pád workeru se v testech simuluje v procesu:** `JobProcessingService` dostane testovací háček, který zastaví smyčky a heartbeaty bez uvolnění leasů. Z pohledu databáze je to stejné jako zabitý proces (leasy vyprší, nikdo je neuvolní). Skutečné zabití procesu ověří ruční zkouška v úkolu 10.5.

## Odchylky při implementaci (1. 10. 2026)

Měřeno v cloudovém prostředí (PostgreSQL 18, databáze `eshopguard_test_jobs`).

1. **Úloha s klíčem souběhu se bere, jen když je pro svůj klíč první čekající** (`KeyHead` v `JobQueueSql`, index `ix_jobs_queued_key_head (concurrency_key, priority, id) WHERE state = 'queued'`).
   - Proč: dva workery mohly současně převzít dvě úlohy stejného klíče. Vyhrála ta, která dřív zapsala do `ux_jobs_concurrency_running`, i když byla novější, takže pořadí `priority, id` neplatilo.
   - Měření: testy střídání domén a deseti úloh jednoho klíče selhaly 3× z 8 běhů, po úpravě 10× z 10.
2. **Pořadí platí i mezi úlohami bez klíče a s klíčem.** Převzetí má tři kroky:
   - úlohy s klíčem, před kterými ve frontě není žádná úloha bez klíče;
   - úlohy bez klíče jedním příkazem;
   - zbylé úlohy s klíčem.

   V designu šly úlohy s klíčem až po úlohách bez klíče, takže při stálé zásobě úloh bez klíče by na řadu nepřišly. První čekající úloha bez klíče se počítá jednou v CTE `unkeyed_head`; kontrola po řádcích vedla na sekvenční průchod tabulky (16 ms → 1 ms).
3. **Strop tenanta platí i uvnitř jednoho příkazu převzetí.** Kontrola po řádcích vidí jen úlohy, které běžely před příkazem, takže jeden příkaz vzal 8 úloh tenanta při stropu 2. Zamčení kandidáti se proto seřadí po tenantech (`row_number()`) a vezme se jen tolik, kolik strop dovolí; ostatní zůstanou ve frontě.
4. **Index fronty:** index `ix_jobs_resource_class_priority_not_before_id` ze změny 3 nahradil `ix_jobs_queued_unkeyed (resource_class, priority, id) WHERE state = 'queued' AND concurrency_key IS NULL` a `not_before` se kontroluje filtrem. Starý index řadí podle `not_before` před `id`, takže převzetí četlo a třídilo celou skupinu priority.
   - Nad 100 000 čekajícími úlohami trval příkaz převzetí 10 úloh 75 ms, nově 0,75 ms; celé `ClaimAsync` asi 4 ms.
   - Úloha s klíčem z 20 000 se převezme asi za 2 ms.
   - Kontroluje `ClaimPlanTests`: plán používá indexy fronty, žádný sekvenční průchod.
5. **Sloty:** když `cpu` chybí, worker má tolik slotů jako procesorů; hodnota 0 druh vypíná (v designu znamenala `cpu: 0` počet procesorů). Worker tak může druh nebrat, což potřebují vyhrazené workery i testy na sdílené databázi.
6. **Mezipaměť pozastavených druhů je 2 s** (v designu 5 s) a čekání v pozastaveném druhu nejvýš 1 s. Obnovení tak platí do 3 s, specifikace žádá do 5 s.
7. **Heartbeat úlohy prodlužuje zámek domény** stejným příkazem (`ops.domains.lease_job_id = id úlohy`, nový index `ix_domains_lease_job_id`) a zámek nikdy nezkracuje.
8. **Oznámení** se posílá i po dokončení, selhání a zrušení úlohy s klíčem (klíč se uvolní) a po odložení, ne jen při zařazení. Další úloha domény tak nečeká na interval dotazování.
9. **Rozhraní:**
   - nové metody `IJobQueue.MarkCanceledAsync`, `IsRunCancelRequestedAsync` a `CompleteAsync(job, db, …)`, kde `db` je kontext rozsahu obsluhy;
   - `FailAsync(…, retryAfter)` kvůli `JobResult.Retry(After)`;
   - `IWorkerStore.HeartbeatAsync` vrací `bool`, protože worker se smazaným řádkem se znovu zaregistruje;
   - rozšíření EF `db.EnqueueJobAsync(queue, request)` a `db.CancelRunJobsAsync(queue, runId)` dostávají frontu parametrem, protože kontext EF nezná služby aplikace;
   - nastavení `Worker:RegistryHeartbeatSeconds` (30 s) a `Jobs:PausedClassesCacheSeconds`; lease, heartbeat a interval dotazování jsou desetinná čísla.
10. **Pokračování zrušeného běhu:** `JobTransaction.EnqueueAsync` čte `cancel_requested` s `FOR SHARE`, takže souběžné zrušení počká na COMMIT a pak zruší i to, co vzniklo. Běh, který není vidět, se bere jako zrušený (fail-closed). Proto API musí nastavit `cancel_requested` před voláním `CancelRunJobsAsync`.
11. **Buckety OpenAI v migraci F2** podle limitů gpt-6.1-sol, Tier 4 (ověřeno na stránce modelu 1. 10. 2026):
    - `openai`: 10 000 požadavků za minutu (166,67/s);
    - `openai:tokens`: 4 000 000 tokenů za minutu (66 666,67/s);
    - u obou zásoba na 10 s, protože OpenAI vynucuje limity i v kratších oknech, a podíl P0–P1 20 %.

    Chybějící bucket OpenAI proto ve změně 5 nenastane.
12. **Testovací háčky:** `SimulateCrashAsync` (pád) a `SuspendJobHeartbeats` (zaseknutý worker), obojí `internal`.

**Zjištění pro změnu 17:** když jsou na začátku fronty úlohy tenantů, kteří už dosáhli stropu, převzetí je musí přeskočit. 20 000 přeskočených úloh stojí asi 30 ms na převzetí. Zátěžový test rozhodne, zda je potřeba spravedlivější výběr po tenantech.

## Data Flow

```
API / obsluha (změna 8)                    PostgreSQL ops.jobs                     Worker
  BEGIN (tenant)
  INSERT checks.runs …
  IJobQueue.EnqueueAsync(run.discover) ──→ INSERT … ON CONFLICT (dedupe_key)
  COMMIT ───────────────────────────────→ pg_notify('eshopguard_jobs','fetch') ──→ JobNotificationListener → ResourceClassLoop(fetch)
                                                                                   ClaimAsync: UPDATE … FOR UPDATE SKIP LOCKED
                                           state=running, attempts=1, lease 120 s ←┘
                                                                                   handler.ExecuteAsync (DI scope, ITenantContext = job.tenant)
                                           heartbeat každých 30 s ←──────────────── HeartbeatAsync (+ cancel_requested běhu, + zámek domény)
                                                                                   context.CompleteAsync(tx => zápisy + EnqueueAsync(pokračování))
                                           SELECT … FOR UPDATE (fencing)
                                           zápisy obsluhy, INSERT pokračování
                                           state=succeeded, COMMIT ←──────────────┘

Pád workeru:      lease_until vyprší → SchedulerService (zámek taktu) → RequeueExpiredLeasesAsync → state=queued, not_before=+odstup
Zaseknutý worker: jiný worker převezme (attempts=2) → původní CompleteAsync: SELECT … FOR UPDATE nic nenajde → ROLLBACK, LeaseLostException
Došlý kredit:     obsluha vrátí PauseClass("jev.credit_exhausted") → system_settings.jobs.paused_classes += jev → úloha zpět (Defer) → nikdo nebere jev
```

## File Changes

`src/EshopGuard.Jobs/`:
- `EshopGuard.Jobs.csproj` (odkazy `Core`, `Data`, `Storage`; balíčky `Npgsql`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Options`).
- `Queue/IJobQueue.cs`, `Queue/JobRequest.cs`, `Queue/EnqueueResult.cs`, `Queue/ClaimedJob.cs`, `Queue/HeartbeatResult.cs`, `Queue/JobError.cs`, `Queue/JobPriority.cs`, `Queue/LeaseLostException.cs`, `Queue/JobTransaction.cs`, `Queue/PgJobQueue.cs`, `Queue/JobQueueSql.cs` (texty SQL jako konstanty), `Queue/EshopGuardDbJobExtensions.cs` (`EnqueueJobAsync`, `CancelRunJobsAsync` nad aktuální transakcí EF).
- `Workers/IWorkerStore.cs`, `Workers/PgWorkerStore.cs`, `Workers/WorkerRegistration.cs`, `Workers/DomainLease.cs`, `Workers/DomainPolitenessState.cs`, `Workers/RateLimitReservation.cs`, `Workers/RateLimitBucketMissingException.cs`, `Workers/JobKeys.cs` (`Domain`, `Run`).
- `Processing/IJobHandler.cs`, `Processing/JobResult.cs`, `Processing/JobExecutionContext.cs`, `Processing/JobHandlerRegistry.cs`, `Processing/ResourceClassLoop.cs`, `Processing/JobProcessingService.cs` (+ testovací háček `SimulateCrashAsync` za `internal` a `InternalsVisibleTo EshopGuard.Jobs.Tests`), `Processing/JobNotificationListener.cs`, `Processing/WorkerOptions.cs` (`Slots`, `TenantCaps`, `LeaseSeconds`, …, validace), `Processing/JobsOptions.cs`.
- `Scheduling/IScheduledTask.cs`, `Scheduling/SchedulerService.cs`, `Scheduling/SchedulerOptions.cs`, `Scheduling/LeaseReaperTask.cs`, `Scheduling/PartitionMaintenanceTask.cs`, `Scheduling/JobCleanupTask.cs`, `Scheduling/WorkerRegistryCleanupTask.cs`.
- `Handlers/EnsurePartitionsHandler.cs` (`system.ensure_partitions`), `Handlers/CleanupJobsHandler.cs` (`system.cleanup_jobs`).
- `JobsServiceCollectionExtensions.cs`: `AddEshopGuardJobQueue(IConfiguration)` (jen `IJobQueue` pro API) a `AddEshopGuardJobProcessing(IConfiguration)` (zpracování, plánovač, obsluhy údržby pro worker).

`src/EshopGuard.Worker/`:
- `Program.cs`: místo `WorkerSkeletonService` `AddEshopGuardJobProcessing(builder.Configuration)`; `HostOptions.ShutdownTimeout = ShutdownSeconds`.
- `WorkerSkeletonService.cs` smazat (nahrazuje `JobProcessingService`, který přebírá logy `worker.started` a `worker.stopped`); `WorkerOptions.cs` ze změny 2 (`Id`, `ShutdownSeconds`) přesunout do `EshopGuard.Jobs/Processing/WorkerOptions.cs` a rozšířit; `appsettings.json` podle designu.
- `tests/EshopGuard.Worker.Tests/WorkerShutdownTests.cs` (ze změny 2) upravit na `JobProcessingService` (stejná očekávání: `worker.started`, `worker.stopped`, konec do `ShutdownSeconds`).

`src/EshopGuard.Api/Program.cs`: `AddEshopGuardJobQueue` (API jen zařazuje a ruší).

`src/EshopGuard.Data/Migrations/<ts>_F2JobQueue.cs` (+ `.Designer.cs`, snapshot) a `Migrations/Sql/F2/01_job_queue.sql`.

`deploy/dev/setup-local.ps1`: spustit `00_roles.sql` i pro `eshopguard_test_jobs` (K rozhodnutí 7).

`tests/EshopGuard.Jobs.Tests/`:
- `EshopGuard.Jobs.Tests.csproj`; `JobsTestDatabase.cs` (fixture: připojení z `TestConfiguration` s `Database = eshopguard_test_jobs`, migrace jako `Owner`, `TRUNCATE ops.jobs, ops.workers, ops.domains` a obnovení řádků limitů před každým testem, tabulka `jobs_test.effects(job_id bigint, attempt int, worker_id text, started_at timestamptz, finished_at timestamptz, tag text)` s právy pro `eshopguard_worker`); `JobsCollection.cs` (`DisableParallelization = true`).
- `TestHandlers.cs` (`test.record`, `test.block`, `test.fail`, `test.fetch_domain`, `test.pause`, `test.cancel_aware`), `WorkerHarness.cs` (spustí N instancí `JobProcessingService` v procesu s vlastním `Worker:Id`, krátkým leasem a heartbeatem).
- `NoDoubleProcessingTests.cs`, `KilledWorkerTests.cs`, `StuckWorkerTests.cs`, `DomainAlternationTests.cs`, `EnqueueTransactionTests.cs`, `DedupeAndConcurrencyKeyTests.cs`, `PriorityTests.cs`, `RetryAndFailureTests.cs`, `CancelTests.cs`, `TenantCapTests.cs`, `PauseTests.cs`, `RateLimitTests.cs`, `DomainLeaseTests.cs`, `SchedulerTests.cs`, `GracefulShutdownTests.cs`, `UnknownKindTests.cs`, `WorkerRegistryTests.cs`, `MaintenanceHandlersTests.cs`.
