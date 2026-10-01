# Tasks

Rozhodnutí uživatele 1. 10. 2026: data ze SQLite se nepřevádějí (původní skupina 5 „Převod dnešní cache“ vypuštěna).

## 1. Výchozí stav (před změnou)
- [x] 1.1 Ověřit, že `JevCacheKeyCompatibilityTests` ze změny 5 projdou (referenční klíče `Fixtures/site` a `Fixtures/site-sk`); klíč `cache_key` v PostgreSQL je tentýž `LegacyKey`. Hotovo: 1 023 klíčů shodných (změna 5); `PgJevCache` ukládá tentýž `LegacyKey`.

## 2. Databáze
- [x] 2.1 Migrace `*_F3CacheInPostgres`: `checks.sieve_answers` stejného tvaru jako `checks.jev_answers` (PK `tenant_id`, `cache_key text`; `response jsonb`, `model text`, `question_set_hash bytea null`, `created_at`), dělení HASH(`tenant_id`) 32 zůstává. `checks.jev_answers` má tento tvar už ze změny 3. Hotovo: migrace `20261001212037_F3CacheInPostgres`. Stará testovací data `sieve_answers` (zapsaná jen testy izolace, bez id otázek) migrace vyprázdní příkazem `TRUNCATE`, který RLS neomezuje.
- [x] 2.2 `fixes.rewrite_cache` a `shop.page_profiles`: ověřit, že tvar ze změny 3 unese dnešní záznamy (klíč a odpověď přepisu; profil s číslem, oblastmi, vzorovými stránkami, modelem a verzí zadání podle e-shopu); měnit jen to, co chybí. Hotovo bez změny: tvar ze změny 3 unese dnešní záznamy (ověřeno testy chování).
- [x] 2.3 Funkce `iam.ensure_cli_tenant()` (`SECURITY DEFINER`, pevné ID tenanta `cli`, `EXECUTE` jen `eshopguard_worker`), v migraci s návratem `Down`. Hotovo: `iam.ensure_cli_tenant()` (pevné ID `00000000-0000-0000-0000-0000000000c1`), `Down` funkci smaže a řádek tenanta nechá (mohou na něj odkazovat odpovědi).
- [x] 2.4 RLS a granty ze změny 3 platí; ověřit testem izolace (3.6). Hotovo: RLS a granty ze změny 3 platí beze změny; worker do `iam.tenants` zapsat nesmí (test).
- [x] 2.5 Do `databaze-a-plan-implementace-2026-10-01.md` zapsat upravený tvar `sieve_answers` a tenant `cli` (K rozhodnutí 2). Hotovo.
- [x] 2.6 Test: migrace proběhne jako `eshopguard_owner` na prázdné i na existující testovací databázi a jde vrátit. Hotovo: na čisté databázi `F3` nahoru, zpět na `F2JobQueue` a znovu nahoru; testovací databáze migrují testy.

## 3. Úložiště nad PostgreSQL
- [x] 3.1 `src/EshopGuard.Data/Stores/PgJevCache.cs`: `GetAsync`; `GetManyAsync` (jeden dotaz `cache_key = ANY($1)` na tabulku druhu); `SetAsync` (`ON CONFLICT DO NOTHING`); serializace `JevResult` stejnými `JsonSerializerOptions` jako dosud. Hotovo.
- [x] 3.2 `src/EshopGuard.Data/Stores/PgRewriteCache.cs` (`IRewriteCache`). Hotovo (stejný klíč přepíše odpověď jako dosud).
- [x] 3.3 `src/EshopGuard.Data/Stores/PgPageProfileStore.cs` (`IPageProfileStore`: `GetAsync(site)`, `AddAsync`; e-shop podle domény v tenantu, založí ho, když chybí). Hotovo; v úloze workeru použije e-shop úlohy (`IStoreTenant.ShopId`).
- [x] 3.4 `src/EshopGuard.Data/Stores/PostgresStoresExtensions.cs`: `AddEshopGuardPostgresStores(Func<IServiceProvider, IStoreTenant>)` registruje všechny tři. Hotovo: `AddEshopGuardPostgresStores(Func<IServiceProvider, IStoreTenant>, cacheAnswers)`.
- [x] 3.5 Testy: `PgJevCacheContractTests`, `PgRewriteCacheContractTests`, `PgPageProfileStoreContractTests` dědí společné testy (změna 5 a nové pro přepisy a profily) a běží proti `eshopguard_test` jako `eshopguard_worker`. Hotovo: společné testy ze změny 5 a nové pro přepisy a profily, jako `eshopguard_worker` proti `eshopguard_test`.
- [x] 3.6 Test izolace: odpověď, přepis a profil tenanta A nejsou vidět tenantovi B. Hotovo: odpovědi (podrobné i síta), přepis a profil tenanta A tenant B nevidí ani dotazem bez podmínky na tenanta.

## 4. CLI napojené na PostgreSQL
- [x] 4.1 `CliConfiguration.cs`: `ConnectionStrings:Cli` z proměnné prostředí `ConnectionStrings__Cli`, z `.env` nebo z user-secrets projektu `EshopGuard.Cli`, nikdy ze `settings.yaml`; v logu ani výstupu se připojení nevypisuje. Hotovo; user-secrets `eshopguard-cli` zapisuje `deploy/dev/setup-local.ps1`.
- [x] 4.2 `CliHost.cs`: bez `--mock` `AddEshopGuardPostgresStores` s tenantem `cli` (`--no-cache` vypne jen odpovědi Jevu a přepisy); s `--mock` bez databáze. Hotovo.
- [x] 4.3 Příkaz `eshopguard cache init`: zavolá `iam.ensure_cli_tenant()` a vypíše stav (tenant, počty odpovědí, přepisů a profilů). Hotovo.
- [x] 4.4 Kontrola před během (`scan`, `check-text`, `rewrite` bez `--mock`): nedostupná databáze, chybějící migrace nebo tenant `cli` znamená chybu s návratovým kódem ≠ 0 ještě před stažením a odhadem ceny. Test s neplatným připojením ověří, že se nic nestáhlo ani nevolalo. Hotovo: test spustí CLI s nedostupnou databází proti místnímu testovacímu e-shopu; skončí kódem 1, hláškou „Databáze cache není dostupná“ a e-shop nedostal žádný požadavek. Ručně ověřeny i hlášky bez tenanta a bez připojení.
- [x] 4.5 Test `SameStoresRegistrationTests`: kontejner CLI vrací `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`, a to z rozšíření `AddEshopGuardPostgresStores`, které použije i worker (registrace ve workeru je ve změně 8). Hotovo: kontejner CLI vrací `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`; s `--no-cache` jen profily z PostgreSQL; s `--mock` žádná databáze.
- [x] 4.6 Test: sken testovacího e-shopu s falešným klientem Jevu a úložišti PostgreSQL uloží odpovědi u tenanta `cli`; druhý běh pošle 0 požadavků a výstupy se shodují. Hotovo (`CliPostgresCacheTests`): první běh 116 požadavků, druhý 0 a 116 odpovědí z cache, nálezy, stránky a segmenty shodné.

## 5. Odstranění SQLite
- [x] 5.1 Smazat `src/EshopGuard.Core/Cache/SqliteJevCache.cs`, SQLite implementace v `Fix/RewriteCache.cs` a `Profiles/PageProfileStore.cs` a jejich registrace; výchozí úložiště knihovny `NullJevCache`, `NullRewriteCache`, `InMemoryPageProfileStore`. Hotovo.
- [x] 5.2 Odebrat `Microsoft.Data.Sqlite` ze všech projektů a z `Directory.Packages.props`; upravit testy, které SQLite používaly (`CacheTests`, `SqliteJevCacheContractTests`). Hotovo; `SqliteJevCacheContractTests` nahradily testy nad PostgreSQL, `CacheTests` a `ProfileTests` používají úložiště v paměti.
- [x] 5.3 Odebrat `CacheOptions.Path` a oddíl `cache:` ze `src/config/settings.yaml`; upravit komentáře, které o souboru cache mluví. Hotovo.
- [x] 5.4 `README.md`, `CLAUDE.md`, `deploy/dev/setup-local.ps1`, `scripts/cloud-setup.sh`: cache v PostgreSQL, nastavení `ConnectionStrings:Cli`, příkaz `cache init`, `--mock` bez databáze. Hotovo.
- [x] 5.5 Test `NoLocalFilesTests`: běh `scan --mock` na místním testovacím e-shopu v dočasné pracovní složce. Po běhu ve složce není `cache/` ani žádný `*.sqlite*`. Hotovo.

## 6. Ověření
- [x] 6.1 `dotnet build src/EshopGuard.sln` bez chyb a varování. Hotovo: bez chyb a varování.
- [x] 6.2 Testy bez placených projdou (`dotnet test --solution src/EshopGuard.sln --filter-not-trait "Category=Jev"`), včetně nových testů `EshopGuard.Data.Tests` a `EshopGuard.Cli.Tests`. Hotovo: 748 testů v sedmi projektech (741 prošlo, 7 explicitních přeskočeno), z toho 312 v `EshopGuard.Core.Tests`, 275 v `EshopGuard.Data.Tests`, 21 v `EshopGuard.Cli.Tests`.
- [x] 6.3 Výstupy CLI shodné s referenčními (`PipelineEquivalenceTests`, test kategorie `Snapshot` nad nahrávkami). Hotovo: `PipelineEquivalenceTests` a test `Snapshot` nad vegis.sk a naturfyt.sk shodné.
- [x] 6.4 Ruční kontrola v cloudu proti lokální databázi: `cache init`; chybové hlášky bez databáze, bez migrace a bez tenanta; `scan --mock` bez databáze. Ostrý běh s Jevem se nedělá (placený, bez souhlasu). Hotovo: `cache init` na čisté databázi tenanta založí a podruhé ohlásí, že existuje; sken bez tenanta skončí radou spustit `cache init`; `scan --mock` běží bez databáze.
- [x] 6.5 `DO_NOT_TRACK=1 OPENSPEC_TELEMETRY=0 openspec validate --all --no-interactive` projde. Hotovo.
- [x] 6.6 Uživateli předat rozhodnutí K 3 (smazat `src/cache/jev-cache.sqlite`). Předáno v souhrnu změny.
