# Tasks

Cesty jsou relativní ke kořeni `D:\_github\Overko\eshop-guard`. Žádný úkol nevolá Jev ani OpenAI; testovací obsluhy jsou bez externích služeb.

## 1. Migrace a testovací databáze

- [ ] 1.1 `dotnet ef migrations add F2JobQueue --project src/EshopGuard.Data` s `Migrations/Sql/F2/01_job_queue.sql`: indexy `ux_jobs_concurrency_running`, `ix_jobs_queued_keyed`, `ix_jobs_running_tenant`, `ix_jobs_finished`, omezení `ck_jobs_attempts` a `ck_jobs_lease`, řádky `ops.system_settings ('jobs.paused_classes', '{}')` a `ops.rate_limit_buckets ('jev', 1200, 1200, 20, '{"p0_p1_share": 0.2}')`; `Down` vše vrátí.
- [ ] 1.2 `deploy/dev/setup-local.ps1`: spustit `00_roles.sql` i pro `eshopguard_test_jobs` (podle K rozhodnutí 7); ověřit `\l eshopguard_test_jobs` s vlastníkem `eshopguard_owner`.
- [ ] 1.3 `dotnet ef database update` na `eshopguard`, `eshopguard_test` a `eshopguard_test_jobs`; `has-pending-model-changes` nic nehlásí.

## 2. Fronta (`EshopGuard.Jobs/Queue`)

- [ ] 2.1 Typy `JobRequest`, `EnqueueResult`, `ClaimedJob`, `HeartbeatResult`, `JobError`, `JobPriority`, `LeaseLostException`, `JobTransaction`; rozhraní `IJobQueue` podle designu.
- [ ] 2.2 `Queue/JobQueueSql.cs` se všemi příkazy z designu (zařazení, převzetí bez klíče, převzetí s klíčem, heartbeat, zámek pro dokončení, dokončení, selhání, odložení, vrácení propadlých, zrušení, pozastavení, úklid); všude `clock_timestamp()`.
- [ ] 2.3 `PgJobQueue.EnqueueAsync`: `INSERT … ON CONFLICT (dedupe_key) DO NOTHING RETURNING id`, při konfliktu `SELECT id, state`, `pg_notify` ve stejné transakci; validace (`Kind` neprázdný, priorita 0–4, `MaxAttempts` ≥ 1, `Payload` do 64 kB, aby se do fronty nedávaly texty stránek).
- [ ] 2.4 `Queue/EshopGuardDbJobExtensions.cs`: `EnqueueJobAsync` a `CancelRunJobsAsync` nad `Database.CurrentTransaction`; bez transakce `job.enqueue_requires_transaction`.
- [ ] 2.5 `PgJobQueue.ClaimAsync`: krok 1 (bez klíče, `LIMIT max`), krok 2 (s klíčem, `LIMIT 1` opakovaně do zaplnění); zachycení 23505 na `ux_jobs_concurrency_running` → vrácení transakce kroku 2 a pokračování bez chyby; strop tenanta z `WorkerOptions.TenantCaps`.
- [ ] 2.6 `PgJobQueue.HeartbeatAsync`: prodloužení s fencingem; pro `run_id` čtení `checks.runs.cancel_requested` přes `TenantSql.BeginAsync` s tenantem úlohy.
- [ ] 2.7 `PgJobQueue.CompleteAsync`: transakce `EshopGuardDb` (tenant úlohy), `SELECT … FOR UPDATE` s fencingem, `writeResults(JobTransaction)`, `UPDATE state = 'succeeded'`; při 0 řádcích `ROLLBACK` a `LeaseLostException`. `JobTransaction.EnqueueAsync` u běhu s `cancel_requested` nic nezaloží.
- [ ] 2.8 `FailAsync` (odstup z `JobsOptions.Retry`, `last_error` = kód + typ + detail ořezaný na 500 znaků), `DeferAsync` (`attempts − 1`), `CancelAsync`, `RequeueExpiredLeasesAsync`, `PauseResourceClassAsync`, `ResumeResourceClassAsync`, `GetPausedClassesAsync` (mezipaměť 5 s), `DeleteFinishedAsync` (po dávkách).

## 3. Workery, domény a limity (`EshopGuard.Jobs/Workers`)

- [ ] 3.1 `IWorkerStore`, `WorkerRegistration`, `DomainLease`, `DomainPolitenessState`, `RateLimitReservation` (`Granted(remaining)` / `Denied(retryAfter)`), `RateLimitBucketMissingException`, `JobKeys` (`Domain` bez `www.` a malými písmeny, `Run(runId, step)`).
- [ ] 3.2 `PgWorkerStore.RegisterAsync` (`INSERT … ON CONFLICT (id) DO UPDATE`), `HeartbeatAsync`, `UnregisterAsync`, `DeleteStaleWorkersAsync`.
- [ ] 3.3 `PgWorkerStore.TryAcquireDomainAsync`, `RenewDomainAsync`, `ReleaseDomainAsync` podle designu (`FOR UPDATE SKIP LOCKED`, uvolní jen držitel, respektuje `blocked_until`).
- [ ] 3.4 `PgWorkerStore.TryReserveAsync` (jeden `UPDATE … RETURNING`, vyhrazený podíl P0–P1, `Denied` s `RetryAfter`, chybějící bucket → výjimka, příliš velká dávka → `ratelimit.batch_too_large`) a `ReturnTokensAsync`.

## 4. Zpracování úloh (`EshopGuard.Jobs/Processing`)

- [ ] 4.1 `IJobHandler`, `JobResult` (`Succeeded`, `Retry`, `Fail`, `Defer`, `PauseClass`, `Canceled`), `JobExecutionContext` (`Job`, `Services`, `CompleteAsync`, `IsRunCancellationRequestedAsync`, `DeferAsync`).
- [ ] 4.2 `JobHandlerRegistry`: z DI `IEnumerable<IJobHandler>`; duplicitní `Kind` → výjimka při startu; neznámý `Kind` → `FailAsync(permanent: true)` s `job.unknown_kind`.
- [ ] 4.3 `WorkerOptions` (přesun ze `src/EshopGuard.Worker/WorkerOptions.cs` a rozšíření o `Slots`, `TenantCaps`, `LeaseSeconds`, `HeartbeatSeconds`, `MinPollMilliseconds`, `MaxPollSeconds`) s validací (`config.worker_invalid`) a `JobsOptions` (`DefaultMaxAttempts`, `Retry.BaseSeconds`, `Retry.MaxSeconds`).
- [ ] 4.4 `ResourceClassLoop`: volné sloty, pozastavení, `ClaimAsync`, spuštění úlohy v novém DI scope s `ITenantContext.Set(job.TenantId)`, heartbeat (`PeriodicTimer`), mapování `JobResult` na volání fronty, neošetřená výjimka → `job.unhandled`, `LeaseLostException` → log `job.lease_lost`; čekání na oznámení / uvolnění slotu / interval.
- [ ] 4.5 `JobNotificationListener`: `LISTEN eshopguard_jobs` na samostatném spojení (`ConnectionStrings:WorkerListen`, jinak `ConnectionStrings:Worker`), rozeslání do smyček podle druhu, znovupřipojení s odstupem.
- [ ] 4.6 `JobProcessingService`: registrace workeru, smyčky pro šest druhů, heartbeat workeru 30 s, logy `worker.started` / `worker.stopped`, korektní ukončení podle designu (`draining`, čekání do `ShutdownSeconds − 5 s`, `DeferAsync(0, "worker.shutdown")`, `UnregisterAsync`); `internal` háček `SimulateCrashAsync` (zastaví smyčky a heartbeaty bez uvolnění leasů) s `InternalsVisibleTo EshopGuard.Jobs.Tests`.

## 5. Plánovač a údržba

- [ ] 5.1 `Scheduling/IScheduledTask`, `SchedulerOptions` (`Enabled`, `TickSeconds`), `SchedulerService` (takt v transakci se `pg_try_advisory_xact_lock(<konstanta>)`; bez zámku nic; chyba úkolu = log s kódem, takt pokračuje dalším úkolem).
- [ ] 5.2 `LeaseReaperTask` (`RequeueExpiredLeasesAsync(500)` každý takt), `PartitionMaintenanceTask` (`system.ensure_partitions:{yyyy-MM-dd}`, P4, `system`), `JobCleanupTask` (`system.cleanup_jobs:{yyyy-MM-dd}`), `WorkerRegistryCleanupTask` (10 min).
- [ ] 5.3 `Handlers/EnsurePartitionsHandler` (`PartitionMaintainer.EnsureMonthlyPartitionsAsync(3)`, výsledek do logu) a `Handlers/CleanupJobsHandler` (`DeleteFinishedAsync(7 dní, 30 dní, 10 000)` opakovaně).
- [ ] 5.4 `JobsServiceCollectionExtensions.AddEshopGuardJobQueue` (API) a `AddEshopGuardJobProcessing` (worker: `IJobQueue`, `IWorkerStore`, `JobHandlerRegistry`, obsluhy údržby, `JobProcessingService`, `JobNotificationListener`, `SchedulerService`).

## 6. Worker a API

- [ ] 6.1 `src/EshopGuard.Worker/Program.cs`: `AddEshopGuardJobProcessing(builder.Configuration)`, `HostOptions.ShutdownTimeout` z `Worker:ShutdownSeconds`; smazat `WorkerSkeletonService.cs`; `appsettings.json` podle designu.
- [ ] 6.2 `src/EshopGuard.Api/Program.cs`: `AddEshopGuardJobQueue` (jen `IJobQueue` pro zařazení a zrušení).
- [ ] 6.3 `tests/EshopGuard.Worker.Tests/WorkerShutdownTests.cs` upravit na `JobProcessingService` (stejná očekávání).

## 7. Testovací infrastruktura

- [ ] 7.1 `tests/EshopGuard.Jobs.Tests/EshopGuard.Jobs.Tests.csproj` (odkazy `Jobs`, `Data`; `Compile Include` sdíleného `TestConfiguration.cs`) a přidání do `EshopGuard.sln`.
- [ ] 7.2 `JobsTestDatabase.cs`: připojení z `TestConfiguration` s `Database = eshopguard_test_jobs`; migrace jako `Owner`; před každým testem jako `Owner` `TRUNCATE ops.jobs, ops.workers, ops.domains, jobs_test.effects` a obnovení `jev` a `jobs.paused_classes`; schéma a tabulka `jobs_test.effects` s právy pro `eshopguard_worker`. `JobsCollection` s `DisableParallelization = true`.
- [ ] 7.3 `TestHandlers.cs`: `test.record` (zapíše účinek v `CompleteAsync`), `test.block` (čeká na bránu z testu), `test.fail` (vrací `Retry`/`Fail` podle payloadu), `test.fetch_domain` (získá zámek domény, zapíše interval, 100 ms, uvolní, zařadí pokračování do N dávek s `JobKeys.Domain`), `test.pause` (vrátí `PauseClass`), `test.cancel_aware`.
- [ ] 7.4 `WorkerHarness.cs`: spustí N instancí `JobProcessingService` v procesu (vlastní `Worker:Id`, `LeaseSeconds = 2`, `HeartbeatSeconds = 0.5`, `Scheduler:TickSeconds = 0.5`), čeká na podmínku s časovým limitem a při selhání vypíše stav `ops.jobs`.

## 8. Testy z podkladu

- [ ] 8.1 `NoDoubleProcessingTests`: 4 workery × 8 slotů `cpu`, 1 000 úloh `test.record`; 1 000 účinků, 1 000 různých `job_id`, všechny `succeeded`, `attempts = 1`; časový limit 120 s.
- [ ] 8.2 `KilledWorkerTests`: worker A převezme `test.block`, `SimulateCrashAsync`; po leasu plánovač workeru B úlohu vrátí (`last_error = job.lease_expired`), B ji dokončí; `attempts = 2`, jeden účinek (od B).
- [ ] 8.3 `StuckWorkerTests`: A převezme `test.block` s vypnutým heartbeatem (háček), B převezme po vypršení a dokončí; uvolnění brány A → `LeaseLostException`; účinek jen od B, úloha `succeeded`, `attempts = 2`, `lease_owner` null.
- [ ] 8.4 `DomainAlternationTests`: běh A (tenant A) a běh B (tenant B), každý `test.fetch_domain` s 5 dávkami na `shop.test`, 2 workery se 4 sloty `fetch`; intervaly se nepřekrývají, pořadí A, B, A, B, …; v `ops.domains` po doběhnutí `lease_job_id` null.

## 9. Další testy

- [ ] 9.1 `EnqueueTransactionTests`: potvrzení → úloha + oznámení; vrácení → nic; bez transakce `job.enqueue_requires_transaction`; `pg_notify` přijde až po `COMMIT` (posluchač v testu).
- [ ] 9.2 `DedupeAndConcurrencyKeyTests`: stejný `dedupe_key` → `Created = false` a stejné `JobId`; 10 úloh se stejným `concurrency_key` a 4 workery → intervaly se nepřekrývají; ruční souběžné převzetí dvou úloh se stejným klíčem → jedna 23505, nejvýš jedna `running`.
- [ ] 9.3 `PriorityTests`: P0 před starší P2; FIFO u stejné priority; `not_before` v budoucnu se nepřevezme.
- [ ] 9.4 `RetryAndFailureTests`: `Retry` 2× pak úspěch → odstup roste (poměr druhého a prvního odstupu v rozmezí 1,33–3,0, dané rozptylem ±20 %); `max_attempts = 3` se stálým `Retry` → `failed` s kódem; `Fail` → hned `failed`; `Defer` → `attempts` beze změny; neošetřená výjimka → `queued` s `job.unhandled`.
- [ ] 9.5 `CancelTests`: `CancelRunJobsAsync` zruší 5 čekajících; běžící `test.cancel_aware` skončí `canceled` do jednoho heartbeatu a pokračování nevznikne; `CancelAsync` běžící úlohy údržby vrátí `false`.
- [ ] 9.6 `TenantCapTests`: tenant A 50 úloh `jev`, B 5, strop 2, 2 workery; vzorkování po 100 ms: běžících A ≤ 2 + počet workerů; první úloha B začne před koncem všech A.
- [ ] 9.7 `PauseTests`: `test.pause` → `jobs.paused_classes` obsahuje `jev`, úloha `queued` se stejným `attempts`, 3 s žádné převzetí `jev`, `fetch` dál běží; `ResumeResourceClassAsync` → převzetí do 5 s.
- [ ] 9.8 `RateLimitTests`: scénáře ze specifikace (vyhrazený podíl, 8 souběžných × 5 rezervací = přesně 10 úspěchů, chybějící bucket, příliš velká dávka); `ReturnTokensAsync` nepřekročí kapacitu; doplňování podle času (po 1 s +10 tokenů s tolerancí ±1).
- [ ] 9.9 `DomainLeaseTests`: držitel, odmítnutí druhého, převzetí po vypršení se zachovaným stavem zdvořilosti, uvolnění cizím nic nezmění, `blocked_until` v budoucnu → `null`.
- [ ] 9.10 `SchedulerTests`: dvě instance, takt 0,5 s, 10 s → jedna `system.ensure_partitions` a jedna `system.cleanup_jobs` na dnešek; takty se nepřekrývají; po zastavení instance A takty provádí B.
- [ ] 9.11 `GracefulShutdownTests`: krátká úloha doběhne a nic dalšího se nepřevezme; dlouhá úloha po `ShutdownSeconds` zpět `queued` se stejným `attempts` a `worker.shutdown`; záznam v `ops.workers` zmizí.
- [ ] 9.12 `UnknownKindTests` (`run.unknown` → `failed`, `job.unknown_kind`, 1 pokus), `WorkerRegistryTests` (řádek se sloty, heartbeat, `draining` při ukončení, smazání neaktivních po 10 min), `MaintenanceHandlersTests` (`system.ensure_partitions` volá `PartitionMaintainer`, `system.cleanup_jobs` smaže `succeeded`/`canceled` starší 7 dní a nechá `failed` mladší 30 dní).

## 10. Dokumentace a poznámky

- [ ] 10.1 `README.md`, oddíl „Fronta a worker“: druhy zdrojů a sloty, priority, `dedupe_key` a `concurrency_key` (`JobKeys.Domain`, `JobKeys.Run`), pravidlo „výsledky jen v `CompleteAsync`, vnější účinky s deterministickými klíči, žádné texty stránek v `payload` ani `last_error`“, pozastavení a obnovení druhu, konfigurace `Worker:*`.
- [ ] 10.2 Do `README.md` poznámka PgBouncer podle designu (transakční režim ano; `LISTEN` přímým spojením; připravené příkazy až s PgBouncer ≥ 1.21).

## 11. Ověření

- [ ] 11.1 `dotnet build EshopGuard.sln`: 0 chyb.
- [ ] 11.2 `dotnet test EshopGuard.sln` (filtr bez `Jev`): všechny testy `EshopGuard.Jobs.Tests` projdou, včetně 8.1–8.4; `EshopGuard.Core.Tests` beze změny počtu.
- [ ] 11.3 Testy 8.1–8.4 spustit 10× za sebou (filtr na třídy); 10 z 10 projde (kontrola nestability souběhu).
- [ ] 11.4 `dotnet run --project src/EshopGuard.Worker` proti `eshopguard`: v `ops.workers` je řádek se sloty; do 15 s je v `ops.jobs` `system.ensure_partitions` na dnešek ve stavu `succeeded`; Ctrl+C → log `worker.stopped` do 90 s a řádek v `ops.workers` zmizí.
- [ ] 11.5 Ruční zkouška skutečného zabití procesu: spustit dva workery, zařadit (přes `psql` jako `eshopguard_owner`, v transakci) úlohu `system.cleanup_jobs` s `not_before` teď, ukončit proces, který ji převzal, přes `Stop-Process -Force` během zpracování (případně s dočasně prodlouženou obsluhou v prostředí Development); do 2 min + takt plánovače úlohu dokončí druhý worker, `attempts = 2`.
- [ ] 11.6 `EXPLAIN` dotazu převzetí z `JobQueueSql` nad `eshopguard_test_jobs` s 100 000 úlohami `queued`: používá částečný index fronty (`resource_class`, `priority`, `not_before`, `id`) WHERE `state = 'queued'`, ne sekvenční průchod; zapsat naměřenou dobu převzetí 10 úloh (dnes neměřeno) jako podklad pro zátěžový test ve změně 17.
