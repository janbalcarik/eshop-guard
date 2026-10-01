# Proposal: Fronta úloh v PostgreSQL a worker

## Intent

**Problém.** Dnešní knihovna dělá celý sken jako jedno volání v paměti. Webová verze musí zpracovat tisíce e-shopů souběžně a spolehlivě:
- pád nebo nasazení workeru nesmí zahodit práci ani ji zdvojit;
- velký e-shop nesmí zablokovat ostatní zákazníky;
- Jev a OpenAI mají jeden limit na klíč pro všechny workery;
- cizí e-shop smí stahovat jen jeden běh najednou, i když ho mají dva tenanti.

Bez fronty s leasy by tohle musela řešit každá obsluha zvlášť.

**Proč teď.** Fáze F2 navazuje na datový model (změna 3, tabulky `ops.jobs`, `ops.workers`, `ops.domains`, `ops.rate_limit_buckets`, `ops.system_settings`). Běhy ve workeru (změna 8), konektory (15) a sledování (16) jsou jen obsluhy úloh nad touto frontou.

**Přínos.**
- `IJobQueue` a `IWorkerStore` nad PostgreSQL: zařazení ve stejné transakci jako běh, výběr `FOR UPDATE SKIP LOCKED`, lease 2 minuty s heartbeatem po 30 s, opakování s rostoucím odstupem, zrušení, `dedupe_key` a `concurrency_key`.
- Dokončení jen s vlastním leasem a číslem pokusu (fencing): zaseknutý worker, kterému úlohu převzal jiný, svůj výsledek nezapíše.
- Druhy zdrojů `fetch`, `cpu`, `jev`, `llm`, `io`, `system` s vlastními sloty, priority P0–P4, strop souběžných úloh na tenanta, pozastavení druhu úloh při došlém kreditu nebo odmítnutém klíči.
- Zámky domén (`ops.domains`) a globální limity volání (`ops.rate_limit_buckets`) s vyhrazeným podílem pro P0–P1.
- Plánovač se zámkem a údržbou (vracení propadlých leasů, měsíční části tabulek, úklid hotových úloh).
- Worker jako Generic Host, který při nasazení dokončí rozdělanou dávku a pustí leasy.
- Testy z podkladu: 4 workery a 1 000 úloh bez dvojího zpracování, zabitý worker, zaseknutý worker nezapíše, dvě úlohy na stejnou doménu se střídají.

**Fáze:** F2 Fronta a worker.

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.8 (`ops.jobs`, `workers`, `domains`, `rate_limit_buckets`, `system_settings`), část 5 (paralelní workery: druhy zdrojů, výběr úlohy, lease, zámky domén, globální limity, proč převzetí nekoliduje, škálování, PgBouncer), část 7 (`EshopGuard.Jobs`, `EshopGuard.Worker`, `EshopGuard.Jobs.Tests`), část 8 (F2);
- `architektura-multitenant-worker-2026-10-01.md`: část 2 (worker a plánovač), část 4 (priority P0–P4, strop na tenanta, vyhrazený podíl Jevu), část 5 (fronta, lease a heartbeat, zakládání v transakci, opakování, pojistky u externích služeb, zrušení, ukončení workeru, úlohy po dávkách, globální limity, domény), část 7 (spolehlivost), část 9 (workery ve stejné síti, `IWorkerStore`).

## Scope

In scope:
- `EshopGuard.Jobs`: `IJobQueue`, `IWorkerStore`, implementace `PgJobQueue` a `PgWorkerStore` (přímé spojení jako `eshopguard_worker`, API jako `eshopguard_app` jen zařazuje a ruší).
- Zařazení (`EnqueueAsync`) v transakci volajícího (EF `EshopGuardDb` i čisté `NpgsqlTransaction`), `dedupe_key` (vrátí existující úlohu), `concurrency_key` (nejvýš jedna běžící úloha na klíč), `not_before`, `max_attempts`, priorita 0–4, oznámení `pg_notify('eshopguard_jobs', …)` po potvrzení.
- Převzetí (`ClaimAsync`) po druzích zdroje s `FOR UPDATE SKIP LOCKED`, pořadí `priority, id`, měkký strop běžících úloh na tenanta, vynechání pozastavených druhů.
- Lease, heartbeat, fencing (`lease_owner` + `attempts`) při heartbeatu, dokončení, selhání a odložení; dokončení a zápis výsledků obsluhy v jedné transakci.
- Opakování s exponenciálním odstupem a náhodným rozptylem, trvalé selhání po vyčerpání pokusů, odložení bez započtení pokusu, vracení propadlých leasů.
- Zrušení: čekající úlohy hned, běžící úlohy kooperativně podle `checks.runs.cancel_requested` (zjišťováno při heartbeatu), potlačení pokračování zrušeného běhu.
- Pozastavení a obnovení druhu úloh (`ops.system_settings`, klíč `jobs.paused_classes`).
- Zámky domén v `ops.domains` (lease, prodlužování, uvolnění se stavem zdvořilosti) a klíč souběhu `domain:{doména}` pro střídání běhů.
- Rezervace tokenů v `ops.rate_limit_buckets` jedním `UPDATE … RETURNING`, vyhrazený podíl pro P0–P1, vrácení nevyužitých tokenů.
- Registr workerů `ops.workers` (sloty, heartbeat, `draining`).
- Plánovač: zámek `pg_try_advisory_xact_lock` na každý takt, úlohy údržby `system.ensure_partitions` (volá `PartitionMaintainer` ze změny 3), `system.cleanup_jobs`, vracení propadlých leasů, úklid registru workerů.
- `EshopGuard.Worker`: zpracování po druzích se sloty z konfigurace, probouzení přes `LISTEN` s pravidelným dotazováním jako pojistkou, korektní ukončení.
- Migrace `F2JobQueue`: indexy a omezení fronty, výchozí řádky `ops.system_settings` a `ops.rate_limit_buckets` (`jev`).
- Nový testovací projekt `tests/EshopGuard.Jobs.Tests` se samostatnou databází `eshopguard_test_jobs`.
- Poznámka k PgBouncer (co s ním funguje a co ne).

Out of scope:
- Obsluhy kroků běhu (`run.discover`, `run.fetch`, …) a orchestrace běhu: změna 8.
- Zpracování `ops.schedules` (noční sledování): změna 16. Plánovač tu umí jen úlohy údržby.
- Odesílání `ops.outbox` (e-maily, SuperFaktúra): změny 9 a 12.
- Adaptér `IRateLimiter` v knihovně `EshopGuard.Core`, který limity knihovny přepne na `ops.rate_limit_buckets`: změna 5.
- Limity jednotlivých konektorů (`connector:shoptet:{eshop}`): změna 15.
- Ukazatele fronty v `/health`, OpenTelemetry, upozornění provozu: změna 17.
- Nasazení PgBouncer, druhá implementace `IWorkerStore` přes API pro vzdálené workery, Redis nebo RabbitMQ.
- Strop souběžných **analýz** na tenanta (architektura část 4: „nejvýš 2 souběžné analýzy na účet podle tarifu“): týká se běhů, řeší změna 8/10. Tady je strop souběžných **úloh** na tenanta a druh zdroje.

## Approach

1. **Logika v knihovně `EshopGuard.Jobs`, worker tenký.** `EshopGuard.Worker` jen čte konfiguraci, registruje obsluhy a spouští `JobProcessingService` a `SchedulerService` z knihovny.
2. **Převzetí jedním příkazem** `WITH c AS (SELECT … FOR UPDATE SKIP LOCKED LIMIT @n) UPDATE … RETURNING` (podklad část 5). Úlohy s `concurrency_key` se berou zvlášť po jedné a jedinečný částečný index `(concurrency_key) WHERE state = 'running'` je poslední pojistka proti souběhu.
3. **Fencing:** převzetí zvýší `attempts`; heartbeat, dokončení, selhání i odložení platí jen s `lease_owner = @me AND attempts = @attempt AND state = 'running'`. Dokončení nejdřív zamkne řádek úlohy (`FOR UPDATE`), pak zapíše výsledky obsluhy a stav, vše v jedné transakci. Kdo lease ztratil, dostane `LeaseLostException` a jeho transakce se vrátí.
4. **Idempotence:** zpracování „aspoň jednou“, výsledky obsluh s jedinečnými klíči (změna 8). Vnější účinky mimo databázi (soubory v úložišti) musí mít deterministické klíče.
5. **Střídání na doméně** zajistí pořadí fronty: stahovací úlohy mají `concurrency_key = domain:{doména}`, dávka po sobě zařadí pokračování na konec fronty a čekající úloha druhého běhu (starší `id`) jde na řadu dřív. Zámek v `ops.domains` je druhá pojistka a nese stav zdvořilosti (robots.txt, Crawl-delay, tempo).
6. **Fail-closed:** neznámý druh úlohy = trvalé selhání `job.unknown_kind`; chybějící bucket limitu = výjimka, ne neomezené volání; pozastavený druh se nebere.
7. **Plánovač bez stavu:** každý takt v transakci se zámkem `pg_try_advisory_xact_lock`; denní úlohy mají `dedupe_key` s datem, takže víc instancí nic nezdvojí.

## Dependencies

- Změna 2 `add-solution-foundation`: projekty `EshopGuard.Jobs`, `EshopGuard.Worker`, role `eshopguard_worker`, `DatabaseStartupGuard`, `00_roles.sql` s parametrem `db_name`.
- Změna 3 `add-multitenant-data-model`: tabulky `ops.*`, `checks.runs.cancel_requested`, `ITenantContext`, `TenantSql`, `PartitionMaintainer`.
- Navazují: změna 5 (`IRateLimiter` nad `IWorkerStore`), 8 (obsluhy kroků běhu, `concurrency_key = run:{id}:{krok}`, pozastavení při došlém kreditu), 15 (konektory), 16 (plánovač sledování), 17 (provozní ukazatele, `stop_grace_period`).

## Done when

- Test: 4 workery a 1 000 úloh, každá zpracovaná právě jednou (1 000 záznamů účinku, 1 000 různých `job_id`, všechny `succeeded`).
- Test: zabitý worker (lease nikdo neprodlužuje, nic neuvolní) → úloha se po vypršení leasu vrátí, jiný worker ji dokončí, `attempts = 2`, jeden záznam účinku.
- Test: zaseknutý worker po převzetí úlohy jiným dostane `LeaseLostException` a jeho zápis v databázi chybí; existuje jen zápis nového vlastníka.
- Test: dvě úlohy dvou tenantů na stejnou doménu se střídají (pořadí dávek A, B, A, B…) a nikdy neběží současně.
- Testy zařazení v transakci, `dedupe_key`, `concurrency_key`, priorit, opakování, zrušení, pozastavení, stropu tenanta, limitů, zámků domén, plánovače a korektního ukončení projdou.
- `dotnet test EshopGuard.sln` projde včetně `EshopGuard.Jobs.Tests`; `EshopGuard.Core.Tests` beze změny počtu.
- `dotnet run --project src/EshopGuard.Worker` zapíše řádek do `ops.workers`, plánovač založí `system.ensure_partitions` na dnešek a Ctrl+C worker ukončí do `Worker:ShutdownSeconds`, řádek v `ops.workers` zmizí.

## K rozhodnutí

1. **Výchozí hodnoty, které podklady neurčují** (všechny jsou v konfiguraci, mění se bez kódu):
   - sloty: `fetch` 100, `cpu` počet jader, `jev` 8 (jako dnešní `concurrency` Jevu), `llm` 4 (jako `rewrite.concurrency`), `io` 4, `system` 2;
   - strop běžících úloh na tenanta a druh: `fetch` 20, `cpu` 4, `jev` 4 (polovina slotů, architektura část 4: „jeden tenant nedostane víc než polovinu výkonu Jevu“), `llm` 2, `io` 4, `system` bez stropu;
   - opakování: 5 pokusů, odstup 10 s × 2^(pokus−1), nejvýš 15 min, rozptyl ±20 %;
   - úklid registru: řádek `ops.workers` bez heartbeatu 10 min se smaže.
   Sloty a stropy potvrdí zátěžový test (změna 17); dnes neměřeno.
2. **Limit OpenAI v `ops.rate_limit_buckets`.** Pro Jev je limit známý (1 200 požadavků/min, `config/settings.yaml`), migrace založí bucket `jev` (kapacita 1 200, doplňování 20/s, vyhrazený podíl P0–P1 20 % podle architektury část 4). Limit OpenAI pro `gpt-6.1-sol` v podkladech není; bucket `openai` založí změna 5 nebo 8 po zjištění limitu účtu. Do té doby rezervace pro `openai` skončí chybou `ratelimit.bucket_missing` (fail-closed).
3. **Úklid hotových úloh.** Podklad: „hotové se po 7 dnech mažou“. Patří mezi „hotové“ i `failed`? Návrh: mazat po 7 dnech `succeeded` a `canceled`; `failed` ponechat 30 dní kvůli rozboru chyb.
4. **Jak se obnoví pozastavený druh úloh** po došlém kreditu nebo odmítnutém klíči (architektura: „po nápravě se pokračuje“)? Tato změna dává metodu `ResumeResourceClassAsync`; kdo ji spustí (správcovská obrazovka, příkaz workeru, automatická zkouška po N minutách), není rozhodnuté.
5. **Rozsah `IWorkerStore`.** Podklad ho zmiňuje jen jako rozhraní, přes které by šla napojit vzdálená implementace. Návrh: `IWorkerStore` = registr workerů, zámky domén a rezervace limitů (vše, co obsluha potřebuje mimo frontu a data tenanta); data běhů (stránky, nálezy) dostanou vlastní úložiště ve změně 8.
6. **Doména jako `concurrency_key`.** Podklad popisuje zámek domény v `ops.domains`; střídání dvou běhů ale zámek sám nezaručí (uvolněný zámek může hned znovu získat stejný běh). Návrh: stahovací úlohy navíc s `concurrency_key = domain:{doména}`, takže pořadí fronty zajistí střídání. Potvrdit.
7. **Samostatná testovací databáze `eshopguard_test_jobs`.** Testy fronty mažou `ops.jobs` a spouštějí workery, které berou jakoukoli úlohu daného druhu; ve sdílené `eshopguard_test` by braly úlohy jiných testovacích projektů (změna 8). Návrh: `00_roles.sql` spustit i s `db_name=eshopguard_test_jobs` (stejné role a user-secrets, jen jiný název databáze v testu).
8. **Zrušení běžící úlohy bez běhu.** Podklad má příznak zrušení jen u běhu (`runs.cancel_requested`). Úlohy bez `run_id` (údržba) jde zrušit jen ve stavu `queued`. Stačí to?
