# EshopGuard

Testovací prototyp kontroly textů e-shopů: projde web, vytáhne texty a pomocí modelu Jev (TypeSafe) v nich hledá rizikové environmentální tvrzení, nekalé praktiky z černé listiny a chybějící povinné informace. Výstup je screening, ne právní posouzení. Zadání je v `../Zadání CLI prototyp kontroly textů e-shopu s Jevem.md`, stažené předpisy a podklady v `../podklady/`.

Stav:

- **M1 hotový:** stahování, extrakce, segmentace, deduplikace.
- **M2 hotový:** pravidla v YAML, falešný klient Jevu, vyhodnocení a všechny výstupy.
- **M3 hotový:** skutečný klient Jevu (limit požadavků, opakování při 429/529/5xx, počítání tokenů a ceny), cache odpovědí (od změny 5b v PostgreSQL).
- **Katalog kontrol, první sady (26. 9. 2026):** `eco` draft7 a nový modul `ucp` draft3 (viz `rules/CHANGELOG.md`). Primárním trhem je Slovensko, kde od 27. 9. 2026 platí zákazy podle směrnice EmpCo. Živý test na testovacím e-shopu prochází.
- **M4:** evaluace přesnosti.

Po úpravě `rules/*.yaml` sestavte projekt znovu (`dotnet build`), protože testy i CLI si soubory pravidel kopírují při sestavení.

## Moduly

Rozsah podle země: **Slovensko** = nové povinnosti (od 27. 9. 2026 a od 19. 6. 2026), co trestá SOI, a zákonné informační povinnosti; **Česko** = co trestá ČOI a zákonné informační povinnosti. Bez volby `--modules` běží všechny moduly, které mají pravidla pro zvolenou zemi.

| Modul | Soubor | Co kontroluje | Země | Čte |
| --- | --- | --- | --- | --- |
| `eco` | `rules/eco.yaml` | Environmentální tvrzení podle směrnice (EU) 2024/825 (EmpCo), nové od 27. 9. 2026: obecná tvrzení, „udržitelný“, klimatická neutralita a kompenzace, značky udržitelnosti, tvrzení o celku, budoucí závazky. | SK | věty |
| `dur` | `rules/dur.yaml` | Nové od 27. 9. 2026: tvrzení o životnosti, opravitelnosti, spotřebním materiálu, neoriginálních dílech a aktualizacích softwaru (vše k ověření). | SK | věty |
| `ucp` | `rules/ucp.yaml` | Spotřebitelské recenze z černé listiny (body 23b a 23c), za které ČOI i SOI trestají. Ostatní body černé listiny jsou vypnuté v `rules/ucp_parked.yaml`. | SK, CZ | věty |
| `lr` | `rules/lr.yaml`, `config/legal_requirements.yaml` | Rozpracováno, vypnuto (`enabled: false`): zákonné požadavky na všechny výrobky kategorie vydávané za přednost („dojčenská fľaša bez BPA“) a bezvýznamné výhody, nové od 27. 9. 2026. Kategorie výrobku se čte z titulku, nadpisu, drobečkové navigace a kategorie e-shopu. | SK | věty |
| `legal` | `rules/legal_sk.yaml`, `rules/legal_cz.yaml` | Povinné informace na právních stránkách: mimosoudní řešení sporů, reklamace, odstoupení, vzorový formulář. Na Slovensku navíc za celý web (`site_signal`): harmonizované oznámení o zákonné záruce (od 27. 9. 2026), tlačítko „odstúpiť od zmluvy tu“ (od 19. 6. 2026) a odkaz na zrušenou platformu ODR. V Česku od 1. 1. 2027 tlačítko „Odstoupit od smlouvy“ a údaj o něm v obchodních podmínkách (zákon č. 159/2026 Sb.); do té doby jako upozornění dopředu. | SK, CZ | odstavce právních stránek, celý web |

**Síto po odstavcích.** Než věty dostanou podrobné otázky, rozdělí se hlavní text každé stránky (kromě právních) na úseky po sobě jdoucích bloků do 600 znaků a Jev u každého úseku jednou otázkou na modul řekne, zda v něm je téma modulu (příroda a značky, životnost a opravy, recenze). Věty úseku jdou na podrobné otázky modulu jen při pravděpodobnosti aspoň 0,2. Patička, hlavička, titulek, meta popis a právní stránky jdou vždy celé; úsek, který Jev nevyhodnotí (chyba, text delší než 20 000 znaků), pustí své věty do všech modulů, takže chyba síta nikdy nález neschová. Na vegis.sk (103 stránek) a naturfyt.sk (40 stránek) síto nevynechalo žádný nález a snížilo cenu na 45 % a 57 %; vypíná se volbou `--no-sieve`. Jev přijme asi 32 000 tokenů na požadavek (80 000 znaků slovenského textu prošlo, 90 000 ne), úseky jsou tedy hluboko pod limitem.

**Skupiny nálezů.** Zpráva dělí nálezy podle toho, nakolik je rozhoduje text zákona (pole `checkability` pravidla):

- **porušení podle textu zákona** (`text`): text splňuje znaky zákazu tak, jak je popisuje zákon nebo jeho odůvodnění, například „ekologický“ nebo „šetrný k životnímu prostředí“ bez upřesnění (příklady z odůvodnění 9 směrnice (EU) 2024/825);
- **k posouzení** (`assess`): záleží na tom, jak text chápe průměrný spotřebitel, a Komise to posuzuje případ od případu, například „prírodný“, „BIO“ u kosmetiky nebo odznak „Vegan“;
- **k ověření** (`verify`): záleží na faktech mimo web, například na certifikaci jmenované značky nebo na košíku, který nástroj nestahuje.

Jistota (vysoká, nižší) říká jen, jak si je Jev jistý, že text odpovídá popisu pravidla; zda jde o porušení, určuje skupina.

Každá věta jde do větných modulů (na Slovensku `eco`, `dur` a `ucp`, v Česku `ucp`) jedním voláním Jevu se všemi otázkami všech modulů naráz; cache ukládá odpovědi zvlášť po modulech, takže nová verze jedné sady se ptá znovu jen na své otázky. Na vegis.sk (108 stránek) to snížilo počet volání z 21 182 na 7 084, cenu z 0,98 na 0,75 USD a dobu vyhodnocení zhruba třikrát; odpovědi se od volání po modulech liší stejně málo jako dva běhy téhož způsobu (72 % odpovědí úplně stejných, průměrný rozdíl 0,003). Menu, drobečková navigace, seznamy odkazů a popisky filtrů se nevyhodnocují; odznaky u produktu („Eco“, „Vegan“) se čtou každý zvlášť. Stejný text v titulku nebo meta popisu jako v obsahu stránky dává jeden nález a stejný text se stejným pravidlem na více stránkách také jeden nález se seznamem stránek. Právní odkazy jsou u pravidel zvlášť pro EU, Slovensko a Česko; každý verdikt nese odkazy EU a své země. Pravidla, u kterých se Česko liší (EmpCo zatím nepřevzalo, sněmovní tisk 53), mají v `explanation_by_jurisdiction` vlastní vysvětlení: v Česku jde zatím jen o posouzení klamavého konání případ od případu. Zprávy a české znění otázek jsou česky, Jev dostává otázky anglicky (`--question-lang`).

## Požadavky

- .NET SDK 10.0.4xx (verze je zafixovaná v `global.json` v kořeni repozitáře).
- Pro webovou aplikaci (API, worker) a jejich testy: PostgreSQL 18 na `localhost:5432` a PowerShell 7.4+ pro `deploy/dev/setup-local.ps1`. CLI databázi nepotřebuje.

## Sestavení a testy

Z kořene repozitáře:

```bash
dotnet build src/EshopGuard.sln
dotnet test --solution src/EshopGuard.sln --filter-not-trait "Category=Jev"
```

`dotnet test` běží v režimu Microsoft.Testing.Platform (nastavení `test.runner` v `global.json`) a spustí všech osm testovacích projektů. Kategorie testů:

| Kategorie | Potřebuje | Bez prostředí |
|---|---|---|
| (bez kategorie) | nic, ani síť | – |
| `Db` | PostgreSQL s databázemi `eshopguard_test` a `eshopguard_test_jobs` (testy fronty) a user-secrets `eshopguard-tests` | selže se jménem chybějícího klíče |
| `Jev` | klíč `JEV_API_KEY` nebo `TYPESAFE_API_KEY`, placené volání | přeskočí se |

Testy `Db` se bez prostředí záměrně nepřeskočí, aby nic neprošlo naprázdno. Vynechat je jde jen filtrem, např. na počítači bez databáze:

```bash
dotnet test --solution src/EshopGuard.sln --filter-not-trait "Category=Jev" --filter-not-trait "Category=Db"
```

Jeden testovací projekt přímo (nativní runner xUnit, jiná syntaxe filtru):

```bash
dotnet run --project src/tests/EshopGuard.Core.Tests -- -trait- "Category=Jev"
```

Testy `EshopGuard.Core.Tests` nepotřebují síť. Testovací e-shop se čte ze souborů v `src/tests/EshopGuard.Core.Tests/Fixtures/site/` a testy používají skutečné soubory pravidel z `src/rules/` a `src/config/labels.yaml`.

Testy s kategorií `Jev` projdou proti skutečnému API český testovací e-shop (`Fixtures/site`, země cz, očekávání v `Fixtures/expected_findings.json`) a slovenský (`Fixtures/site-sk`, země sk, `Fixtures/expected_findings_sk.json`). Běží, jen když je k dispozici klíč (`JEV_API_KEY` nebo `TYPESAFE_API_KEY`), a stojí dohromady asi 0,02 USD. Spouštějte je jen po odhadu ceny a se souhlasem:

```bash
dotnet test --solution src/EshopGuard.sln --filter-trait "Category=Jev"
```

## Lokální databáze a úložiště

Webová aplikace (`src/EshopGuard.Api`, `src/EshopGuard.Worker`) se připojuje k databázi `eshopguard` výhradně jako `eshopguard_app` / `eshopguard_worker`. Superuživatel obchází Row-Level Security, proto ho aplikace při startu odmítne (`db.role_bypasses_rls`). Účet `postgres` se používá jen jednou, pro založení rolí skriptem `deploy/sql/00_roles.sql`.

1. Role, databáze `eshopguard`, `eshopguard_test` a `eshopguard_test_jobs`, user-secrets (PowerShell 7, heslo `postgres` jen v této relaci):

   ```powershell
   $env:PGPASSWORD = 'postgres'
   ./deploy/dev/setup-local.ps1            # -PgBin, pokud psql není v C:\Program Files\PostgreSQL\18\bin
   Remove-Item Env:PGPASSWORD
   ```

   Skript vygeneruje hesla rolí, nikam je nevypíše a uloží připojení do `dotnet user-secrets` (`eshopguard-data`, `eshopguard-api`, `eshopguard-worker`, `eshopguard-tests`). Opakované spuštění vygeneruje nová hesla a role uvede do správného stavu.

2. Migrace (jako `eshopguard_owner`, připojení `ConnectionStrings:Migrations` z user-secrets `eshopguard-data`):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/EshopGuard.Data
   ```

   Testovací databáze `eshopguard_test` a `eshopguard_test_jobs` migrují testy samy. Testy fronty mají vlastní databázi, protože před každým testem vyprázdní `ops.jobs`.

3. Spuštění: `dotnet run --project src/EshopGuard.Api` (http://localhost:5080, stav na `/health`) a `dotnet run --project src/EshopGuard.Worker`. Ve vývoji se soubory ukládají do `.data/blobs` ve složce projektu (např. `src/EshopGuard.Api/.data/blobs`, mimo git).

Úložiště souborů je za rozhraním `IBlobStore` (`src/EshopGuard.Storage`). Zatím existuje jen `FileSystemBlobStore` (lokální složka, na serveru připojený svazek); úložiště v cloudu (Azure, AWS…) se později přidá jako další implementace a vybere se v `Storage:Provider`. Složka se musí nastavit výslovně (`Storage:FileSystem:Root`, na serveru `Storage__FileSystem__Root`), jinak aplikace nenastartuje (`config.storage_key_missing`). Klíče souborů tenanta začínají `tenants/{tenantId}/`.

## Databáze

Datový model webové aplikace je v `src/EshopGuard.Data` (EF Core 10, PostgreSQL 18): 61 tabulek v devíti schématech `iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops` a `ref`. Seznam tabulek tenanta a globálních tabulek je jen jeden, `Configurations/Conventions/TableNames.cs`.

- **Data tenanta jen v transakci s kontextem tenanta:** `ITenantContext.Set(tenantId, userId)` a potom `db.ExecuteInTenantTransactionAsync(...)` (čisté SQL a COPY: `TenantSql.BeginAsync`). Interceptor nastaví `app.tenant_id` přes `set_config(…, true)`, hodnota zanikne s transakcí.
- **Izolace na třech úrovních:** filtr EF `"Tenant"`, Row-Level Security s `FORCE` na 41 tabulkách tenanta (politika `tenant_isolation` volá `ops.current_tenant_id()`) a složené cizí klíče (`tenant_id`, `id`), u dělených tabulek (`tenant_id`, `shop_id`, `id`).
- **Bez tenanta žádná data:** dotaz EF skončí `TenantNotSetException`, SQL chybou 42501 „app.tenant_id is not set“. Nikdy tichý prázdný výsledek.
- **Globální tabulky** (bez RLS, bez textů zákazníků): `iam.tenants`, `iam.users`, `iam.user_logins`, `iam.user_tokens`, `shop.free_sample_claims`, `checks.rule_sets`, `billing.price_lists`, `billing.price_tiers`, `billing.volume_discounts`, `billing.promo_codes`, `billing.stripe_events`, `usage.usage_records`, `usage.usage_daily`, `ops.jobs`, `ops.workers`, `ops.domains`, `ops.rate_limit_buckets`, `ops.system_settings`, `ref.markets`, `ref.locales`.
- **Dělené tabulky:** `content.pages` (16 částí podle e-shopu), `content.page_versions`, `checks.jev_answers`, `checks.sieve_answers` (po 32), měsíční `shop.connector_events`, `checks.run_events`, `usage.usage_records`, `ops.audit_log`. Měsíční části zakládá dopředu `ops.ensure_monthly_partitions` (`PartitionMaintainer` ve workeru); výchozí část neexistuje, zápis do měsíce bez části skončí chybou.
- **Trhy:** `ref.markets` má `sk` (EUR) a `cz` (CZK), jazyky `ref.locales` `sk` a `cs` zatím vypnuté.

Jak přidat tabulku tenanta:
1. entita z `TenantEntity` (nebo s `ITenantOwned`) a konfigurace v `Configurations/<Schéma>/`, odkazy přes `HasTenantForeignKey<…>()`;
2. `dotnet ef migrations add …` a v SQL migrace `ENABLE` + `FORCE ROW LEVEL SECURITY`, politika `tenant_isolation` a práva rolí;
3. zápis do `TableNames.TenantTables` a řádek v `TenantDataSeeder` (testy).

Bez toho selže katalogový test (`RlsCatalogTests`, `SeederCoverageTests`, `ModelCatalogConsistencyTests`).

## Fronta úloh a worker

Fronta je tabulka `ops.jobs` (knihovna `src/EshopGuard.Jobs`). API úlohy jen zakládá a ruší (`AddEshopGuardJobQueue`), worker je zpracovává (`AddEshopGuardJobProcessing`). Zpracování je „aspoň jednou“: úloha může proběhnout víckrát, výsledek se ale zapíše jen jednou.

- **Zakládání v transakci:** `db.EnqueueJobAsync(queue, request)` v otevřené transakci (bez ní `job.enqueue_requires_transaction`), nebo `IJobQueue.EnqueueAsync(request, transaction)` nad čistým SQL. Běh tak nevznikne bez své úlohy. Workery dostanou oznámení `eshopguard_jobs` až po COMMIT, po ROLLBACK vůbec.
- **Druhy zdrojů a sloty** (`Worker:Slots`, kolik úloh druhu worker běží najednou; 0 = druh nebere):

  | Druh | Na co | Sloty | Strop tenanta (`Worker:TenantCaps`) |
  |---|---|---|---|
  | `fetch` | stahování | 100 | 20 |
  | `cpu` | výpočty | počet procesorů, když `cpu` chybí | 4 |
  | `jev` | volání Jevu | 8 | 4 |
  | `llm` | volání OpenAI | 4 | 2 |
  | `io` | úložiště, konektory | 4 | 4 |
  | `system` | údržba | 2 | 0 = bez stropu |

  Strop tenanta je měkký: dva workery ve stejné chvíli ho můžou překročit o úlohy, které právě berou.
- **Priority** P0 (uživatel čeká) až P4 (údržba). Bere se v pořadí `priority, id`; úloha s `not_before` v budoucnu se nebere.
- **Klíče:**
  - `dedupe_key`: druhá úloha se stejným klíčem nevznikne, volající dostane existující (`Created = false`);
  - `concurrency_key`: nejvýš jedna běžící úloha s klíčem a vždy se bere nejstarší čekající. `JobKeys.Domain("www.Shop.sk")` = `domain:shop.sk` (stahování jedné domény napříč tenanty, běhy se po dávkách střídají), `JobKeys.Run(runId, krok)`.
- **Lease a fencing:** převzatá úloha má lease `Worker:LeaseSeconds` (120 s), worker ho prodlužuje heartbeatem po `Worker:HeartbeatSeconds` (30 s). Dokončení, selhání i odložení platí jen s vlastním leasem a číslem pokusu. Úlohu spadlého workeru vrátí plánovač po vypršení leasu (`job.lease_expired`). Zaseknutý worker, jehož úlohu mezitím převzal jiný, dostane `LeaseLostException` a nic nezapíše.
- **Pravidla pro obsluhy (`IJobHandler`):**
  - výsledky jen v `context.CompleteAsync(tx => …)`, ve stejné transakci jako stav úlohy; pokračování přes `tx.EnqueueAsync` (u zrušeného běhu nevznikne);
  - vnější účinky mimo databázi (soubory v úložišti) jen s deterministickými klíči, protože úloha může proběhnout znovu;
  - do `payload` jen identifikátory a parametry (nejvýš 64 kB), žádné texty stránek; do `last_error`, výjimek a logů jen kódy, nikdy texty stránek ani klíče;
  - výsledek `Succeeded`, `Retry(kód)` (odstup `Jobs:Retry:BaseSeconds` × 2^(pokus − 1), nejvýš `MaxSeconds`, ±20 %), `Fail(kód)`, `Defer` (bez započtení pokusu), `PauseClass(kód)` nebo `Canceled`. Neošetřená výjimka = `job.unhandled` a opakování, neznámý druh úlohy = hned `failed` s `job.unknown_kind`.
- **Pozastavení druhu:** obsluha při došlém kreditu nebo odmítnutém klíči vrátí `PauseClass("jev.credit_exhausted")`. Druh se zapíše do `ops.system_settings` (`jobs.paused_classes`), do logu jako chyba, a žádný worker ho nebere. Obnovení je jen ruční, workery ho uvidí do 3 s:
  - v kódu `IJobQueue.ResumeResourceClassAsync(JobResourceClass.Jev)`;
  - v SQL jako `eshopguard_worker` nebo `eshopguard_admin`: `UPDATE ops.system_settings SET value = value - 'jev' WHERE key = 'jobs.paused_classes'`.
- **Zrušení:** čekající úloha `IJobQueue.CancelAsync(id)` (běžící úlohu nezruší). Běh: v jedné transakci nastavit `checks.runs.cancel_requested = true` a potom `db.CancelRunJobsAsync(queue, runId)`. Běžící úloha zrušení uvidí při heartbeatu nebo přes `context.IsRunCancellationRequestedAsync()` a vrátí `Canceled`.
- **Globální limity volání:** `IWorkerStore.TryReserveAsync(bucket, tokeny, priorita)` nad `ops.rate_limit_buckets`:
  - `jev`: 1 200 dotazů za minutu;
  - `openai`: 10 000 požadavků za minutu (gpt-6.1-sol, Tier 4);
  - `openai:tokens`: 4 000 000 tokenů za minutu.

  Úlohy P2–P4 nechají 20 % kapacity pro P0–P1. Chybějící bucket je chyba (`ratelimit.bucket_missing`), nikdy volání bez limitu.
- **Zámky domén:** `IWorkerStore.TryAcquireDomainAsync` a `ReleaseDomainAsync` v `ops.domains` se stavem zdvořilosti (robots.txt, Crawl-delay, tempo, chyby); heartbeat úlohy zámek prodlužuje.
- **Plánovač** běží v každém workeru (`Scheduler:Enabled`, takt `Scheduler:TickSeconds` 15 s). Takt provede jen instance se zámkem `pg_try_advisory_xact_lock`:
  - vrací propadlé leasy;
  - denně zakládá `system.ensure_partitions` a `system.cleanup_jobs` (hotové a zrušené úlohy maže po 7 dnech, neúspěšné po 30);
  - maže záznamy workerů bez heartbeatu 10 min.
- **Ukončení workeru** (SIGTERM, Ctrl+C):
  - přestane brát úlohy a v `ops.workers` se označí `draining`;
  - rozdělané úlohy dokončí do `Worker:ShutdownSeconds` (90 s) minus nejvýš 5 s;
  - zbylé vrátí do fronty bez započtení pokusu (`worker.shutdown`) a smaže svůj řádek.

  `ShutdownSeconds` musí být pod leasem, `stop_grace_period` v Compose nad ním.
- **Konfigurace** workeru je v `src/EshopGuard.Worker/appsettings.json` (`Worker:*`, `Jobs:*`, `Scheduler:*`). Neplatné hodnoty zastaví start (`config.worker_invalid`, např. heartbeat není pod třetinou leasu).
- **Výkon** (1. 10. 2026, `ClaimPlanTests`): převzetí 10 úloh z fronty se 100 000 čekajícími úlohami trvá asi 4 ms, jedné úlohy s klíčem z 20 000 asi 2 ms. Zátěžový test celé fronty dělá změna 17.

**PgBouncer** (až od desítek workerů, nasazení ve změně 17):
- V transakčním režimu funguje `set_config(…, true)`, `pg_try_advisory_xact_lock`, `SELECT … FOR UPDATE SKIP LOCKED` i `pg_notify`.
- `LISTEN` nefunguje: posluchač workeru potřebuje přímé spojení `ConnectionStrings:WorkerListen` (bez něj použije `ConnectionStrings:Worker`).
- Zámky na úrovni relace ani `SET` aplikace nepoužívá.
- Připravené příkazy Npgsql (automatická příprava je vypnutá) vyžadují PgBouncer ≥ 1.21 s `max_prepared_statements`.

## Běhy analýzy ve workeru

Ukázka zdarma a úvodní analýza běží ve workeru jako řetěz úloh nad stejnými kroky knihovny jako CLI (`src/EshopGuard.Jobs/Runs`, změna 8). Shodu výsledků s CLI hlídá `CliParityTests`.

- **Založení** jen přes `IRunService` (`AddRunService`), běh a jeho první úloha vždy v jedné transakci tenanta:
  - `CreateFreeSampleAsync`: jednou na doménu napříč tenanty (`shop.claim_free_sample`; druhý nárok vrátí `sample.already_used_for_domain` bez údajů o jiném tenantovi);
  - `CreateFullAnalysisAsync`: vyžaduje ověřené vlastnictví podle `Shops:Ownership:RequiredBefore` (`IShopOwnershipPolicy`, změna 10; bez registrace politiky `DenyAllOwnershipPolicy`, nic se nespustí) a jen jednu běžící úvodní analýzu e-shopu;
  - `MarkOrderPaidAsync` (Stripe, změna 12) a `ApproveWithoutPaymentAsync` (pilot, zápis do auditu) pustí stahování; zaplacení dřív, než skončí zjištění rozsahu, se neztratí;
  - `RequestCancelAsync`: běh skončí `canceled` u další dávky, hned, když nic z něj neběží.
- **Kroky:** `run.discover` → (ukázka) `run.markets` → `run.fetch` po dávkách → `run.profile` → `run.segment` → `run.sieve` po dávkách → `run.plan_evaluate` → `run.evaluate` po dávkách → `run.rules` → `run.rewrite` → `run.finalize`. Stavy běhu: `queued → discovering → (awaiting_payment) → crawling → profiling → segmenting → evaluating → ruling → rewriting → finished | partial | failed | canceled`; každý přechod je událost `run.status` v `checks.run_events` s `pg_notify('run_events', run_id)` po COMMIT.
- **Data běhu:** výsledek každé adresy v `checks.run_urls`, fronta a tempo rozsahu (jazykové verze) v `checks.run_scopes`, stránky a jejich verze s otisky vět v `content.pages` a `content.page_versions`, nálezy v `checks.findings` (stejná věta na více stránkách = jeden nález s výskyty), návrhy oprav ve `fixes.fix_proposals`. HTML, extrakce a pracovní soubory jsou v úložišti pod `tenants/{t}/shops/{s}/runs/{r}/`, výsledek ve tvaru CLI v `result/scan.json.gz`.
- **Nezkontrolované** (fail-closed): `runs.stats.unchecked` po důvodech, úplný seznam v `result/unchecked.json.gz`. Stránky, které selhaly, byly moc velké, nepřečtené včas, přesměrované jinam nebo do vnitřní sítě, bez načteného textu, věty bez odpovědi Jevu a nepřečtené dokumenty dají `partial`; robots.txt, filtr URL a limit stránek se jen vyjmenují. Běh bez jediné zkontrolované stránky je `failed` s kódem (`robots_disallow_all`, `target_not_allowed`, `site_unreachable`, `no_html_pages`).
- **Výpadky:** dávka Jevu nebo přepisu, ve které služba část položek dočasně nevyřídila (bez odpovědi, 408, 429, 5xx), se vrátí do fronty s rostoucím odstupem; co už přišlo, je v cache a znovu se neplatí. Šestý pokus ponechá, co má, a zbytek uvede jako nezkontrolovaný (`partial`). Odmítnutý klíč nebo došlý kredit pozastaví druh úloh (`run.paused_internal`) bez spotřeby pokusu; po obnovení (`ResumeResourceClassAsync`) běh pokračuje. Úloha, které dojdou pokusy z jiného důvodu, ukončí běh `failed` s `internal_error`, takže žádný běh nečeká navždy. Adresy e-shopu s chybou mají v `run_urls` stav HTTP a kód (`http_404` → `gone`, `http_429`, `timeout`, `too_large`).
- **Férovost a paměť** (měřeno 2. 10. 2026 v cloudu, falešný Jev): krok zařadí všechny své dávky najednou; další tenant nečeká díky stropu běžících úloh tenanta (`Worker:TenantCaps:Jev` 4 z 8 slotů). Ukázka jiného tenanta skončila, zatímco analýza 5 000 stránek měla ve frontě 247 dávek Jevu (`FairnessTests`). Analýza 5 000 syntetických stránek trvala 2 minuty; vrchol živých objektů procesu `run.segment` 200 MB, `run.rules` 497 MB (`RulesMemoryTests`, na vyžádání s `-explicit only`).
- **Čtení pro API** (změna 10): `IRunReadModel` vrací běh bez interního odhadu, částek, počtů placených volání a názvů služeb; `IRunQueueEstimator` pozici ve frontě (čekající ukázky před ním) a odhad konce z průměrné doby kroků za poslední hodinu (bez historie `null`).
- **Náklady:** před každým placeným krokem se uloží interní odhad (`runs.estimate.internal`, zákazník ho nevidí; `market_usd` zahrnuje i profily, které napíše rozbor verzí v `run.markets`, spotřeba těchto volání je v `usage_records` jako `profile`; profily nad strop ukázky se jen vynechají a jazyk verzí se určí z celého hlavního textu). Ukázka zdarma se zastaví (`failed`, `sample_budget_exceeded`), když odhad překročí `Runs:FreeSample:MaxInternalUsd` (výchozí 1,00 USD); úvodní analýza se kvůli nákladům nikdy nezastaví, nad `Runs:CostAlertRatio` jen varování v logu a metrika `eshopguard.run.cost_over_estimate`. Spotřeba každého volání je v `usage.usage_records`.
- **Ukázka** uloží `runs.stats.sample` (počty podle závažnosti a skupiny, 5 nejzávažnějších nálezů, ukázka opravy nebo důvod, proč chybí) a základ rozsahu `runs.estimate.basis`: po verzích stav, produkty (nebo dolní mez), ostatní stránky sitemap `other_pages` a `translated_share`; po zaškrtnutých zemích (`markets`) kontrolovaná verze a její produkty, u neznámých důvod `unknown_reason`. Bez pásma a částky.
- **Nastavení** v `src/EshopGuard.Worker/appsettings.json`: oddíl `Runs` (dávky, limity ukázky a analýzy) a `EshopGuard` (nastavení knihovny se stejnými klíči jako `config/settings.yaml`, PascalCase; `BaseDirectory` = složka s `rules/` a `config/` vůči složce projektu). Klíče jen z proměnných prostředí `TYPESAFE_API_KEY` (nebo `JEV_API_KEY`) a `OPENAI_API_KEY`. Worker se nespustí (`StartupChecks`), když je povolená vnitřní síť, User-Agent nezačíná `EshopGuard/0.1`, klíč je v konfiguraci, nebo mimo Development a Testing chybí kontakt v `EshopGuard:Crawl:UserAgent` nebo je zapnutý mock.
- **Vývoj** (jen v Development, `dotnet run` ho nastaví z `launchSettings.json`): běh založí `dotnet run --project src/EshopGuard.Worker -- dev seed-run --tenant <id|cli> --shop-url https://vegis.sk/ --kind free_sample|full_analysis [--approve]` (e-shop založí, když chybí, vlastnictví neověřuje); úvodní analýzu čekající na platbu schválí `dev approve-run --tenant <id|cli> --run <id>` (se zápisem do auditu). Samotný worker: `dotnet run --project src/EshopGuard.Worker`. Postup lokálního ověření včetně placených kroků je v `LOKALNI-OVERENI.md`.

## Identita, tenanti a API

Přihlášení, tenanti, členové a pozvánky (změna 9). Logika je v knihovně `src/EshopGuard.Application`, API (`src/EshopGuard.Api`) je tenká vrstva. Popis API je dokument OpenAPI `v1` (`/openapi/v1.json`, jen v Development a Testing); u každé operace jsou v `x-problem-codes` kódy chyb, které může vrátit. Snímek je `src/tests/EshopGuard.Api.Tests/Snapshots/openapi-v1.json`, nový zapíše `ESHOPGUARD_UPDATE_SNAPSHOTS=1` (projít a zapsat do commitu).

- **Chyby jako kódy:** každá chyba je `application/problem+json` s `code`, `params` a `traceId`, bez věty (`detail` chybí). Text skládá frontend. Neočekávaná výjimka je `500 internal_error` bez podrobností.
- **Přihlášení odkazem** (`/api/auth/login-link`, `…/inspect`, `…/consume`): token je jen v e-mailu (za `#`), v databázi `SHA-256`; odpověď je stejná pro existující i neexistující účet; `inspect` nic nespotřebuje, `consume` přihlásí nejvýš jednou. Limity: 60 s mezi odkazy, 5 za hodinu na e-mail a 20 na IP (kbelíky v `ops.rate_limit_buckets`, klíče jen s HMAC).
- **Heslo je volitelné:** stejná odpověď `401 auth.invalid_credentials` pro neexistující účet, účet bez hesla, špatné heslo i zablokování (5 chyb, 15 minut). Odkaz nebo Google zablokování zruší. Nové heslo vymění `security_stamp`, ostatní relace skončí do 5 minut.
- **Google** se zapne jen s `Authentication:Google:ClientId` a `ClientSecret` (user-secrets). Účet se propojí podle e-mailu jen při `email_verified`; selhání vrátí prohlížeč na přihlašovací stránku s `?error=kód`.
- **Jen HTTPS:** pod `/api` dostane čisté HTTP `400 request.https_required`. Lokálně `dotnet run --project src/EshopGuard.Api --launch-profile https` (https://localhost:5443, jednou `dotnet dev-certs https --trust`); za reverzní proxy se schéma bere z `X-Forwarded-Proto` jen od adres v `Proxy:KnownProxies` (výchozí loopback).
- **Relace a CSRF:** cookie `__Host-eg_session` (HttpOnly, Secure, SameSite=Lax, 30 dní s posouváním) nese jen `sub`, otisk `security_stamp`, `amr` a `auth_time`. Každá změna (POST, PUT, PATCH, DELETE) pod `/api` potřebuje hlavičku `X-CSRF-TOKEN` z `GET /api/auth/csrf`; token je vázaný na uživatele, po přihlášení se načte znovu. Výjimka smí být jen pod `/api/webhooks/`.
- **Tenant z adresy:** `/api/t/{tenantId}/…` projde `TenantAccessFilter` (bez členství `404 tenant.not_found`, nízká role `403 auth.forbidden_role` s `requiredRole`, pozastavený tenant `403 tenant.suspended` kromě čtení účtu). Každý koncový bod tam musí mít `.RequireTenantRole(...)` a každá změna CSRF, jinak API nenastartuje (`EndpointPolicyValidator`). Matice rolí je v `design.md` změny 9 a hlídá ji `RoleMatrixTests`.
- **Uživatel bez tenanta:** transakce uživatele před výběrem tenanta (`ITenantContext.SetUser`, `ExecuteInUserTransactionAsync`, `TenantSql.BeginUserAsync`) má `app.tenant_id` nulové UUID (`TenantSql.NoTenant`), které žádný řádek nevlastní. Vlastní členství, pozvánky na vlastní e-mail a pozvánku podle otisku tokenu ukážou jen politiky migrace F5, a jen v takové transakci.
- **E-maily:** šablony `src/EshopGuard.Application/Email/Templates/{sk,cs}/` (předmět, HTML, text; `labels.json` názvy rolí). E-mail s tokenem odchází přímo z požadavku, ostatní přes `ops.outbox`: každý řádek má svou úlohu `email.send` a pošle ho worker v jazyce příjemce. Lokálně je zachytí Mailpit: `docker compose -f deploy/docker-compose.dev.yml up -d mailpit`, schránka na http://localhost:8025.
- **Audit:** `ops.audit_log` zapisuje přihlášení (i neúspěšná), zablokování, hesla, Google, vznik účtu a tenanta, role, členy a pozvánky; bez tenanta jen akce `auth.*` a `user.*`. V `data` nikdy token, heslo ani čitelný e-mail (jen `emailHash`), IP jen jako `ipHash` (HMAC s `Security:IpHashKey`).
- **Nastavení bez tajemství** v `src/EshopGuard.Api/appsettings.json` (`Auth`, `Frontend`, `Email`, `Invitations`, `Tenants`, `Localization`, `Legal`, `RateLimiting`). Tajemství jen v user-secrets nebo proměnných prostředí: `Security:IpHashKey` (32 bajtů base64; `deploy/dev/setup-local.ps1` ho vygeneruje), `Email:Smtp:Password`, `Authentication:Google:ClientSecret`. Bez `Security:IpHashKey`, `Frontend:BaseUrl` a `Email:From` API nenastartuje (`config.application_invalid`). `Legal:TermsVersion` a `Legal:PrivacyVersion` jsou zatím `0`, dokud nejsou zveřejněné dokumenty.

## E-shopy a onboarding v API

E-shopy, rozpoznání platformy, ukázka zdarma, místa prodeje, jazykové verze, rozsah, nabídka ceny, ověření vlastnictví a nastavení (změna 10). Služby jsou v `src/EshopGuard.Application/Shops`, koncové body pod `/api/t/{tenantId}/shops` (čtení každý člen, změny `admin` a `owner`), síťová práce jen ve workeru.

- **Adresa e-shopu** (`ShopUrlNormalizer`): jen `http`/`https` na portu 80 nebo 443, bez IP adresy, vnitřního jména (`localhost`, jméno bez tečky, `.local`, `.internal`, `.lan`, `.home.arpa`) a přihlašovacích údajů; IDN jako punycode, doména bez `www.`, `base_path` `/` nebo `/cesta/`. API adresu nikdy nestahuje. V Development a Testing projdou hostitelé z `Shops:AllowedDevHosts` (např. `localhost:8000`), jinde API s tímto nastavením nenastartuje. Stejnou doménu může mít jiný tenant; smazání je měkké a stejnou adresu jde pak přidat znovu.
- **Rozpoznání platformy** je úloha P0 `shop.detect_platform`, kterou API založí ve stejné transakci jako e-shop a na kterou čeká nejvýš `Api:InteractiveWaitSeconds` (8 s), pak vrátí `pending`. Worker přečte robots.txt a úvodní stránku přes stahovač s ochranou SSRF (`Shops:Detection:MaxBytes` 1 MB, `TimeoutSeconds` 10) a porovná jen technické podpisy z `config/platforms.yaml` (meta značky, hostitelé a cesty načítaných souborů, hlavičky, cookies; text stránky ani odkazy v textu nikdy). Dva druhy podpisů jedné platformy = `certain`, jeden = `likely`, podpisy dvou platforem = `unknown`. Přesměrování na jinou doménu se jen nahlásí, cizí doména se nestahuje. Platformu zvolenou klientem (`PUT …/platform`) další rozpoznání nepřepíše. Podpisy Shoptetu jsou ověřené na uložené stránce, ostatní platformy jsou vedené jako „k ověření“.
- **Způsob napojení** `PUT …/source`: `web` (výchozí), `feed` (adresa stejnou kontrolou jako e-shop, formát `heureka` nebo `google`), `connector` jen s připojeným konektorem; během běžící analýzy `409 shop.run_in_progress`.
- **Ukázka zdarma** `POST …/sample`: jen ze stavu `draft`, podle politiky vlastnictví, nejvýš 5 ukázek tenanta za den (`shops:sample:tenant:*`, kbelík před nárokem domény), pak `IRunService.CreateFreeSampleAsync` (nárok, běh a první úloha v jedné transakci). `GET …/sample` vrací průběh, pozici ve frontě a po skončení výsledek jako kódy: počty nálezů, 5 nejzávažnějších (`ruleId`, `params`, verdikty po zemích), ukázku opravy jen s `recheckStatus = ok` a co nebylo zkontrolováno po důvodech (`byReason`, nic se neskrývá).
- **Místa prodeje** `GET`/`PUT …/markets`: jen země s podporou kontrol (`ref.markets.checks_status` ≠ `none` a zapnutá sada pravidel), u každé důvody jako kódy (`seat`, `tld`, `currency`, `language_version`, `phone_prefix`) a ověřené doslovné citace se stránkou; interní poznámka modelu se nevrací. Předvybrané jsou země se silným důkazem nebo doručením. Potvrzení uloží aktivní a odmítnuté země s `confirmed_by`/`confirmed_at`, ručně přidaná země má `source = user`; během úvodní analýzy nebo sledování `409 markets.locked_during_run`.
- **Jazykové verze** `GET …/languages`: verze bez nepodporovaných jazyků, podíl přeložených popisů ze vzorku, produkty, zda se kontroluje a podle kterých zemí (stejným pravidlem jako cena). Verzi na jiné doméně klient potvrdí (`…/confirmation`), verzi může vyloučit (`…/exclusion`), nikdy ne poslední kontrolovanou. Ruční rozhodnutí drží `shop_languages.decided_at` a rozbor ukázky ho nepřepíše.
- **Rozsah a cena:** `ShopScopeCalculator` (čistá funkce nad `VersionMarketPlanner` knihovny): pro každou zaškrtnutou zemi verze v jejím jazyce, jinak hlavní verze; verze se posuzuje podle všech zaškrtnutých zemí, jejichž zákazníci ji čtou (`readable_languages` v `config/jurisdictions.yaml`); do pásma jde součet produktů za každou zemi, neznámý počet = žádná cena (`scope.product_count_unknown`). `scopeHash` je SHA-256 kanonického JSON. `GET …/scope` vrací uložený stav, `POST …/quote` přepočítá zadané země a vyloučené verze bez uložení a cenu vezme z `IPriceQuoteService` (změna 12); bez ní `503 billing.unavailable`. Základem je vždy dokončená ukázka (`quote.sample_not_finished`, `quote.basis_missing`).
- **Onboarding a objednávka:** `GET …/onboarding` vrací krok (`connect`, `sample`, `scope`, `payment`, `analysis`, `done`) a blokující kódy; `IShopOrderReadiness.CheckAsync` (pro změnu 12) přepočítá rozsah z uloženého stavu a vrátí jeho `scopeHash`, aby objednávka odmítla zastaralou nabídku.
- **Ověření vlastnictví** `…/ownership`: metoda `meta` (`<meta name="eshopguard-site-verification" content="…">` v `<head>` úvodní stránky) nebo `dns` (TXT `_eshopguard.{domain}` s `eshopguard-site-verification=…`), token 22 znaků base64url. Kontrolu dělá úloha P0 `shop.verify_ownership` (meta přes stahovač s robots.txt, DNS přes `DnsClient`). Kdy je ověření povinné, určuje `Shops:Ownership:RequiredBefore` (`sample`, `full_analysis`, nebo samotné `none`) bez výchozí hodnoty: API i worker bez něj nenastartují (K rozhodnutí 1 změny 10). Ve vývoji a testech `full_analysis`.
- **Nastavení** `GET`/`PATCH …/settings`: název, moduly (jen dostupné pro země e-shopu, aspoň jeden), kontrola při uložení (jen s konektorem a ne na BiznisWebu). `PATCH` e-shopu a nastavení nese `version` (xmin řádku) jako `PATCH` tenanta; jiná verze je `409 concurrency.conflict`.
- **Pravidla v API:** API čte stejná pravidla a `config/` jako worker (`EshopGuard:BaseDirectory` vůči složce projektu, ve vývoji `..`), aby země a moduly byly data na jednom místě.

## Nastavení

- `config/settings.yaml`: limity stahování, pravidla segmentace a ceny. Před skenováním cizího webu doplňte do `user_agent` skutečný kontakt.
- `config/labels.yaml`: seznamy značek podle rešerše `podklady/reserse/znacky-udrzatelnosti.md` (nález ruší jen značky, které podmínky splňují), poznámky ke značkám, které je nesplňují nebo jsou neověřené (`label_notes`), a slova pro kontrolu obrázků.
- `config/legal_requirements.yaml`: seznam zákonných požadavků pro modul `lr` (61 položek z rešerše `podklady/reserse/zakonne-poziadavky-ako-prednost.md`).
- `config/sieve.yaml`: síto po odstavcích (otázka na téma každého větného modulu, práh, velikost úseků); vlastní `version`, takže změna síta nezneplatní uložené podrobné odpovědi.
- `rules/*.yaml`: otázky pro Jev, logika pravidel, odkazy na zákon a účinnost (`effective_from`). Každou změnu otázek zapište do `rules/CHANGELOG.md` a zvyšte `version`; otisk otázek nové verze zapíše `eshopguard rules check-texts --update-hashes` do `rules/question-set-hashes.json` (změna otázek bez nové verze se nenačte).
- `rules/texts/<jazyk>/`: texty pravidel (název, vysvětlení, vysvětlení pro zemi, doporučení) po sadách, `_engine.yaml` (poznámky, upozornění a stavy odkazů nástroje) a `_labels.yaml` (poznámky ke značkám). Knihovna vrací kódy a parametry, věty skládá až zpráva v jazyce `--lang`. Viz oddíl Země a jazyky.
- `config/jurisdictions.yaml`: země, které nástroj zná, a jazyk jejich předpisů.
- `.env` (zkopírujte z `.env.example`): klíč a adresa API Jevu. Když `JEV_API_KEY` chybí, použije se proměnná prostředí `TYPESAFE_API_KEY` (i z uživatelského prostředí Windows). Klíč se nikam nezapisuje ani neloguje.
- Tempo stahování se přizpůsobuje serveru: začíná na `crawl.requests_per_second` (1 za sekundu); dokud server odpovídá do 0,5 s bez chyb, zrychluje po 0,25 až na `max_requests_per_second` (3), při odpovědi pomalejší než 1,5 s nebo chybě zpomalí na 70 %, při 429 nebo 503 na polovinu a počká podle Retry-After (nejvýš 60 s) a stránku zkusí znovu (nejvýš dvakrát). Crawl-delay z robots.txt strop sníží. Stahuje se jedním spojením. `--rate` nastaví pevné tempo. Na vegis.sk (107 stránek, server odpovídá za 0,1 s) 111 požadavků za 42 s místo 3,6 min při pevných 0,5 za sekundu.
- Souběžnost: `jev.requests_per_minute: 1200` a `concurrency: 8`, podle dokumentovaného limitu jev-1.13.0 (1 200 požadavků za minutu a 250 000 tokenů za sekundu, https://docs.typesafe.ai/models; limity se mohou měnit, vyšší nabízí firemní tarif). Při odezvě kolem 0,33 s stačí 8 souběžných požadavků na 20 za sekundu. Při odpovědi 429 nebo 529 klient počká a zkusí to znovu. Krátký test 300 požadavků s 32 souběžnými spojeními (77 za sekundu) chybu nevrátil jen proto, že se vešel pod minutový limit.
- Cache odpovědí Jevu, přepisů a profilů šablon je v PostgreSQL, stejně pro CLI i webovou aplikaci: tabulky `checks.jev_answers`, `checks.sieve_answers`, `fixes.rewrite_cache` a `shop.page_profiles` u vyhrazeného tenanta `cli`, role `eshopguard_worker` pod RLS. Žádný lokální soubor cache nevzniká. Připojení je `ConnectionStrings:Cli`: proměnná prostředí `ConnectionStrings__Cli`, `.env`, nebo user-secrets projektu `EshopGuard.Cli` (zapíše je `deploy/dev/setup-local.ps1`); nikdy `settings.yaml`. Jednou je potřeba `eshopguard cache init`, který založí tenanta `cli`; sken ho nikdy nezakládá sám. Bez dostupné databáze, s chybějící migrací nebo bez tenanta běh skončí chybou dřív, než cokoli stáhne nebo zaplatí. Opakovaný běh se stejnými texty a otázkami nic nestojí ani neubírá z limitu Jevu; změna znění otázky (nová `version`) cache pro danou sadu obejde. `--no-cache` vypne cache odpovědí a přepisů (profily šablon zůstávají, jsou šablonou obchodu). S `--mock` se CLI k databázi vůbec nepřipojuje: vymyšlené odpovědi falešného klienta se do cache nikdy nedostanou a profily šablon žijí jen v paměti běhu. Odpovědi ze staré `src/cache/jev-cache.sqlite` se nepřevádějí (rozhodnutí 1. 10. 2026), kód soubor nečte.
- Stahuje se po dávkách: nejvýš `crawl.fetch_batch_max_pages` (100) stránek nebo `fetch_batch_max_seconds` (60 s) v jedné dávce, další dávka pokračuje, kde předchozí skončila (stav fronty URL, tempo a čítače). Výsledek nezávisí na velikosti dávky. Čtení jedné stránky má limit `crawl.extract_timeout_seconds` (30 s); stránka, která trvá déle, se nepřečte a zpráva ji uvede v části „Co nebylo zkontrolováno“.

## Spuštění na testovacím e-shopu

CLI se spouští ze složky `src`: tam hledá `config/settings.yaml`, `rules/` a `.env`, a tam zapisuje `out/`. Z kořene repozitáře skončí chybou „Pravidla nejsou platná: … neexistuje“. Worker (`src/EshopGuard.Worker`) se spouští odkudkoli, cesty bere ze složky projektu.

```bash
cd src
```

V jednom terminálu spusťte testovací e-shop:

```bash
dotnet run --project EshopGuard.Cli -- serve-fixture --port 8000
```

Ve druhém terminálu ho projděte. Během běhu serveru použijte `--no-build`, protože server drží sestavené soubory:

```bash
dotnet run --no-build --project EshopGuard.Cli -- scan http://localhost:8000 --allow-private-network
```

`--allow-private-network` je nutné jen pro místní e-shop: bez něj se adresa v místní síti nestáhne (ochrana proti SSRF, viz níže).

Bez klíče nebo pro zkoušku bez placených volání přidejte `--mock`. Před voláním Jevu se vypíše odhad počtu volání, tokenů a ceny; nad limitem `max_calls_without_confirm` (5 000) se čeká na potvrzení, `--yes` ho přeskočí.

Výstupy jsou ve složce `out/<doména>-<YYYYMMDD-HHMM>/`:

| Soubor | Obsah |
| --- | --- |
| `report.md` | Zpráva pro člověka v češtině |
| `findings.json` | Všechny nálezy se všemi poli a pravděpodobnostmi otázek |
| `findings.csv` | Řádek na nález a prázdné sloupce `human_label` a `note` pro ruční označení |
| `segments.csv` | Všechny unikátní segmenty s kontextem a pravděpodobností každé otázky |
| `pages.jsonl` | Řádek na staženou stránku: adresa, typ, titulek, vytažený hlavní text, počet znaků čitelného textu v HTML a zda se text načetl |
| `sieve.csv` | Každý úsek hlavního textu, který prošel sítem: stránka, pořadí, stav a pravděpodobnost tématu každého modulu |
| `profiles.json` | Profily šablon stránek použité ve skenu: oblasti se selektorem, akcí a důvodem, vzorové stránky, počet stránek a vynechané znaky podle role |
| `run.log` | Průběh běhu |

Hlavní text stránky vybírá SmartReader, který může vynechat například blok s odznaky, cenou a dopravou. Proto se kontroluje i **ostatní viditelný text stránky**: vše, co není hlavní text, hlavička, patička ani navigace (menu, seznamy kategorií, drobečková navigace, filtry). V `pages.jsonl` je jako `rest_text` a segmenty mají zdroj `rest`. **Výpisy jiných produktů** (podobné produkty, doporučené produkty na úvodní stránce) se na cizí stránce nekontrolují. Jejich texty se kontrolují na stránce produktu, odkud pocházejí. Rozpoznávají se podle struktury, ne podle nadpisu: aspoň 3 stejné krátké dlaždice s cenou a odkazem na jinou stránku webu. Při kontrole jen vzorku stránek se proto produkty mimo vzorek nekontrolují.

Zpráva uvádí **pokrytí**: kolik viditelného textu se zkontrolovalo, kolik připadlo na záměrně vynechanou navigaci a výpisy jiných produktů a kolik zůstalo jinak nezkontrolované. Stránky, kde nezkontrolovaný podíl mimo navigaci dosáhne 20 %, zpráva vypíše.

**Profily šablon stránek** (`profiles` v `settings.yaml`) odstraňují ovládací prvky, které rozpoznávání podle struktury nezachytí: cookie lištu, přihlášení, košík, pole a souhlasy formulářů, filtry, záložky. Postup:

1. Po stažení se každá stránka porovná s uloženými profily obchodu. Použije ten, který nechá nejméně jejího textu mimo známé oblasti. Navigace a dlaždice jiných produktů se počítají jako známé vždy. Když i nejlepší profil nechá mimo víc než `max_unknown_share` (10 %), stránka nesedí žádnému.
2. Nesedící stránky se seskupí podle stavby (podobnost názvů prvků a tříd aspoň `template_similarity`, 0,75). Skupina aspoň `min_template_pages` (3) stránek dostane nový profil: model přepisu (`rewrite.model`) dostane zjednodušenou kostru `sample_pages` (3) vzorových stránek a vrátí oblasti šablony se selektory a akcí „kontrolovat“ nebo „vynechat“. První profil obchodu vidí i úvodní stránku a stránku jiné skupiny, aby se naučil společný rámec. Za sken vznikne nejvýš `max_new_profiles_per_scan` (5) profilů. Jejich cena je v odhadu před spuštěním, nad `max_usd_without_confirm` se potvrzuje.
3. Profil se ověří na vzorových stránkách a uloží do databáze cache (tabulka `shop.page_profiles`, e-shop podle domény). Další skeny ho použijí bez modelu, takže rozdíly mezi skeny pocházejí z obchodu, ne z modelu. Nový profil vznikne jen tehdy, když stránky přestanou sedět: obchod změní šablonu nebo přibude sekce s jinou stavbou.

Profil jen ubírá, a to bloky hlavičky, patičky a ostatního textu. **Hlavní text nikdy.** Vynechat smí jen role navigace, výpisy produktů, cookie lišta, přihlášení, vyhledávání, košík, sdílení a pole formulářů. Jinou roli model vynechat nemůže, oblast se kontroluje. Přeskakovací oblast se na stránce nepoužije, když obsahuje oblast ke kontrole nebo delší blok hlavního textu: selektor tam zjevně zachytil něco jiného. Při ověřování na vzorových stránkách se taková oblast změní na kontrolovanou. Blok, který je i mimo vynechané oblasti (odznak u produktu i u podobného produktu), zůstává. Právní stránky a nenačtené stránky profil nepoužívají. Pravidla, která hledají povinné údaje kdekoli na stránce, vidí i vynechané části. Co se vynechalo, je v `pages.jsonl` (`profile_id`, `profile_unknown_share`, `profile_skipped_text`) a v `profiles.json`. Bez klíče OpenAI nebo s `--mock` se nové profily nevytvářejí a stránky bez profilu se kontrolují celé.

Nástroj stahuje HTML a JavaScript nespouští. Stránka, která má v HTML méně než `crawl.min_page_text_chars` (200) znaků čitelného textu včetně menu a patičky, se hlásí jako nenačtená: web ji nejspíš vykresluje až JavaScriptem. Zpráva ji vypíše v části „Co nebylo zkontrolováno“ i se znakem aplikace (Next.js, Nuxt, Angular, prázdný kontejner, hláška „zapněte JavaScript“), v souhrnu a v upozorněních řekne, že u ní chybějící nálezy neznamenají „v pořádku“, a nálezy o chybějících informacích na celém webu doplní poznámkou. Zkontrolovaný zůstává jen titulek, meta popis a popis z JSON-LD. Když se nenačte aspoň polovina stránek, zpráva doporučí připojení přes konektor nebo feed. Části stránek, které se dotahují dodatečně (widgety recenzí, odpočty), takto rozpoznat nejde.

## Jeden text

```bash
dotnet run --project EshopGuard.Cli -- check-text "Ekologický šampon. Obal je ze 100 % recyklovaného papíru."
```

Vypíše pro každou větu pravděpodobnost každé otázky a výsledek každého pravidla. `--kind legal` vyhodnotí text jako právní stránku.

## Přepis problematických pasáží

```bash
dotnet run --project EshopGuard.Cli -- rewrite out/shop.sk-20260930-0919 --limit 5
```

Vezme výsledky hotového skenu (`findings.json`, `pages.jsonl`) a stránky s nálezem ve skupině porušení nebo k posouzení pošle modelu OpenAI (`rewrite.model`, výchozí `gpt-6.1-sol`). Model dostane celý text stránky v číslovaných blocích a vrátí jen změněné bloky („původně → nově“), místa „[doplňte: …]“ pro fakta, která zná jen obchod, a zdůvodnění; nálezy, jejichž znění jen popisuje složení nebo původ, může ponechat. Nástroj každý změněný blok znovu zkontroluje svými pravidly (se sousedními bloky jako okolím) a označí ho jako vyřešeno, čeká na doplnění, stále nález nebo ponecháno. Výstupem je `rewrite.md` (u každé stránky změny „původně → nově“, zdůvodnění a celý opravený text se zvýrazněnými změnami) a `rewrite.json` ve složce skenu; v něm má každá stránka pole `blocks` s každým blokem textu před přepisem a po něm (`original`, `rewritten`, `changed`), podklad pro zobrazení rozdílů ve webu.

- Zadání je v `config/rewrite.yaml`: pokyny, příklady špatných a dobrých znění a doslovné výňatky ze zákona, směrnice a výkladu Komise. Tato společná část jde v každém požadavku první a je pro všechny stránky stejná, takže ji OpenAI po první stránce účtuje z mezipaměti (0,10 místo 2,00 USD za milion tokenů); stránka a její nálezy jdou až za ni. Každou změnu zadání zapište do `rules/CHANGELOG.md` a zvyšte `version`.
- Klíč je v `OPENAI_API_KEY` (`.env`, proměnná prostředí nebo uživatelské prostředí Windows); nikam se nezapisuje. Na kontrolu přepisů je potřeba i klíč Jevu.
- Před voláním vypíše odhad ceny; nad `rewrite.max_usd_without_confirm` (1 USD) se zeptá, `--yes` dotaz přeskočí. Hotové přepisy se ukládají do databáze cache (tabulka `fixes.rewrite_cache`) podle stránky, nálezů, modelu a verze zadání, takže opakovaný běh nic nestojí.
- Měřeno 30. 9. 2026: vegis.sk 24 stránek, 36 nálezů za 0,25 USD (91 s), naturfyt.sk 12 stránek, 28 nálezů za 0,15 USD (81 s); z mezipaměti OpenAI 78–81 % vstupu. Kontrola přepisů Jevem stojí setiny centu.
- Návrhy píše jazykový model: před zveřejněním je musí zkontrolovat člověk a konečné znění posoudit právník. Kontrola pravidly není úplná pojistka.

## Místa prodeje a jazykové verze

```bash
dotnet run --project EshopGuard.Cli -- markets https://www.example.sk --yes
```

Zjistí, do kterých zemí e-shop prodává a jaké má jazykové verze (změna 7). Výsledek zapíše do `out/<web>-markets-<datum>/markets.json` a průběh do `markets.log`. Databázi nepotřebuje, kromě volby `--profiles`.

1. **Technické znaky bez modelu** z úvodní stránky a sitemap:
   - `html lang`, `hreflang` (i `xhtml:link` v sitemap);
   - měny ve strukturovaných datech, mezinárodní předvolby (podle stavby čísla E.164), doména nejvyššího řádu;
   - odkazy úvodní stránky a patičky;
   - kandidáti přepínače jazyka podle stavby adresy (`/sk/`, `?lang=sk`, `sk.shop.cz`, `shop.cz` → `shop.sk`), nikdy podle textu odkazu.
2. **Výběr stránek o prodeji.** Model (`rewrite.model`) vybere ze seznamu odkazů nejvýš 4 stránky. Použije se jen adresa ze seznamu. Chybějící doplní stránky z konektoru a právní stránky.
3. **Rozbor míst prodeje.** Model určí domovskou zemi a země prodeje. U každé uvede sílu důkazu:
   - `strong`: vlastní verze nebo doména, měna, sídlo;
   - `delivery`: konkrétní podmínky doručení;
   - `generic`: např. „do celé EÚ“.

   Každá citace se ověří proti textu stránky. Neověřená citace se zahodí a země bez ověřeného důkazu se nenabídne. Volný text modelu je jen v poli `internal`.
4. **Domovská země** se bere z ověřené adresy. Když chybí, určí ji doména podle `tlds` v `config/jurisdictions.yaml` a klient ji potvrdí. Doména `.com` dá kód `home_country_unknown`.
5. **Jazykové verze.** Najdou se z `hreflang`, přepínače, konektoru, a až nakonec z výsledku modelu. Každá verze se vyzkouší stažením:
   - vlastní adresa: stránka musí mít jazyk verze;
   - cookie (`?lang=sk` nastaví cookie);
   - jazyk prohlížeče (`Accept-Language`);
   - přepínač jen ve skriptu: `needs_browser`;
   - překlad v prohlížeči (Weglot a podobné): kontroluje se původní text;
   - adresa vrací jiný jazyk: `mismatch`;
   - prázdná aplikace: `version_text_not_loaded`.

   Verze na jiné doméně čeká na potvrzení klientem (`needs_confirmation`). Verze v jazyce nepodporovaného trhu je `unsupported`, i když se jazyk zjistí až ze stránky (doména z přepínače, např. bonami.it): nestahuje se, nepotvrzuje a nebere místo ve vzorku.
6. **Plán a cena za každou zemi.** Pro každý zaškrtnutý trh se kontroluje verze v jeho jazyce, jinak hlavní verze. Verze se posoudí podle všech zaškrtnutých zemí, jejichž zákazníci ji čtou (`readable_languages`). Do pásma jde součet produktů za každou zaškrtnutou zemi: počet produktů verze kontrolované pro tu zemi (`plan.by_market`, `plan.counted_products`; rozhodnutí 2. 10. 2026). E-shop s jednou verzí a dvěma zeměmi má dvojnásobek. Neznámý počet kterékoli země nechá součet neznámý (`plan.product_count_unknown`). Odškrtnutí země přepočítá plán bez stahování (`VersionPlan.Recalculate`).
7. **Rozbor verzí v ukázce** (při víc verzích). Texty verzí se neporovnávají a produkty se nepárují:
   - vzorek 100 stránek: povinné stránky každé verze a pak náhodné produkty všech verzí střídavě;
   - produktem je stránka označená v sitemap jako produkt, s JSON-LD `Product`, s jednou položkou `Product` v mikrodatech nebo s `og:type=product`;
   - **verze bez produktové sitemap** (jedna sitemap míchá produkty, kategorie a články, např. Shoptet): stáhne se nejvýš 40 náhodných stránek její sitemap (`markets.product_probe_pages`) a do vzorku jde nejvýš 20 z nich, které jsou podle struktury stránky produkt. Rozhoduje jen struktura, ne slova v adrese. Počet produktů verze zůstane neznámý (`version_product_count_unknown`), dodá ho konektor;
   - **jazyk popisů produktů** jedním voláním modelu na verzi: dvě první věty popisu u 20 náhodných produktů verze (`markets.language_products`, `markets.language_fragments_per_version`). Produkt má popis v jiném jazyce, jen když model víc než polovině jeho vět určil jiný jazyk než jazyk verze (i u hlavní verze). Řádek verze má `translated_share` (podíl produktů s popisem v jazyce verze) a `description_languages` (`labeled_products`, `translated_products`, `foreign_text_products`, `foreign_text_language`). Popis v jiném jazyce je upozornění `untranslated_text` a `versions_untranslated_texts` pro 3c (`language`, `share`, `products`, `of`, `text_language`); na cenu vliv nemá. Věty, kterým model určil jazyk, jsou u verze v `details[].language_fragments`;
   - **popis z profilu** (`--profiles`, ve workeru vždy): věty se berou jen z popisu produktu, tj. z oblastí profilu šablony s rolí `main_description` a `short_description` bez oblastí jiných rolí uvnitř (recenze, související produkty). Recenze v jiném jazyce by jinak vypadaly jako nepřeložený popis. Chybějící profil produktové šablony napíše model (odhad ceny se potvrzuje, 0,07–0,15 USD za profil: vegis.sk a naturfyt.sk 1. 10. 2026, goodie.sk 0,149 USD 2. 10. 2026) a uloží ho; kontrola webu ho pak použije znovu. Stránka bez profilu nebo bez oblasti popisu dá věty z celého hlavního textu; odkud věty jsou, říká `description_languages.basis` (`description`, `main_text`, `mixed`);
   - méně než 10 produktových stránek verze ve vzorku (`markets.min_sample_products`) je upozornění `version_sample_insufficient`, jazyk bez štítků `version_language_unknown`.

   **Počet produktů verze** = jedinečné adresy produktových sitemap v jejím rozsahu. Produktové sitemap se čtou před ostatními a jedna smí mít až 50 MB (`crawl.max_sitemap_bytes`, protokol sitemap). Když se některá nenačte nebo čtení zastaví `crawl.max_sitemap_urls`, počet je neznámý (`version_product_count_incomplete`) a řádek verze má dolní mez `product_count_at_least`.

   Věta pro 3c (`summary`) je podle priority: verze k potvrzení, potřeba prohlížeče, překlad v prohlížeči, jiný jazyk, malý vzorek, nepřeložené popisy, pak `versions_by_market` (`languages`, `urls`, `markets` země → jazyk verze, `translated_share`) a u jediné verze `versions_single`. Ostatní platné kódy jsou v `notices`.

Další vlastnosti:
- **Cena.** Před prvním voláním modelu se vypíše odhad (znaky / 3,2, ceny z `rewrite`). Nad `rewrite.max_usd_without_confirm` se příkaz zeptá, `--yes` dotaz přeskočí. Ověřeno výzkumnými skripty 1. 10. 2026: 0,03–0,08 USD na e-shop za místa prodeje, 0,043 USD za jazyk 46 párů z 5 e-shopů. Zadání v knihovně jsou nová a čísla pro ně zatím nejsou změřená (úkoly 7.3 a 7.4 změny 7).
- **Fail-closed.** Chyba modelu (`market_analysis_failed`), chybějící klíč (`model_missing_key`) i nepotvrzený odhad (`market_analysis_not_confirmed`) vrátí jen technické znaky, verze ze stavby webu a domovskou zemi z domény k potvrzení.
- **Cookie po rozsazích.** Klient pro stahování nemá společný kontejner cookie (`UseCookies = false`). Cookie drží každé procházení zvlášť (web nebo jazyková verze), takže se nepřenášejí mezi weby, verzemi ani tenanty. `--mock` místa prodeje nezjistí, ukáže jen znaky a verze.
- **Profily (`--profiles`).** Profily šablon jsou v PostgreSQL jako u `scan` (tenant `cli`, připojení `ConnectionStrings:Cli`); příkaz databázi před stažením ověří. Bez klíče OpenAI nebo s nepotvrzeným odhadem se použijí jen uložené profily (`version_profiles_not_confirmed`). S `--mock` se profily nevytvářejí ani nečtou.
- Volby: `--markets sk,cz` (zaškrtnuté trhy), `--profiles`, `--out`, `--mock`, `--yes`, `--allow-private-network`. Výpis má u verze počet produktů, popisy v jazyku verze („19 z 20“) a země, pro které se kontroluje, a pod tabulkou produkty za každou zemi a jejich součet.

## Příkazy

| Příkaz | Popis |
| --- | --- |
| `scan <url>` | Projde web podle robots.txt a sitemap, vyhodnotí texty a vytvoří výstupy. Volby: `--max-pages`, `--sample-products`, `--modules` (výchozí: všechny moduly s pravidly pro zvolené země), `--country` (výchozí `sk`), `--jurisdictions` (víc zemí, např. `sk,cz`), `--lang` (jazyk textů ve zprávě, výchozí `cs`), `--as-of` (datum pro účinnost pravidel), `--question-lang`, `--rate` (pevné tempo stahování), `--concurrency`, `--include`, `--exclude`, `--out`, `--mock`, `--no-cache`, `--no-sieve`, `--yes`, `--record <složka>` (uloží všechny odpovědi webu), `--replay <složka>` (odpovídá z nahrávky bez sítě), `--allow-private-network` (jen místní testovací e-shop). |
| `markets <url>` | Místa prodeje s ověřenými citacemi, jazykové verze, plán verzí s produkty za každou zemi a jazyk popisů verzí v ukázce; zapíše `markets.json`. Volby: `--markets`, `--profiles` (jazyk z popisů produktů podle profilu šablony, chybějící profil vytvoří model), `--out`, `--mock`, `--yes`, `--allow-private-network`. |
| `check-text "<text>"` | Vyhodnotí jeden text. Volby: `--kind`, `--modules`, `--country`, `--jurisdictions`, `--lang`, `--as-of`, `--category` (kategorie výrobku pro modul `lr`), `--question-lang`, `--mock`. |
| `rewrite <složka skenu>` | Navrhne přepis problematických pasáží modelem OpenAI a znovu je zkontroluje pravidly všech zvolených zemí. Volby: `--country`, `--jurisdictions`, `--lang`, `--limit`, `--mock`, `--no-cache`, `--yes`. |
| `rules check-texts` | Vypíše po jazycích, zda jsou texty pravidel úplné a zkontrolované, a co chybí; `--update-hashes` zapíše otisky otázek nových verzí sad. |
| `rules extract-texts` | Jednorázový přesun textů z `rules/*.yaml` do `rules/texts/` (hotovo 1. 10. 2026); `--skeleton <jazyk>` vytvoří kostru překladu do nového jazyka. |
| `serve-fixture` | Lokální testovací e-shop (`--port`, `--root`). |
| `cache init` | Jednou založí v databázi cache tenanta `cli` a vypíše, kolik odpovědí, přepisů a profilů cache obsahuje. |
| `bench-extract --replay <složka>` | Změří čas procesoru na stránku pro čtení HTML, extrakci, typ stránky a profily šablon nad nahrávkou (`--runs`, sestavení Release). |
| `evaluate` | Měření přesnosti na označeném vzorku (M4). |

## Země a jazyky

- **Víc zemí v jednom běhu.** `--jurisdictions sk,cz` vyhodnotí pravidla obou zemí nad společnými odpověďmi Jevu.
  - Věta se ptá jednou.
  - Právní odstavec dostane dva dotazy jen tam, kde se slovenské a české otázky se stejným id liší znění. Na testovacím slovenském e-shopu je to 9 → 18 dotazů na odstavce, věty a síto beze změny.
  - Nález téhož pravidla a téhož textu je jeden a nese verdikt pro každou zemi (stav, skupina, závažnost, jistota, odkazy, účinnost). Řadí se podle nejpřísnějšího verdiktu.
  - Zpráva při víc zemích přidá oddíly „Povinnosti za celý web“ (splněno, chybí, platí později, nezkontrolováno s důvodem) a „Pokrytí zemí“ (které moduly v které zemi neběžely).
- **Účinnost.** Pravidlo s `effective_from` se před datem vyhodnotí také, ale verdikt je „platí později“ a ve zprávě je ve skupině „Platí později“, ne mezi porušeními. Datum vyhodnocení je dnešek, nebo `--as-of`.
- **Texty pravidel a jazyky.**
  - Jazyk textů ve zprávě určuje `--lang` (výchozí `cs`). Zpráva jde napsat v jazyce, jehož texty nástroje (`_engine.yaml`, `_labels.yaml`) jsou úplné a zkontrolované.
  - Texty sady, jejíž překlad chybí nebo ho nezkontroloval člověk, se ukážou v jazyce, ve kterém byla sada napsaná. Proto je `legal_sk` i v české zprávě slovensky, jako dosud.
  - Překlad se používá, jen když má v `review` vyplněné `reviewed_by` a `reviewed_at`. Návrh modelem (`machine_draft: true`) se nepoužije, dokud ho nezkontroluje člověk.
  - Stav ukáže `eshopguard rules check-texts`. Slovenské texty a český překlad `legal_sk` jsou strojové návrhy (2. 10. 2026) a čekají na kontrolu člověkem. Do té doby zpráva v `sk` nejde napsat a `legal_sk` se v české zprávě ukazuje slovensky.
- **Další trh (Německo, Polsko, Maďarsko…) bez změny kódu:**
  1. řádek v `config/jurisdictions.yaml` (`law_language`, a pro místa prodeje `country`, `language`, `readable_languages`, `tlds`);
  2. sady `rules/<modul>_<země>.yaml`, nebo země navíc u sady, která platí beze změny;
  3. texty ve složce `rules/texts/<jazyk>/`, kostru vytvoří `eshopguard rules extract-texts --skeleton <jazyk>`;
  4. název země v `_engine.yaml` každého jazyka;
  5. `eshopguard rules check-texts --update-hashes`.

  CLI ověří země, moduly i jazyk proti načteným pravidlům (`NewMarketTests`).

## Ochrana proti SSRF

Nástroj stahuje jen adresy `http` a `https` na portech 80 a 443 bez jména a hesla v adrese. Každé spojení jde přes kontrolu po DNS (`SocketsHttpHandler.ConnectCallback`): když kterákoli adresa hostitele leží ve vnitřní, místní, metadatové nebo vyhrazené síti (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8, 169.254.0.0/16, 100.64.0.0/10, 0.0.0.0/8, dokumentační a testovací rozsahy, multicast, 240.0.0.0/4 a obdoby v IPv6 včetně `::1`, `fc00::/7`, `fe80::/10` a IPv4 mapovaných do IPv6), spojení se nenaváže. Socket se připojí přesně na ověřenou adresu, takže změna DNS mezi kontrolou a spojením neprojde. Stejně se kontroluje každý krok přesměrování, robots.txt a sitemap. Klient pro stahování nepoužívá proxy (proxy by se připojila místo nás, mimo kontrolu).

- Zablokovaná stránka se nikdy nevydává za zkontrolovanou: je ve výsledku (`BlockedUrls`), v upozorněních a ve zprávě v části „Co nebylo zkontrolováno“.
- Když vede do vnitřní sítě už adresa webu, sken skončí chybou `ssrf_blocked` a nic se nestáhne.
- Výjimka je jen volba `--allow-private-network` pro místní testovací e-shop. V `settings.yaml` ji nastavit nejde; webová aplikace ji nepoužije nikdy.

## Nahrávky a porovnání výstupů

`scan <url> --record snapshots/<web>` uloží každou odpověď webu (`index.jsonl` s adresou, stavem a hlavičkami, těla v `bodies/`). `scan <url> --replay snapshots/<web>` pak odpovídá z nahrávky bez sítě; adresa, která v ní není, dostane 404. Nahrávky cizích e-shopů jsou cizí obsah, proto jen lokálně (`src/snapshots/`, `src/baselines/` jsou v `.gitignore`).

- Referenční výstupy testovacích e-shopů z kódu před změnou 5 jsou v `src/tests/EshopGuard.Core.Tests/Baselines/`; `PipelineEquivalenceTests` hlídá, že dnešní kód dává po vynechání časů a tempa stejné soubory. Klíče cache Jevu hlídá `JevCacheKeyCompatibilityTests`, takže uložené odpovědi zůstávají platné.
- Nad lokálními nahrávkami: `dotnet run --project src/tests/EshopGuard.Cli.Tests -- -explicit only -trait "Category=Snapshot"` (spouští skutečné CLI `scan --replay --mock` a porovná výstupy s `src/baselines/`).
- Čas procesoru na stránku (`bench-extract`, Release, cloud se 4 jádry, 5 průchodů, medián) po změně 5, kdy se HTML čte jednou místo pěti: vegis.sk 48,7 ms (opakování 47,5) místo 65,4 ms, www.naturfyt.sk 47,7 ms (49,4) místo 79,8 ms, tedy o 26 % a 40 % méně.

## Použití knihovny bez CLI

```csharp
services.AddEshopGuard(options =>
{
    options.Jev.ApiKey = configuration["TYPESAFE_API_KEY"];   // nebo options.Jev.UseMock = true
    options.Rules.Directory = "rules";
    options.Rules.LabelsFile = "config/labels.yaml";
});

var guard = provider.GetRequiredService<IEshopGuard>();
var result = await guard.AnalyzeTextsAsync(
    [new TextInput { Text = "Tento šampon je ekologický a šetrný k přírodě." }],
    new AnalyzeOptions { Jurisdictions = ["sk", "cz"] });   // nebo Country = "cz"; výchozí je "sk"

// Knihovna vrací kódy a parametry (verdikty po zemích, FindingNote, ScanWarning); věty skládá RuleTextRenderer.
var catalog = await provider.GetRequiredService<IRuleSetProvider>().LoadAsync();
var texts = new RuleTextRenderer(catalog).Render(result.Findings[0], "cs");   // Title, Explanation, Recommendation, Notes, LegalRefs
```

Knihovna je rozdělená na kroky (`EshopGuard.Core.Pipeline`): zjištění rozsahu (robots.txt, sitemap), stahování po dávkách, extrakce, profily šablon, segmenty, odhad ceny, síto, Jev, pravidla a přepis. Vstupy a výstupy kroků jsou záznamy serializovatelné do JSON (se `schema_version`), takže je worker může ukládat mezi úlohami a jiný stroj pokračuje po pádu. `IEshopGuard` je spouští v paměti za sebou (`InMemoryPipelineRunner`), stejně jako dřív jedna služba. Úložiště jsou za rozhraními `EshopGuard.Core.Storage` (`IJevCache`, `IRewriteCache`, `IPageProfileStore`, `IPageContentStore`, `IPageStore`, `IUrlFrontierStore`, `IRateLimiter`); host je zaregistruje před `AddEshopGuard`, jinak platí výchozí (bez cache, úložiště v paměti). Cache v PostgreSQL registruje `services.AddEshopGuardPostgresStores(...)` z `EshopGuard.Data` (CLI s tenantem `cli`, worker s tenantem úlohy). Knihovna nezávisí na databázi (`CoreDependencyTests`).
